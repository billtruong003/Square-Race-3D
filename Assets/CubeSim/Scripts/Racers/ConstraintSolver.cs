using UnityEngine;
using CubeSim.Arena;
using CubeSim.Core;

namespace CubeSim.Racers
{
    public enum ConstraintOutcome
    {
        /// <summary>Already legal; nothing was moved.</summary>
        Clear = 0,

        /// <summary>Was overlapping, and a legal position was found.</summary>
        Resolved = 1,

        /// <summary>No legal position exists - the racer is pinned between pressure and static geometry.</summary>
        Crushed = 2
    }

    /// <summary>
    /// Guarantees a racer never ends a step inside a static wall.
    ///
    /// A racer has two hard constraints: it must not overlap arena walls, and it must sit inside the
    /// current pressure bounds. Those can genuinely conflict once the pressure has advanced onto a
    /// wall - and when they do, the answer is not to shove the racer through the wall. It is crushed.
    ///
    /// Order per step: depenetrate from walls, clamp to the pressure half spaces, repeat. If that
    /// does not converge, search outward for the nearest legal spot. If none exists, report Crushed.
    /// </summary>
    public struct ConstraintSolver
    {
        private const int RelaxIterations = 4;
        private const int SearchRings = 5;
        private const int SearchDirections = 12;

        private readonly PressureField _pressure;
        private readonly float _skin;
        private readonly float _planeY;
        private PlanarMover _mover;

        public ConstraintSolver(PlanarMover mover, PressureField pressure, float skinWidth, float planeY)
        {
            _mover = mover;
            _pressure = pressure;
            _skin = Mathf.Max(0.001f, skinWidth);
            _planeY = planeY;
        }

        /// <summary>True when the position clears every wall and sits inside the pressure bounds.</summary>
        public bool IsLegal(Vector3 position, float halfExtent)
        {
            // A hair under the true extent, so the skin gap the mover keeps is not read as an overlap.
            float probe = Mathf.Max(0.005f, halfExtent - _skin);
            if (_mover.OverlapsWalls(position, probe)) return false;
            return _pressure.IsInsideBounds(position, halfExtent);
        }

        public ConstraintOutcome Resolve(Racer racer)
        {
            Vector3 position = racer.Position;
            position.y = _planeY + racer.HalfExtent;

            if (IsLegal(position, racer.HalfExtent))
            {
                racer.Position = position;
                return ConstraintOutcome.Clear;
            }

            // Alternate the two constraints; most conflicts settle within a couple of passes.
            for (int i = 0; i < RelaxIterations; i++)
            {
                _mover.Depenetrate(ref position, racer.HalfExtent);
                position = _pressure.Clamp(position, racer.HalfExtent, _skin);
                position.y = _planeY + racer.HalfExtent;

                if (IsLegal(position, racer.HalfExtent))
                {
                    racer.Position = position;
                    ReflectOutOfBoundary(racer);
                    return ConstraintOutcome.Resolved;
                }
            }

            if (TryFindNearestLegal(position, racer.HalfExtent, out Vector3 rescued))
            {
                rescued.y = _planeY + racer.HalfExtent;
                racer.Position = rescued;
                ReflectOutOfBoundary(racer);
                return ConstraintOutcome.Resolved;
            }

            // Pinned: pressure on one side, static geometry on the other, nowhere legal within reach.
            racer.Position = position;
            return ConstraintOutcome.Crushed;
        }

        /// <summary>Widening ring search for a legal spot near the conflicted position.</summary>
        private bool TryFindNearestLegal(Vector3 origin, float halfExtent, out Vector3 result)
        {
            float stepSize = Mathf.Max(0.12f, halfExtent * 0.5f);

            for (int ring = 1; ring <= SearchRings; ring++)
            {
                float radius = ring * stepSize;

                for (int i = 0; i < SearchDirections; i++)
                {
                    float angle = i * (Mathf.PI * 2f / SearchDirections);
                    var candidate = new Vector3(
                        origin.x + Mathf.Cos(angle) * radius,
                        origin.y,
                        origin.z + Mathf.Sin(angle) * radius);

                    if (IsLegal(candidate, halfExtent))
                    {
                        result = candidate;
                        return true;
                    }
                }
            }

            result = origin;
            return false;
        }

        /// <summary>Turns a racer away from a boundary it was just pushed off, so it does not re-enter.</summary>
        private void ReflectOutOfBoundary(Racer racer)
        {
            Vector3 direction = _pressure.ReflectOffBoundaries(
                racer.Position, racer.Direction, racer.HalfExtent, out bool reflected);

            if (!reflected) return;

            racer.Direction = direction;
            racer.BounceCount++;
        }
    }
}
