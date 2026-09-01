using UnityEngine;

namespace CubeSim.Core
{
    /// <summary>
    /// The single owned random source for a simulation run. xorshift128 so the stream is
    /// reproducible across platforms and completely independent of UnityEngine.Random.
    /// </summary>
    public sealed class SimulationRandom
    {
        private uint _x, _y, _z, _w;

        public int Seed { get; }

        public SimulationRandom(int seed)
        {
            Seed = seed;

            // splitmix-style scramble so neighbouring seeds produce unrelated streams.
            uint s = (uint)seed;
            _x = Scramble(ref s);
            _y = Scramble(ref s);
            _z = Scramble(ref s);
            _w = Scramble(ref s);

            if ((_x | _y | _z | _w) == 0u) _x = 0x9E3779B9u;
        }

        private static uint Scramble(ref uint state)
        {
            state += 0x9E3779B9u;
            uint z = state;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }

        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive)
            => minInclusive + (maxExclusive - minInclusive) * NextFloat();

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public bool NextBool() => (NextUInt() & 1u) != 0u;

        /// <summary>Unit direction on the XZ plane.</summary>
        public Vector3 NextPlanarDirection()
        {
            float angle = NextFloat() * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        /// <summary>
        /// Unit XZ direction that is never closer than <paramref name="minAxisAngle"/> degrees to an
        /// axis, so racers do not start in a degenerate straight-line corridor bounce.
        /// </summary>
        public Vector3 NextPlanarDirectionBiased(float minAxisAngle)
        {
            float quadrant = Range(0, 4) * 90f;
            float offset = Range(minAxisAngle, 90f - minAxisAngle);
            return PlanarMath.DirectionFromAngle(quadrant + offset);
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
