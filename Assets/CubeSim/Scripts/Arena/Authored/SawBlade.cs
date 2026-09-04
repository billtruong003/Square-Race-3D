using UnityEngine;

namespace CubeSim.Arena.Authored
{
    // ------------------------------------------------------------------------------------------
    // The obstacle roster of the map contract, as pure data. None of these components run logic:
    // Core.ArenaDeviceSystem poses and evaluates every one of them as a function of elapsed run
    // time and racer state, so a seed replays cut for cut.
    //
    //   S  SawBlade      a spinning circular blade; static, or riding a rail back and forth
    //   P  Crusher       a wall slab that slides across a corridor; pinned against a wall = dead
    //   T  SpikeTrap     floor spikes on a clock: idle -> warn -> up; touching them up costs a heart
    //   U  Bumper        a barrel that flings a racer away at double speed
    //   > < ^ v Conveyor floor that drags whoever stands on it
    //   K/k LockedGate   a wall that drops the moment any racer grabs the key
    //   $  CoinPickup    a coin that respawns; the Coin Rush score
    //   +  PotionPickup  one heart back
    //   1 2 Teleporter   step on one pad, appear on its twin
    // ------------------------------------------------------------------------------------------

    /// <summary>A circular blade. Overlap with a racer disc is a cut; the blade never blocks.</summary>
    [DisallowMultipleComponent]
    public class SawBlade : MonoBehaviour
    {
        [SerializeField] private float radius = 2f;
        [SerializeField] private float degreesPerSecond = 420f;
        [SerializeField] private float damagePerHit = 1f;
        [SerializeField] private float hitCooldown = 0.8f;

        [Header("Rail (equal points = static blade)")]
        [Tooltip("Local positions, in the arena root's space.")]
        [SerializeField] private Vector3 railStart;
        [SerializeField] private Vector3 railEnd;
        [SerializeField] private float railSpeed = 5f;
        [SerializeField] private float phase = 0f;

        public float Radius => Mathf.Max(0.1f, radius);
        public float DegreesPerSecond => degreesPerSecond;
        public float DamagePerHit => Mathf.Max(0f, damagePerHit);
        public float HitCooldown => Mathf.Max(0.05f, hitCooldown);
        public bool OnRail => (railEnd - railStart).sqrMagnitude > 1e-4f;

        /// <summary>Where the blade centre sits at run time t: a ping-pong along the rail.</summary>
        public Vector3 PositionAt(float t)
        {
            if (!OnRail) return railStart;
            float length = Vector3.Distance(railStart, railEnd);
            float u = Mathf.PingPong(railSpeed * t + phase, length) / length;
            return Vector3.Lerp(railStart, railEnd, u);
        }

        public void SetDamage(float perHit) => damagePerHit = Mathf.Max(0f, perHit);

        public void Configure(float r, float speedDeg, Vector3 start, Vector3 end, float railMetresPerSecond, float phaseOffset)
        {
            radius = r;
            degreesPerSecond = speedDeg;
            railStart = start;
            railEnd = end;
            railSpeed = railMetresPerSecond;
            phase = phaseOffset;
        }
    }
}
