using System.Collections;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// The short beat between a monster being chosen and its run starting.
    ///
    /// One reusable system for all fifteen: the family's display colour drives the accent, so
    /// nothing here is per-monster. Kept deliberately brief — this plays before every attempt in a
    /// fifteen-unit episode, and anything longer starts costing real running time.
    /// </summary>
    public class SummonPresenter : MonoBehaviour
    {
        [SerializeField] private ChallengeDirector director;
        [SerializeField] private ChallengeShowCatalog catalog;
        [SerializeField] private ParticleSystem burstPrefab;

        [Header("Timing")]
        [Tooltip("Total presentation length. The director's summon hold should be at least this.")]
        [SerializeField] private float duration = 0.95f;
        [Tooltip("Share of the duration spent rising on the island before the swap.")]
        [Range(0.2f, 0.8f)][SerializeField] private float liftShare = 0.55f;
        [SerializeField] private float liftHeight = 0.7f;
        [SerializeField] private float scalePop = 1.12f;

        private ParticleSystem burst;
        private Coroutine routine;

        private void OnEnable()
        {
            if (director != null) director.UnitSummoned += OnSummoned;
        }

        private void OnDisable()
        {
            if (director != null) director.UnitSummoned -= OnSummoned;
            if (routine != null) { StopCoroutine(routine); routine = null; }
        }

        private void OnSummoned(ChallengeUnit unit)
        {
            if (unit == null || unit.Definition == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Play(unit));
        }

        private IEnumerator Play(ChallengeUnit unit)
        {
            var family = catalog != null ? catalog.FamilyOf(unit.Definition) : null;
            Color accent = family != null ? family.displayColor : Color.white;

            // The island copy is already hidden by the director at this point, so the lift plays on
            // the gameplay instance standing at the start line. That keeps one code path for the
            // effect instead of animating a display copy and then swapping it.
            Transform target = unit.transform;
            Vector3 basePos = target.position;
            Vector3 baseScale = target.localScale;

            EmitBurst(basePos + Vector3.up * unit.Definition.height * 0.25f, accent);

            float liftTime = duration * liftShare;
            float t = 0f;
            while (t < liftTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / liftTime);
                float ease = 1f - (1f - k) * (1f - k);        // ease-out
                target.position = basePos + Vector3.up * (liftHeight * ease);
                target.localScale = baseScale * Mathf.Lerp(1f, scalePop, ease);
                yield return null;
            }

            float settleTime = duration - liftTime;
            t = 0f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settleTime);
                target.position = Vector3.Lerp(basePos + Vector3.up * liftHeight, basePos, k);
                target.localScale = Vector3.Lerp(baseScale * scalePop, baseScale, k);
                yield return null;
            }

            // Hand the unit back exactly as it was found; the run must start from a clean pose.
            target.position = basePos;
            target.localScale = baseScale;
            routine = null;
        }

        private void EmitBurst(Vector3 position, Color accent)
        {
            if (burstPrefab == null) return;
            if (burst == null)
            {
                burst = Instantiate(burstPrefab, transform);
                burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            burst.transform.position = position;
            var main = burst.main;
            main.startColor = new ParticleSystem.MinMaxGradient(accent, Color.Lerp(accent, Color.white, 0.6f));
            burst.Play(true);
        }
    }
}
