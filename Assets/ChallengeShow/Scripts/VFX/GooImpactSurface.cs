using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Put on the crystal spike wall. Turns hard ragdoll impacts into a goo burst and ignores the
    /// constant light scraping a settled body produces against the rocks.
    ///
    /// Deliberately has no gameplay authority: slamming into the wall is a spectacle, not a verdict.
    /// A unit with durability left bounces off, settles and gets back up.
    /// </summary>
    public class GooImpactSurface : MonoBehaviour
    {
        [SerializeField] private GooSplatterPool pool;
        [SerializeField] private ChallengeRunRecorder recorder;

        [Tooltip("Collision impulse below this is ignored entirely.")]
        [SerializeField] private float minImpulse = 6f;
        [Tooltip("Impulse that produces a full-strength burst. Anything above clamps here.")]
        [SerializeField] private float maxImpulse = 45f;
        [Tooltip("Seconds before this surface can splatter again, to stop bursts stacking up.")]
        [SerializeField] private float cooldown = 0.15f;
        [Tooltip("Log each splatter. Handy for confirming the threshold is actually reachable.")]
        [SerializeField] private bool logSplatters;

        private float nextAllowedTime;

        /// <summary>Splatters produced this session. Read by playtests to confirm the VFX fires.</summary>
        public int SplatterCount { get; private set; }
        /// <summary>Strongest contact seen, splattered or not — tells you if minImpulse is too high.</summary>
        public float StrongestImpulseSeen { get; private set; }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0 || collision.rigidbody == null) return;

            // Only monsters bleed goo.
            var unit = collision.collider.GetComponentInParent<ChallengeUnit>();
            if (unit == null) return;

            float impulse = collision.impulse.magnitude;
            StrongestImpulseSeen = Mathf.Max(StrongestImpulseSeen, impulse);

            bool canSplatter = pool != null && Time.time >= nextAllowedTime && impulse >= minImpulse;
            if (canSplatter)
            {
                nextAllowedTime = Time.time + cooldown;
                SplatterCount++;

                var contact = collision.GetContact(0);
                float intensity = Mathf.InverseLerp(minImpulse, maxImpulse, impulse);
                pool.Spawn(contact.point, contact.normal, intensity);
                if (logSplatters) Debug.Log($"[Goo] splatter, impulse {impulse:0.0}");
            }

            if (impulse >= minImpulse) recorder?.RecordWallImpact(unit, impulse, canSplatter);
        }
    }
}
