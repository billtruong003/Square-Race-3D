using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.Arena
{
    /// <summary>
    /// Pressure that follows an authored route: each segment fills its own region progressively and
    /// hands over to the next when it is done. A serpentine course therefore closes behind the
    /// racers lane by lane instead of being faked with one giant moving box.
    ///
    /// Filled regions are axis-aligned boxes, so both the "is this position legal" test and the push
    /// out of a filled region stay exact and allocation free.
    /// </summary>
    public sealed class AuthoredTrackPressure : PressureField
    {
        private sealed class Segment
        {
            public Rect Bounds;
            public int Axis;
            public float Sign;
            public float StartCoordinate;
            public float FillLength;
            public float Speed;
            public float StartDelay;
            public float CompletionDistance;

            public float Distance;          // how far the front has travelled
            public Rect Filled;             // cached filled sub-rect
            public bool Active;
            public bool Complete;

            public Transform Visual;
        }

        private readonly List<Segment> _segments = new List<Segment>(8);
        private readonly Transform _root;
        private readonly float _groundY;
        private readonly float _height;
        private readonly float _startDelay;
        private readonly bool _enabled;

        public override bool Enabled => _enabled;

        public int SegmentCount => _segments.Count;
        public int ActiveSegmentIndex { get; private set; }

        public AuthoredTrackPressure(PressureTrack track, PressureConfig config, float groundY,
            MaterialLibrary materials, Transform parent)
        {
            _groundY = groundY;
            _height = config.height;

            _root = new GameObject("PressureTrack").transform;
            _root.SetParent(parent, false);

            if (track == null || !config.enabled)
            {
                _enabled = false;
                return;
            }

            // Config overrides let an episode pace an authored route without editing the map.
            _startDelay = config.trackStartDelay >= 0f ? config.trackStartDelay : track.StartDelay;
            float scale = config.trackSpeedScale > 0f ? config.trackSpeedScale : track.SpeedScale;

            IReadOnlyList<PressureSegment> authored = track.Segments;
            for (int i = 0; i < authored.Count; i++)
            {
                PressureSegment source = authored[i];
                float length = source.FillLength;
                if (length <= 0.01f)
                {
                    Debug.LogWarning($"[CubeSim] Pressure segment '{source.name}' has zero length; skipped.");
                    continue;
                }

                var segment = new Segment
                {
                    Bounds = source.Footprint,
                    Axis = WallFillMath.Axis(source.Direction),
                    Sign = WallFillMath.Sign(source.Direction),
                    StartCoordinate = source.StartCoordinate,
                    FillLength = length,
                    Speed = source.Speed * scale,
                    StartDelay = source.StartDelay,
                    CompletionDistance = length * source.CompletionFraction
                };

                segment.Filled = ComputeFilled(segment);
                segment.Visual = CreateVisual(i, materials);
                _segments.Add(segment);
            }

            _enabled = _segments.Count > 0;
            ApplyVisuals();
        }

        private Transform CreateVisual(int index, MaterialLibrary materials)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PressureFill_" + index.ToString("D2");
            go.layer = SimulationLayers.Pressure;
            go.GetComponent<MeshRenderer>().sharedMaterial = materials.Pressure;
            go.transform.SetParent(_root, false);
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>
        /// Advances the route. Only one segment fills at a time; completed segments stay filled, so
        /// the closed-off part of the course keeps blocking racers.
        /// </summary>
        public override void Tick(float elapsedTime)
        {
            if (!_enabled) return;

            float budget = elapsedTime - _startDelay;
            if (budget <= 0f) return;

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];

                budget -= segment.StartDelay;
                if (budget <= 0f) break;

                segment.Active = true;
                float travelled = budget * segment.Speed;

                if (travelled >= segment.CompletionDistance)
                {
                    segment.Distance = segment.FillLength;
                    segment.Complete = true;
                    // Time spent finishing this segment is not available to the next one.
                    budget -= segment.CompletionDistance / segment.Speed;
                    ActiveSegmentIndex = Mathf.Min(i + 1, _segments.Count - 1);
                }
                else
                {
                    segment.Distance = travelled;
                    segment.Complete = false;
                    ActiveSegmentIndex = i;
                    segment.Filled = ComputeFilled(segment);
                    ApplyVisual(segment);
                    break;
                }

                segment.Filled = ComputeFilled(segment);
                ApplyVisual(segment);
            }
        }

        private static Rect ComputeFilled(Segment s)
        {
            float d = Mathf.Clamp(s.Distance, 0f, s.FillLength);
            float end = s.StartCoordinate + s.Sign * d;
            float lo = Mathf.Min(s.StartCoordinate, end);
            float hi = Mathf.Max(s.StartCoordinate, end);

            return s.Axis == 0
                ? Rect.MinMaxRect(lo, s.Bounds.yMin, hi, s.Bounds.yMax)
                : Rect.MinMaxRect(s.Bounds.xMin, lo, s.Bounds.xMax, hi);
        }

        private void ApplyVisuals()
        {
            for (int i = 0; i < _segments.Count; i++) ApplyVisual(_segments[i]);
        }

        private void ApplyVisual(Segment s)
        {
            if (s.Visual == null) return;

            bool visible = s.Distance > 0.01f;
            if (s.Visual.gameObject.activeSelf != visible) s.Visual.gameObject.SetActive(visible);
            if (!visible) return;

            s.Visual.localPosition = new Vector3(s.Filled.center.x, _groundY + _height * 0.5f, s.Filled.center.y);
            s.Visual.localScale = new Vector3(s.Filled.width, _height, s.Filled.height);
        }

        public override bool IsInsideBounds(Vector3 position, float halfExtent)
        {
            if (!_enabled) return true;

            for (int i = 0; i < _segments.Count; i++)
            {
                if (Overlaps(_segments[i], position, halfExtent)) return false;
            }

            return true;
        }

        private static bool Overlaps(Segment s, Vector3 position, float halfExtent)
        {
            if (s.Distance <= 0f) return false;

            Rect f = s.Filled;
            return position.x + halfExtent > f.xMin && position.x - halfExtent < f.xMax &&
                   position.z + halfExtent > f.yMin && position.z - halfExtent < f.yMax;
        }

        /// <summary>
        /// Pushes a racer back out of filled pressure, along the fill axis and against the sweep, so
        /// it is ejected toward the still-open part of the segment rather than sideways into a wall.
        /// </summary>
        public override Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth)
        {
            if (!_enabled) return position;

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment s = _segments[i];
                if (!Overlaps(s, position, halfExtent)) continue;

                Rect f = s.Filled;
                float coordinate = s.Axis == 0 ? position.x : position.z;
                float front = s.Axis == 0
                    ? (s.Sign > 0f ? f.xMax : f.xMin)
                    : (s.Sign > 0f ? f.yMax : f.yMin);

                float corrected = front + s.Sign * (halfExtent + skinWidth);

                // Sideways escape when it is genuinely shorter - a racer clipping the corner of a
                // filled region should not be shot the length of the segment.
                float alongCost = Mathf.Abs(corrected - coordinate);
                float crossCost = CrossEscapeCost(s, position, halfExtent, out float crossValue, out int crossAxis);

                if (crossCost < alongCost)
                {
                    if (crossAxis == 0) position.x = crossValue; else position.z = crossValue;
                }
                else if (s.Axis == 0)
                {
                    position.x = corrected;
                }
                else
                {
                    position.z = corrected;
                }
            }

            return position;
        }

        private static float CrossEscapeCost(Segment s, Vector3 position, float halfExtent,
            out float value, out int axis)
        {
            Rect f = s.Filled;
            axis = s.Axis == 0 ? 1 : 0;

            float coordinate = axis == 0 ? position.x : position.z;
            float lo = axis == 0 ? f.xMin : f.yMin;
            float hi = axis == 0 ? f.xMax : f.yMax;

            float toLow = coordinate - (lo - halfExtent);
            float toHigh = (hi + halfExtent) - coordinate;

            if (toLow < toHigh)
            {
                value = lo - halfExtent - 0.01f;
                return Mathf.Abs(value - coordinate);
            }

            value = hi + halfExtent + 0.01f;
            return Mathf.Abs(value - coordinate);
        }

        public override Vector3 ReflectOffBoundaries(Vector3 position, Vector3 direction,
            float halfExtent, out bool reflected)
        {
            reflected = false;
            if (!_enabled) return direction;

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment s = _segments[i];
                if (s.Distance <= 0f) continue;
                if (!Overlaps(s, position, halfExtent + 0.05f)) continue;

                // The face racers meet is the advancing front, whose normal opposes the sweep.
                Vector3 normal = s.Axis == 0
                    ? new Vector3(-s.Sign, 0f, 0f)
                    : new Vector3(0f, 0f, -s.Sign);

                if (Vector3.Dot(direction, normal) < 0f)
                {
                    direction = PlanarMath.Reflect(direction, normal);
                    reflected = true;
                }
            }

            return direction;
        }

        public override Rect CurrentBounds(Rect arenaRect) => arenaRect;

        public override float Progress
        {
            get
            {
                if (_segments.Count == 0) return 0f;

                float total = 0f, done = 0f;
                for (int i = 0; i < _segments.Count; i++)
                {
                    total += _segments[i].FillLength;
                    done += _segments[i].Distance;
                }

                return total <= 0f ? 0f : done / total;
            }
        }

        public override string Describe()
            => $"track {_segments.Count} segments, at {ActiveSegmentIndex}, {Progress * 100f:F0}% filled";
    }
}
