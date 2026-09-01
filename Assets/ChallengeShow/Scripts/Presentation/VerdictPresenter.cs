using System.Collections;
using TMPro;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Shows PASS or FAIL when an attempt is genuinely over.
    ///
    /// Driven by the director's AttemptResolved event rather than by unit state, so it can only fire
    /// on a real verdict — a knockdown or a recovery mid-run never triggers it, which is the whole
    /// point now that a monster goes down two or three times per attempt.
    /// </summary>
    public class VerdictPresenter : MonoBehaviour
    {
        [SerializeField] private ChallengeDirector director;
        [SerializeField] private TextMeshPro label;

        [Header("Placement")]
        [Tooltip("Metres above the resolved unit the verdict floats.")]
        [SerializeField] private float heightAboveUnit = 3.2f;

        [Header("Look")]
        [SerializeField] private Color passColor = new(0.42f, 0.95f, 0.45f);
        [SerializeField] private Color failColor = new(0.98f, 0.36f, 0.32f);
        [SerializeField] private float holdSeconds = 1.9f;
        [SerializeField] private float punchScale = 1.35f;
        [SerializeField] private float punchSeconds = 0.22f;

        private Coroutine routine;
        private ChallengeUnit tracked;

        private void Awake() => Hide();

        private void OnEnable()
        {
            if (director != null) director.AttemptResolved += OnResolved;
        }

        private void OnDisable()
        {
            if (director != null) director.AttemptResolved -= OnResolved;
            if (routine != null) { StopCoroutine(routine); routine = null; }
            Hide();
        }

        private void LateUpdate()
        {
            if (label == null || !label.gameObject.activeSelf || tracked == null) return;
            // TrackedPosition, not transform.position: after a knockdown the root stays where the
            // hit landed while the body flies on, so the verdict would hang metres from the monster
            // it belongs to - and often outside the result shot entirely.
            label.transform.position = tracked.TrackedPosition + Vector3.up * heightAboveUnit;
        }

        private void OnResolved(ChallengeUnit unit)
        {
            if (label == null || unit == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Play(unit));
        }

        private IEnumerator Play(ChallengeUnit unit)
        {
            bool passed = unit.State == ChallengeState.Passed;
            tracked = unit;

            label.text = passed ? "PASS!" : "FAIL!";
            label.color = passed ? passColor : failColor;
            label.gameObject.SetActive(true);
            label.transform.position = unit.TrackedPosition + Vector3.up * heightAboveUnit;

            // Scale punch: overshoot then settle. Cheap, reads instantly at video size.
            Vector3 target = Vector3.one;
            float t = 0f;
            while (t < punchSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / punchSeconds);
                float s = Mathf.Lerp(0.3f, punchScale, 1f - (1f - k) * (1f - k));
                label.transform.localScale = target * s;
                yield return null;
            }

            t = 0f;
            const float settle = 0.12f;
            while (t < settle)
            {
                t += Time.deltaTime;
                label.transform.localScale = target * Mathf.Lerp(punchScale, 1f, Mathf.Clamp01(t / settle));
                yield return null;
            }
            label.transform.localScale = target;

            yield return new WaitForSeconds(holdSeconds);
            Hide();
            routine = null;
        }

        private void Hide()
        {
            if (label != null) label.gameObject.SetActive(false);
            tracked = null;
        }
    }
}
