using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// A wall that opens and closes on a fixed clock - the timing door of the reference videos.
    /// Data only: <see cref="Core.MovingObstacleSystem"/> drives the motion off elapsed run time,
    /// so the cycle is identical every run of a seed.
    /// </summary>
    [RequireComponent(typeof(ArenaWall))]
    [DisallowMultipleComponent]
    public class CyclingWall : MonoBehaviour
    {
        [Tooltip("Seconds the passage stays open.")]
        [SerializeField] private float openDuration = 3f;

        [Tooltip("Seconds the wall stays closed.")]
        [SerializeField] private float closedDuration = 3f;

        [Tooltip("Seconds the slide between states takes.")]
        [SerializeField] private float slideDuration = 0.4f;

        [Tooltip("Offset into the cycle, so a row of doors breathes in sequence instead of in step.")]
        [SerializeField] private float phaseOffset = 0f;

        public float OpenDuration => Mathf.Max(0.5f, openDuration);
        public float ClosedDuration => Mathf.Max(0.5f, closedDuration);
        public float SlideDuration => Mathf.Max(0.05f, slideDuration);
        public float PhaseOffset => phaseOffset;

        public void Configure(float open, float closed, float phase)
        {
            openDuration = open;
            closedDuration = closed;
            phaseOffset = phase;
        }
    }
}
