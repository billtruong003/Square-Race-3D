using UnityEngine;

namespace CubeSim.Core
{
    /// <summary>
    /// Pure XZ-plane movement math. No Unity scene state, no side effects - this is the part that is
    /// unit tested.
    /// </summary>
    public static class PlanarMath
    {
        public const float Epsilon = 1e-5f;

        /// <summary>Drops the Y component.</summary>
        public static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>
        /// Flattens onto XZ and normalizes. Returns false when the input has no meaningful planar
        /// component (e.g. a floor/ceiling normal), in which case <paramref name="result"/> is zero.
        /// </summary>
        public static bool TryNormalizePlanar(Vector3 v, out Vector3 result)
        {
            float x = v.x;
            float z = v.z;
            float sqr = x * x + z * z;
            if (sqr < Epsilon * Epsilon)
            {
                result = Vector3.zero;
                return false;
            }

            float inv = 1f / Mathf.Sqrt(sqr);
            result = new Vector3(x * inv, 0f, z * inv);
            return true;
        }

        /// <summary>
        /// Billiard reflection constrained to XZ. Both arguments are flattened first, and the result
        /// is always unit length so the configured speed is preserved exactly.
        /// </summary>
        public static Vector3 Reflect(Vector3 direction, Vector3 normal)
        {
            if (!TryNormalizePlanar(normal, out Vector3 n)) return Flatten(direction);
            if (!TryNormalizePlanar(direction, out Vector3 d)) return Flatten(direction);

            Vector3 reflected = d - 2f * Vector3.Dot(d, n) * n;
            return TryNormalizePlanar(reflected, out Vector3 result) ? result : d;
        }

        /// <summary>Degrees measured clockwise from +Z, matching Unity's Y euler convention.</summary>
        public static Vector3 DirectionFromAngle(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        public static float AngleFromDirection(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            return angle < 0f ? angle + 360f : angle;
        }

        /// <summary>Distance travelled by a constant-speed mover over one step.</summary>
        public static float StepDistance(float speed, float deltaTime)
            => Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime);

        /// <summary>
        /// Distance still owed after advancing <paramref name="advanced"/> of
        /// <paramref name="remaining"/>. Never negative, so a collision loop always terminates.
        /// </summary>
        public static float ConsumeDistance(float remaining, float advanced)
            => Mathf.Max(0f, remaining - Mathf.Max(0f, advanced));

        /// <summary>Signed overlap of a racer half-extent against an axis-aligned half space.</summary>
        public static float HalfSpacePenetration(float position, float halfExtent, float boundary, float insideSign)
        {
            // insideSign = +1 when the valid region is boundary..+inf, -1 when it is -inf..boundary.
            return insideSign > 0f
                ? boundary - (position - halfExtent)
                : (position + halfExtent) - boundary;
        }
    }
}
