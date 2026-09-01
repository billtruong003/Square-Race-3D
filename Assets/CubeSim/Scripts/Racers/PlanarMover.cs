using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Racers
{
    /// <summary>
    /// Custom kinematic mover: transform movement, cast-based collision detection, manual billiard
    /// reflection. No Rigidbody, no forces, no physics materials, no Unity collision response.
    ///
    /// One step consumes a fixed travel budget (speed * dt). Every impact eats part of the budget,
    /// reflects the direction, and the remainder is spent along the new direction inside the same
    /// step - so the configured speed is preserved exactly, including through corners.
    ///
    /// Casts run against walls and pressure slabs alike. Overlap recovery only ever considers static
    /// walls: pressure boundaries are half spaces the constraint solver handles analytically.
    /// </summary>
    public struct PlanarMover
    {
        private readonly int _castMask;
        private readonly int _wallMask;
        private readonly float _skin;
        private readonly int _maxIterations;
        private readonly float _planeY;

        // Shared scratch buffer: overlap recovery is rare and the simulation is single threaded, so
        // one static buffer keeps the movement loop allocation free.
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        public PlanarMover(int castMask, int wallMask, float skinWidth, int maxIterations, float planeY)
        {
            _castMask = castMask;
            _wallMask = wallMask;
            _skin = Mathf.Max(0.001f, skinWidth);
            _maxIterations = Mathf.Max(1, maxIterations);
            _planeY = planeY;
        }

        /// <summary>
        /// Anything that wants to know about real wall impacts - currently the breakable walls.
        /// Reported from the cast itself, so it is a genuine contact rather than proximity or a
        /// per-frame overlap.
        /// </summary>
        public interface IContactListener
        {
            void ReportContact(Racer racer, Collider collider);
        }

        /// <summary>Advances a racer by one simulation step, resolving every impact along the way.</summary>
        public void Step(Racer racer, float deltaTime) => Step(racer, deltaTime, null);

        public void Step(Racer racer, float deltaTime, IContactListener contacts)
        {
            float distance = PlanarMath.StepDistance(racer.Speed, deltaTime);
            if (distance <= PlanarMath.Epsilon) return;

            if (!PlanarMath.TryNormalizePlanar(racer.Direction, out Vector3 direction))
            {
                // A racer with no direction would sit still forever; nudge it back onto the plane.
                direction = Vector3.forward;
            }

            Vector3 position = racer.Position;
            position.y = _planeY + racer.HalfExtent;

            float halfCast = Mathf.Max(0.001f, racer.HalfExtent - _skin);
            Vector3 halfExtents = new Vector3(halfCast, halfCast, halfCast);

            float remaining = distance;
            int iterations = 0;

            while (remaining > PlanarMath.Epsilon && iterations < _maxIterations)
            {
                iterations++;

                if (!Physics.BoxCast(position, halfExtents, direction, out RaycastHit hit,
                        Quaternion.identity, remaining + _skin, _castMask, QueryTriggerInteraction.Ignore))
                {
                    position += direction * remaining;
                    racer.DistanceTravelled += remaining;
                    remaining = 0f;
                    break;
                }

                contacts?.ReportContact(racer, hit.collider);

                float advance = Mathf.Max(0f, hit.distance - _skin);
                if (advance > 0f)
                {
                    position += direction * advance;
                    racer.DistanceTravelled += advance;
                    remaining = PlanarMath.ConsumeDistance(remaining, advance);
                }

                bool reflected = false;
                if (PlanarMath.TryNormalizePlanar(hit.normal, out Vector3 normal) &&
                    Vector3.Dot(direction, normal) < -PlanarMath.Epsilon)
                {
                    direction = PlanarMath.Reflect(direction, normal);
                    // Step off the surface so the next cast does not start flush against it, which is
                    // what produces jitter when a racer skims a wall at a shallow angle.
                    position += normal * _skin;
                    racer.BounceCount++;
                    reflected = true;
                }

                if (advance <= PlanarMath.Epsilon && !reflected)
                {
                    // Started inside geometry, or hit a surface whose normal does not oppose us
                    // (a floor/ceiling face). Push out analytically and stop this step.
                    Depenetrate(ref position, racer.HalfExtent);
                    break;
                }
            }

            position.y = _planeY + racer.HalfExtent;
            racer.Position = position;
            racer.Direction = direction;
        }

        /// <summary>True when a box of this half extent overlaps any static wall.</summary>
        public bool OverlapsWalls(Vector3 position, float halfExtent)
        {
            int count = Physics.OverlapBoxNonAlloc(position, new Vector3(halfExtent, halfExtent, halfExtent),
                OverlapBuffer, Quaternion.identity, _wallMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count && i < OverlapBuffer.Length; i++) OverlapBuffer[i] = null;
            return count > 0;
        }

        /// <summary>
        /// Escape overlapping wall geometry. Every wall is an axis-aligned box, so the minimum
        /// translation vector comes straight from the bounds - no ComputePenetration and no dummy
        /// collider needed.
        /// </summary>
        public bool Depenetrate(ref Vector3 position, float halfExtent)
        {
            int count = Physics.OverlapBoxNonAlloc(position, new Vector3(halfExtent, halfExtent, halfExtent),
                OverlapBuffer, Quaternion.identity, _wallMask, QueryTriggerInteraction.Ignore);
            count = Mathf.Min(count, OverlapBuffer.Length);

            bool moved = false;
            for (int i = 0; i < count; i++)
            {
                Bounds b = OverlapBuffer[i].bounds;

                float dx = position.x - b.center.x;
                float dz = position.z - b.center.z;
                float overlapX = halfExtent + b.extents.x - Mathf.Abs(dx);
                float overlapZ = halfExtent + b.extents.z - Mathf.Abs(dz);
                if (overlapX <= 0f || overlapZ <= 0f) continue;

                if (overlapX < overlapZ)
                {
                    position.x += (dx >= 0f ? 1f : -1f) * (overlapX + _skin);
                }
                else
                {
                    position.z += (dz >= 0f ? 1f : -1f) * (overlapZ + _skin);
                }

                moved = true;
            }

            // Release the references so the shared buffer does not keep destroyed colliders alive.
            for (int i = 0; i < count; i++) OverlapBuffer[i] = null;

            return moved;
        }
    }
}
