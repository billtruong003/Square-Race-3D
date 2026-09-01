using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Racers
{
    /// <summary>
    /// Racer-vs-racer contact math, in the XZ plane.
    ///
    /// Racers collide as discs, not as boxes. Walls stay axis-aligned boxes because the arena is
    /// built from them, but a box-vs-box response can only ever push along X or Z - every racer
    /// bounce would snap to an axis, which is exactly the "just reverse it" look the design rules out.
    /// A disc gives a real contact normal straight from the centre line, so an angled hit deflects at
    /// an angle, consistent with how a racer bounces off a wall.
    ///
    /// The radius used is the racer's half extent - the box inradius. The circumradius would make
    /// racers collide on empty corners; the inradius makes them touch when their faces do.
    ///
    /// Pure math, no Unity scene state and no side effects, so every case here is unit tested.
    /// </summary>
    public static class RacerContactMath
    {
        /// <summary>
        /// Discrete overlap between two racers at their current positions.
        /// <paramref name="normal"/> points from A to B and is always unit length.
        /// </summary>
        public static bool TryOverlap(Vector3 a, Vector3 b, float contactDistance,
            out Vector3 normal, out float penetration)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float sqr = dx * dx + dz * dz;

            if (sqr >= contactDistance * contactDistance)
            {
                normal = Vector3.zero;
                penetration = 0f;
                return false;
            }

            if (sqr < PlanarMath.Epsilon * PlanarMath.Epsilon)
            {
                // Exactly co-located. Any direction is as good as any other, but it has to be the
                // same one every run, so pick the axis rather than something derived from noise.
                normal = Vector3.right;
                penetration = contactDistance;
                return true;
            }

            float distance = Mathf.Sqrt(sqr);
            normal = new Vector3(dx / distance, 0f, dz / distance);
            penetration = contactDistance - distance;
            return true;
        }

        /// <summary>
        /// Swept contact over one step: A travels a0 to a1 while B travels b0 to b1.
        /// Returns the fraction of the step at which the two first touch.
        ///
        /// Without this, two racers closing faster than their combined diameter per step swap sides
        /// without ever registering an overlap - they pass straight through each other, and no amount
        /// of end-of-step testing can see it happened.
        /// </summary>
        public static bool TrySweep(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1,
            float contactDistance, out float timeOfImpact)
        {
            // Relative motion of B as seen from A: one moving point against a stationary disc.
            float dx = b0.x - a0.x;
            float dz = b0.z - a0.z;
            float vx = (b1.x - b0.x) - (a1.x - a0.x);
            float vz = (b1.z - b0.z) - (a1.z - a0.z);

            float c = dx * dx + dz * dz - contactDistance * contactDistance;
            if (c <= 0f)
            {
                // Already touching at the start of the step.
                timeOfImpact = 0f;
                return true;
            }

            float a = vx * vx + vz * vz;
            if (a < PlanarMath.Epsilon * PlanarMath.Epsilon)
            {
                // No relative motion, and they did not start in contact.
                timeOfImpact = 0f;
                return false;
            }

            float b = 2f * (dx * vx + dz * vz);
            if (b >= 0f)
            {
                // Separating, so the gap only ever grows over this step.
                timeOfImpact = 0f;
                return false;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                timeOfImpact = 0f;
                return false;
            }

            float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (t < 0f || t > 1f)
            {
                timeOfImpact = 0f;
                return false;
            }

            timeOfImpact = t;
            return true;
        }

        /// <summary>Position of a racer partway through its step.</summary>
        public static Vector3 Lerp(Vector3 from, Vector3 to, float t)
            => new Vector3(
                from.x + (to.x - from.x) * t,
                from.y,
                from.z + (to.z - from.z) * t);

        /// <summary>
        /// Billiard response. Each direction is reflected about the contact plane, and only when that
        /// racer is actually closing on the other - reflecting a racer that is already moving away
        /// would turn it straight back into the contact and the pair would buzz against each other.
        ///
        /// Directions stay unit length, which is what keeps the configured speed exact.
        ///
        /// <paramref name="normal"/> must point from A to B, as <see cref="TryOverlap"/> returns it.
        /// Reflection itself is sign-indifferent, but the approach test is not: hand it a flipped
        /// normal and both racers read as separating, so neither turns and the pair slides through.
        /// </summary>
        public static void Respond(ref Vector3 directionA, ref Vector3 directionB, Vector3 normal,
            out bool changedA, out bool changedB)
        {
            changedA = false;
            changedB = false;

            if (!PlanarMath.TryNormalizePlanar(normal, out Vector3 n)) return;

            // Normal points A -> B, so A closes when it moves along it and B when it moves against it.
            if (Vector3.Dot(directionA, n) > PlanarMath.Epsilon)
            {
                directionA = PlanarMath.Reflect(directionA, n);
                changedA = true;
            }

            if (Vector3.Dot(directionB, n) < -PlanarMath.Epsilon)
            {
                directionB = PlanarMath.Reflect(directionB, n);
                changedB = true;
            }
        }

        /// <summary>
        /// Splits a separation between two racers. Normally each takes half; when one of them cannot
        /// legally take its half - a wall or the pressure is behind it - the whole correction goes to
        /// the other. Solving an overlap by pushing a racer into a wall just moves the problem.
        /// </summary>
        public static void SplitCorrection(float total, bool canMoveA, bool canMoveB,
            out float shareA, out float shareB)
        {
            total = Mathf.Max(0f, total);

            if (canMoveA && canMoveB)
            {
                shareA = total * 0.5f;
                shareB = total * 0.5f;
                return;
            }

            if (canMoveA) { shareA = total; shareB = 0f; return; }
            if (canMoveB) { shareA = 0f; shareB = total; return; }

            // Neither has anywhere to go. Leave them where they are and let the constraint solver
            // decide whether this is a crush.
            shareA = 0f;
            shareB = 0f;
        }
    }
}
