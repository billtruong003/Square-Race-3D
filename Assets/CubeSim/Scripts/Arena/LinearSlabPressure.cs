using System.Collections.Generic;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.Arena
{
    /// <summary>
    /// Straight slabs closing in from the arena edges - the original squeeze from the reference
    /// video. Advancing them is a deterministic function of elapsed run time, so the same config and
    /// seed reproduce the same squeeze.
    ///
    /// It only reports and enforces boundaries; deciding what happens to a racer that cannot fit any
    /// more is the constraint solver's job.
    /// </summary>
    public sealed class LinearSlabPressure : PressureField
    {
        private readonly PressureConfig _config;
        private readonly List<PressureSlab> _slabs = new List<PressureSlab>(4);
        private readonly Transform _root;

        public IReadOnlyList<PressureSlab> Slabs => _slabs;

        public override bool Enabled => _config.enabled;

        public LinearSlabPressure(PressureConfig config, Rect bounds, float groundY,
            MaterialLibrary materials, Transform parent)
        {
            _config = config;

            _root = new GameObject("Pressure").transform;
            _root.SetParent(parent, false);

            if (!config.enabled) return;

            for (int i = 0; i < config.slabs.Count; i++)
            {
                _slabs.Add(new PressureSlab(config.slabs[i], config, bounds, groundY, materials, _root));
            }
        }

        /// <summary>Advance every boundary. Call before racers move so the casts see current geometry.</summary>
        public override void Tick(float elapsedTime)
        {
            if (!_config.enabled) return;

            for (int i = 0; i < _slabs.Count; i++) _slabs[i].Tick(elapsedTime);
        }

        /// <summary>True when a box of this half extent sits on the legal side of every boundary.</summary>
        public override bool IsInsideBounds(Vector3 position, float halfExtent)
        {
            if (!_config.enabled) return true;

            for (int i = 0; i < _slabs.Count; i++)
            {
                if (_slabs[i].Penetration(position, halfExtent) > 0f) return false;
            }

            return true;
        }

        /// <summary>Pushes a position onto the legal side of every boundary.</summary>
        public override Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth)
        {
            if (!_config.enabled) return position;

            for (int i = 0; i < _slabs.Count; i++)
            {
                position = _slabs[i].Clamp(position, halfExtent, skinWidth);
            }

            return position;
        }

        /// <summary>
        /// The rectangle still enclosed by the boundaries, clipped to the arena. Used to validate
        /// weapon drops and to report the shrinking playable area.
        /// </summary>
        public override Rect CurrentBounds(Rect arenaRect)
        {
            if (!_config.enabled) return arenaRect;

            float xMin = arenaRect.xMin, xMax = arenaRect.xMax;
            float zMin = arenaRect.yMin, zMax = arenaRect.yMax;

            for (int i = 0; i < _slabs.Count; i++)
            {
                PressureSlab slab = _slabs[i];
                if (slab.Axis == 0)
                {
                    if (slab.InsideSign > 0f) xMin = Mathf.Max(xMin, slab.Boundary);
                    else xMax = Mathf.Min(xMax, slab.Boundary);
                }
                else
                {
                    if (slab.InsideSign > 0f) zMin = Mathf.Max(zMin, slab.Boundary);
                    else zMax = Mathf.Min(zMax, slab.Boundary);
                }
            }

            return Rect.MinMaxRect(Mathf.Min(xMin, xMax), Mathf.Min(zMin, zMax),
                Mathf.Max(xMin, xMax), Mathf.Max(zMin, zMax));
        }

        /// <summary>Reflects a direction off whichever boundary it is currently pushing into.</summary>
        public override Vector3 ReflectOffBoundaries(Vector3 position, Vector3 direction,
            float halfExtent, out bool reflected)
        {
            reflected = false;
            if (!_config.enabled) return direction;

            for (int i = 0; i < _slabs.Count; i++)
            {
                PressureSlab slab = _slabs[i];
                if (slab.Penetration(position, halfExtent) < -halfExtent) continue;

                Vector3 normal = slab.Normal;
                if (Vector3.Dot(direction, normal) < 0f)
                {
                    direction = PlanarMath.Reflect(direction, normal);
                    reflected = true;
                }
            }

            return direction;
        }
    }
}
