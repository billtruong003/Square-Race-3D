using UnityEngine;

namespace CubeSim.Arena.Authored
{
    public enum SpikeState { Idle = 0, Warning = 1, Up = 2 }

    /// <summary>Floor spikes on a clock. Only the Up state hurts; Warning is the tell.</summary>
    public class SpikeTrap : ArenaRegion
    {
        [SerializeField] private float upDuration = 1.4f;
        [SerializeField] private float idleDuration = 2.4f;
        [SerializeField] private float warnDuration = 0.6f;
        [SerializeField] private float phase = 0f;
        [SerializeField] private float damage = 0.5f;   // half a heart: a 1-heart sudden-death racer survives one bite
        [SerializeField] private float hitCooldown = 1f;

        public float Damage => Mathf.Max(0f, damage);
        public float HitCooldown => Mathf.Max(0.1f, hitCooldown);

        public SpikeState StateAt(float t)
        {
            float cycle = idleDuration + warnDuration + upDuration;
            float u = Mathf.Repeat(t + phase, cycle);
            if (u < idleDuration) return SpikeState.Idle;
            if (u < idleDuration + warnDuration) return SpikeState.Warning;
            return SpikeState.Up;
        }

        public void Configure(float up, float idle, float warn, float phaseOffset)
        {
            upDuration = up;
            idleDuration = idle;
            warnDuration = warn;
            phase = phaseOffset;
        }

        protected override Color GizmoColor => new Color(0.8f, 0.8f, 0.85f, 0.9f);
    }
}
