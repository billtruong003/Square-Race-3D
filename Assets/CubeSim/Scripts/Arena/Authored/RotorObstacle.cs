using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// The spinning cross: two bar colliders under this transform, rotated about the centre by the
    /// obstacle system as a pure function of elapsed time. Slow by design - the racers' analytic
    /// wall recovery assumes near-axis-aligned boxes, and a slow rotor keeps its sweep gentle
    /// enough for the constraint solver to shepherd racers out of the way.
    /// </summary>
    [DisallowMultipleComponent]
    public class RotorObstacle : MonoBehaviour
    {
        [Tooltip("Degrees per second. Positive is clockwise seen from the camera.")]
        [SerializeField] private float degreesPerSecond = 24f;

        [Tooltip("Offset into the spin, so mirrored rotors do not move in lockstep.")]
        [SerializeField] private float phaseDegrees = 0f;

        public float DegreesPerSecond => degreesPerSecond;
        public float PhaseDegrees => phaseDegrees;

        public void Configure(float speed, float phase)
        {
            degreesPerSecond = speed;
            phaseDegrees = phase;
        }
    }
}
