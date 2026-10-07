using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Arena
{
    /// <summary>
    /// A closing zone measured in walking distance instead of straight lines.
    ///
    /// Every open floor cell gets one close time, computed once when the round is built:
    ///  - PathChase (race): far end of the course first, the goal last and never.
    ///  - Circle (battle): rings around a seeded open-floor centre, shrinking in phases, each ring
    ///    announced for the whole hold before it goes.
    ///  - Collapse (battle): tiles fall away in waves toward a final platform, with a seeded ragged edge.
    ///
    /// Because the distance is walked, not measured as the crow flies, the zone follows corridors,
    /// never seals a racer in a pocket behind a wall, and never lands its last safe area on a wall.
    /// Closed cells act like a wall the cubes bounce off; a cube standing on a cell as it closes is
    /// swallowed when the zone is lethal. A pure function of elapsed time: same seed, same squeeze.
    /// </summary>
    public sealed class GridZonePressure : PressureField
    {
        private readonly ZoneConfig _config;
        private readonly PathField _grid;
        private readonly PathField _goalField;
        private readonly float[] _closeAt;
        private readonly float[] _warnFrom;
        private readonly int[] _order;           // open cells sorted by close time
        private readonly int[] _warnOrder;       // open cells sorted by warn start
        private readonly bool _enabled;
        private readonly Rect _rect;

        private int _closedCursor;
        private int _warnCursor;
        private float _now;
        private bool _boundsDirty = true;
        private Rect _openBounds;
        private readonly float _lastClose;

        private readonly Texture2D _mask;
        private readonly Color32[] _pixels;
        private readonly Material _material;
        private readonly Transform _root;
        private bool _maskDirty = true;

        public override bool Enabled => _enabled;
        public override PathField GoalField => _goalField;

        public Vector3 Centre { get; }

        public GridZonePressure(PressureConfig config, ArenaRuntime arena, float racerHalfExtent,
            SimulationRandom random, Transform parent)
        {
            _config = config.zone ?? new ZoneConfig();
            _rect = arena.PlayableRect;
            _root = new GameObject("Zone").transform;
            _root.SetParent(parent, false);

            _grid = PathField.Build(arena, racerHalfExtent, _config.cellSize);
            int n = _grid.Columns * _grid.Rows;
            _closeAt = new float[n];
            _warnFrom = new float[n];
            for (int i = 0; i < n; i++) { _closeAt[i] = float.PositiveInfinity; _warnFrom[i] = float.PositiveInfinity; }

            bool ok;
            switch (_config.shape)
            {
                case ZoneShape.PathChase:
                    ok = BuildChase(arena);
                    if (ok) _goalField = _grid;
                    Centre = arena.Center;
                    break;
                case ZoneShape.Circle:
                    ok = BuildCircle(random, out Vector3 circleCentre);
                    Centre = circleCentre;
                    break;
                default:
                    ok = BuildCollapse(random, out Vector3 pitCentre);
                    Centre = pitCentre;
                    break;
            }

            _enabled = config.enabled && ok;
            _arena = arena;
            _racerHalfExtent = racerHalfExtent;
            // Only fights hold: a race pen opens on its own (doors), and racers dig through rock.
            _holding = _enabled && _config.shape != ZoneShape.PathChase;

            var open = new List<int>(_grid.OpenCount);
            for (int i = 0; i < n; i++) if (_grid.IsOpen(i) && !float.IsPositiveInfinity(_closeAt[i])) open.Add(i);
            _order = open.ToArray();
            Array.Sort(_order, (a, b) => _closeAt[a].CompareTo(_closeAt[b]));
            _warnOrder = open.ToArray();
            Array.Sort(_warnOrder, (a, b) => _warnFrom[a].CompareTo(_warnFrom[b]));
            _lastClose = _order.Length > 0 ? _closeAt[_order[_order.Length - 1]] : 0f;

            if (!_enabled) return;

            _pixels = new Color32[n];
            _mask = new Texture2D(_grid.Columns, _grid.Rows, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ZoneMask"
            };

            Shader shader = Shader.Find("CubeSim/ZoneOverlay");
            if (shader != null)
            {
                _material = new Material(shader) { name = "ZoneOverlay" };
                _material.SetTexture("_Mask", _mask);
                _material.SetVector("_MaskRect", new Vector4(_grid.Origin.x, _grid.Origin.y,
                    _grid.Columns * _grid.CellSize, _grid.Rows * _grid.CellSize));
                _material.SetFloat("_Style", _config.shape == ZoneShape.Collapse ? 1f : 0f);

                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "ZoneOverlay";
                UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(_root, false);
                quad.transform.position = new Vector3(_rect.center.x, arena.GroundY + 0.03f, _rect.center.y);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(_rect.width, _rect.height, 1f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            else
            {
                Debug.LogWarning("[CubeSim] Shader CubeSim/ZoneOverlay not found; the zone runs invisibly.");
            }

            Tick(0f);
        }

        // ---------------------------------------------------------------- schedules

        private bool BuildChase(ArenaRuntime arena)
        {
            var goals = new List<Rect>();
            if (arena.GoalAreas != null)
            {
                for (int i = 0; i < arena.GoalAreas.Count; i++)
                {
                    if (arena.GoalAreas[i] != null) goals.Add(arena.GoalAreas[i].Footprint);
                }
            }

            if (goals.Count == 0 || !_grid.ComputeFrom(goals))
            {
                Debug.LogWarning("[CubeSim] PathChase zone needs a reachable goal; zone disabled.");
                return false;
            }

            float[] d = _grid.Distances;
            float max = _grid.MaxDistance;
            float speed = SpeedFor(max - _config.keepNearGoal);
            for (int i = 0; i < d.Length; i++)
            {
                if (!_grid.IsOpen(i) || d[i] >= PathField.Unreachable || d[i] <= _config.keepNearGoal) continue;
                _closeAt[i] = _config.startDelay + (max - d[i]) / speed;
                _warnFrom[i] = _closeAt[i] - _config.warnSeconds;
            }

            return true;
        }

        private bool BuildCircle(SimulationRandom random, out Vector3 centre)
        {
            if (!PickCentre(random, out centre, out float[] d, out float max)) return false;

            int phases = Mathf.Max(1, _config.phases);
            float final = Mathf.Min(_config.finalRadius, max);
            float hold = Mathf.Max(0f, _config.holdSeconds);
            float shrink = Mathf.Max(0.1f, _config.shrinkSeconds);

            for (int i = 0; i < d.Length; i++)
            {
                if (!_grid.IsOpen(i) || d[i] >= PathField.Unreachable || d[i] <= final) continue;

                // Radii fall linearly from the full reach to the final ring; find the phase whose
                // shrink sweeps this cell.
                for (int p = 1; p <= phases; p++)
                {
                    float rPrev = Mathf.Lerp(max, final, (p - 1) / (float)phases);
                    float rNext = Mathf.Lerp(max, final, p / (float)phases);
                    if (d[i] <= rNext) continue;

                    float phaseStart = _config.startDelay + (p - 1) * (hold + shrink);
                    float shrinkStart = phaseStart + hold;
                    float t = (rPrev - d[i]) / Mathf.Max(0.001f, rPrev - rNext);
                    _closeAt[i] = shrinkStart + Mathf.Clamp01(t) * shrink;
                    // The whole doomed ring is announced while the phase holds.
                    _warnFrom[i] = phaseStart;
                    break;
                }
            }

            return true;
        }

        private bool BuildCollapse(SimulationRandom random, out Vector3 centre)
        {
            if (!PickCentre(random, out centre, out float[] d, out float max)) return false;

            float tile = Mathf.Max(_grid.CellSize, _config.tileSize);
            int tileCols = Mathf.CeilToInt(_grid.Columns * _grid.CellSize / tile);
            int tileRows = Mathf.CeilToInt(_grid.Rows * _grid.CellSize / tile);
            var tileDist = new float[tileCols * tileRows];
            for (int i = 0; i < tileDist.Length; i++) tileDist[i] = float.MaxValue;

            for (int i = 0; i < d.Length; i++)
            {
                if (!_grid.IsOpen(i) || d[i] >= PathField.Unreachable) continue;
                int t = TileOf(i, tile, tileCols);
                if (d[i] < tileDist[t]) tileDist[t] = d[i];
            }

            var jitter = new float[tileDist.Length];
            for (int i = 0; i < jitter.Length; i++) jitter[i] = random.NextFloat() * _config.tileJitter;

            float speed = SpeedFor(max - _config.finalRadius);
            for (int i = 0; i < d.Length; i++)
            {
                if (!_grid.IsOpen(i) || d[i] >= PathField.Unreachable) continue;
                int t = TileOf(i, tile, tileCols);
                // Whole tiles drop together, so the floor reads as panels falling, not sand.
                if (tileDist[t] <= _config.finalRadius) continue;
                _closeAt[i] = _config.startDelay + (max - tileDist[t]) / speed + jitter[t];
                _warnFrom[i] = _closeAt[i] - _config.warnSeconds;
            }

            return true;
        }

        private float SpeedFor(float distanceToClose) =>
            _config.closeSeconds > 0f
                ? Mathf.Max(0.05f, distanceToClose / _config.closeSeconds)
                : Mathf.Max(0.05f, _config.speed);

        private int TileOf(int cell, float tile, int tileCols)
        {
            int c = cell % _grid.Columns, r = cell / _grid.Columns;
            int tc = Mathf.FloorToInt(c * _grid.CellSize / tile);
            int tr = Mathf.FloorToInt(r * _grid.CellSize / tile);
            return tr * tileCols + tc;
        }

        /// <summary>
        /// The battle centre: among a seeded handful of open cells plus the one nearest the middle,
        /// the cell with the shortest walk to the farthest floor - so nobody spawns hopelessly far
        /// from the final ring, and the centre is always floor a cube can stand on.
        /// </summary>
        private bool PickCentre(SimulationRandom random, out Vector3 centre, out float[] distances, out float max)
        {
            centre = Vector3.zero;
            distances = null;
            max = 0f;
            if (_grid.OpenCount == 0) return false;

            var candidates = new List<int>();
            int middle = _grid.NearestOpen(new Vector3(_rect.center.x, 0f, _rect.center.y));
            if (middle >= 0) candidates.Add(middle);

            int n = _grid.Columns * _grid.Rows;
            for (int k = 0, guard = 0; k < 10 && guard < 4000; guard++)
            {
                int i = random.Range(0, n);
                if (!_grid.IsOpen(i)) continue;
                candidates.Add(i);
                k++;
            }

            float bestMax = float.MaxValue;
            for (int k = 0; k < candidates.Count; k++)
            {
                int i = candidates[k];
                Vector3 p = _grid.CellCentre(i % _grid.Columns, i / _grid.Columns, 0f);
                if (!_grid.ComputeFrom(p)) continue;
                if (_grid.MaxDistance >= bestMax) continue;
                bestMax = _grid.MaxDistance;
                distances = _grid.Snapshot();
                centre = p;
            }

            if (distances == null) return false;
            max = bestMax;
            return true;
        }

        // ---------------------------------------------------------------- runtime

        // ---- hold while sealed: a battle zone must not start eating a pen nobody can leave yet
        private ArenaRuntime _arena;
        private float _racerHalfExtent;
        private bool _holding;
        private float _held;
        private int _brokenSeen = -1;
        private PathField _reach;
        private const float MaxHoldSeconds = 60f;

        /// <summary>True while the zone clock is paused for sealed pens.</summary>
        public bool Holding => _holding;

        /// <summary>
        /// Pauses the zone clock while any cube still stands in floor it cannot walk out of (a team
        /// pen behind an intact wall). Re-checked whenever a breakable falls; released for good once
        /// every cube can reach the centre, or after a minute regardless.
        /// </summary>
        public void UpdateHold(Racers.Racer[] racers, int brokenWalls, float deltaTime)
        {
            if (!_holding) return;

            if (brokenWalls != _brokenSeen || _reach == null)
            {
                _brokenSeen = brokenWalls;
                Physics.SyncTransforms();
                _reach = PathField.Build(_arena, _racerHalfExtent, _config.cellSize, false);
                _reach.ComputeFrom(Centre);
            }

            bool sealedCube = false;
            for (int i = 0; i < racers.Length && !sealedCube; i++)
            {
                if (racers[i].IsActive && _reach.Sample(racers[i].Position) >= PathField.Unreachable * 0.5f) sealedCube = true;
            }

            if (!sealedCube || _held >= MaxHoldSeconds)
            {
                _holding = false;
                return;
            }

            _held += deltaTime;
        }

        public override void Tick(float elapsedTime)
        {
            if (!_enabled) return;
            _now = elapsedTime - _held;

            while (_closedCursor < _order.Length && _closeAt[_order[_closedCursor]] <= _now)
            {
                int i = _order[_closedCursor++];
                _pixels[i].r = 255;
                _pixels[i].g = 0;
                _maskDirty = true;
                _boundsDirty = true;
            }

            while (_warnCursor < _warnOrder.Length && _warnFrom[_warnOrder[_warnCursor]] <= _now)
            {
                int i = _warnOrder[_warnCursor++];
                if (_pixels[i].r == 0) { _pixels[i].g = 255; _maskDirty = true; }
            }

            if (_material != null)
            {
                _material.SetFloat("_Pulse", 0.5f + 0.5f * Mathf.Sin(_now * 6f));
                if (_maskDirty)
                {
                    _mask.SetPixels32(_pixels);
                    _mask.Apply(false);
                    _maskDirty = false;
                }
            }
        }

        private bool IsClosed(int i) => i >= 0 && _closeAt[i] <= _now;

        public override bool Engulfs(Vector3 position) => _enabled && _config.lethal && IsClosed(_grid.IndexOf(position));

        public override bool IsInsideBounds(Vector3 position, float halfExtent)
        {
            if (!_enabled) return true;
            ForEachCell(position, halfExtent, out bool anyClosed, out _);
            return !anyClosed;
        }

        public override Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth)
        {
            if (!_enabled) return position;

            // Box-vs-cell depenetration, a few passes: each closed cell under the box pushes it out
            // along its shallower axis, exactly like an axis-aligned wall would.
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                int c0 = Col(position.x - halfExtent), c1 = Col(position.x + halfExtent);
                int r0 = Row(position.z - halfExtent), r1 = Row(position.z + halfExtent);
                for (int r = r0; r <= r1 && !moved; r++)
                {
                    for (int c = c0; c <= c1 && !moved; c++)
                    {
                        int i = r * _grid.Columns + c;
                        if (!IsClosed(i)) continue;

                        Vector3 cc = _grid.CellCentre(c, r, position.y);
                        float h = _grid.CellSize * 0.5f;
                        float dx = position.x - cc.x, dz = position.z - cc.z;
                        float ox = halfExtent + h - Mathf.Abs(dx);
                        float oz = halfExtent + h - Mathf.Abs(dz);
                        if (ox <= 0f || oz <= 0f) continue;

                        if (ox < oz) position.x += (dx >= 0f ? 1f : -1f) * (ox + skinWidth);
                        else position.z += (dz >= 0f ? 1f : -1f) * (oz + skinWidth);
                        moved = true;
                    }
                }

                if (!moved) break;
            }

            return position;
        }

        public override Vector3 ReflectOffBoundaries(Vector3 position, Vector3 direction,
            float halfExtent, out bool reflected)
        {
            reflected = false;
            if (!_enabled) return direction;

            float probe = halfExtent + _grid.CellSize * 0.5f;
            if (Mathf.Abs(direction.x) > 1e-4f &&
                CellClosedAt(position + new Vector3(Mathf.Sign(direction.x) * probe, 0f, 0f)))
            {
                direction.x = -direction.x;
                reflected = true;
            }

            if (Mathf.Abs(direction.z) > 1e-4f &&
                CellClosedAt(position + new Vector3(0f, 0f, Mathf.Sign(direction.z) * probe)))
            {
                direction.z = -direction.z;
                reflected = true;
            }

            return direction;
        }

        public override Rect CurrentBounds(Rect arenaRect)
        {
            if (!_enabled) return arenaRect;
            if (!_boundsDirty) return _openBounds;

            float xMin = float.MaxValue, zMin = float.MaxValue, xMax = float.MinValue, zMax = float.MinValue;
            for (int i = 0; i < _closeAt.Length; i++)
            {
                if (!_grid.IsOpen(i) || IsClosed(i)) continue;
                Vector3 c = _grid.CellCentre(i % _grid.Columns, i / _grid.Columns, 0f);
                xMin = Mathf.Min(xMin, c.x); xMax = Mathf.Max(xMax, c.x);
                zMin = Mathf.Min(zMin, c.z); zMax = Mathf.Max(zMax, c.z);
            }

            _openBounds = xMin > xMax ? arenaRect : Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            _boundsDirty = false;
            return _openBounds;
        }

        public override float Progress => _order.Length == 0 ? 0f : _closedCursor / (float)_order.Length;

        public override string Describe() =>
            $"zone {_config.shape} {_closedCursor}/{_order.Length} cells closed, last at {_lastClose:F0}s";

        private bool CellClosedAt(Vector3 p) => IsClosed(_grid.IndexOf(p));

        private void ForEachCell(Vector3 position, float halfExtent, out bool anyClosed, out int count)
        {
            anyClosed = false;
            count = 0;
            int c0 = Col(position.x - halfExtent + 0.01f), c1 = Col(position.x + halfExtent - 0.01f);
            int r0 = Row(position.z - halfExtent + 0.01f), r1 = Row(position.z + halfExtent - 0.01f);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    count++;
                    if (IsClosed(r * _grid.Columns + c)) { anyClosed = true; return; }
                }
            }
        }

        private int Col(float x) => Mathf.Clamp(Mathf.FloorToInt((x - _grid.Origin.x) / _grid.CellSize), 0, _grid.Columns - 1);
        private int Row(float z) => Mathf.Clamp(Mathf.FloorToInt((z - _grid.Origin.y) / _grid.CellSize), 0, _grid.Rows - 1);
    }
}
