using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>A sliding slab. The transform IS the body; the system moves it along the travel.</summary>
    [DisallowMultipleComponent]
    public class Crusher : MonoBehaviour
    {
        [Tooltip("Body position at u = 0, in the arena root's space.")]
        [SerializeField] private Vector3 restPosition;

        [Tooltip("Offset the body reaches at u = 1.")]
        [SerializeField] private Vector3 travel;

        [Tooltip("Seconds for one full out-and-back.")]
        [SerializeField] private float period = 4f;

        [SerializeField] private float phase = 0f;

        [Tooltip("0 = pure cosine glide. Higher values snap: a slow pull back, then a fast slam.")]
        [Range(0f, 1f)] [SerializeField] private float slam = 0.6f;

        public Vector3 RestPosition => restPosition;
        public Vector3 Travel => travel;

        /// <summary>0..1 along the travel at run time t. Slam skews the curve toward a fast strike.</summary>
        public float Progress(float t)
        {
            float p = Mathf.Max(0.5f, period);
            float u = Mathf.Repeat((t + phase) / p, 1f);
            // A cosine glide, sharpened so the slab lingers open and strikes quickly.
            float glide = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f);
            float sharp = Mathf.Pow(glide, 1f + slam * 3f);
            return Mathf.Lerp(glide, sharp, slam);
        }

        /// <summary>Direction the body is currently moving: which way a pinned racer gets shoved.</summary>
        public Vector3 TravelDirection => travel.sqrMagnitude > 1e-6f ? travel.normalized : Vector3.zero;

        public void Configure(Vector3 rest, Vector3 travelOffset, float periodSeconds, float phaseOffset)
        {
            restPosition = rest;
            travel = travelOffset;
            period = periodSeconds;
            phase = phaseOffset;
        }
    }
}
