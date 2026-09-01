using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// A trigger volume that resolves any unit entering it. One component covers both the finish
    /// line and the out-of-bounds volume, because the only difference is the verdict.
    ///
    /// Ragdoll bone colliders resolve to their owning unit too, so a unit that is launched across
    /// the finish line still counts as a pass — the pass condition is physical, not stateful.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChallengeZoneTrigger : MonoBehaviour
    {
        public enum Verdict
        {
            Pass,
            Fail
        }

        [SerializeField] private Verdict verdict = Verdict.Pass;
        [SerializeField] private ChallengeOutcomeReason failReason = ChallengeOutcomeReason.FellOutOfArena;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var unit = other.GetComponentInParent<ChallengeUnit>();
            if (unit == null || unit.IsResolved) return;

            if (verdict == Verdict.Pass) unit.MarkPassed();
            else unit.MarkFailed(failReason);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;
            Gizmos.color = verdict == Verdict.Pass
                ? new Color(0.2f, 1f, 0.3f, 0.25f)
                : new Color(1f, 0.2f, 0.2f, 0.15f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box) Gizmos.DrawCube(box.center, box.size);
        }
    }
}
