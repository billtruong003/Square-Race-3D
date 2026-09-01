using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// What has to happen before a wall opens. All six rules count the same "impact" event; they
    /// differ only in who is allowed to contribute and how often.
    /// </summary>
    public enum BreakCondition
    {
        /// <summary>N impacts from anyone. The classic "hit it 100 times" shortcut.</summary>
        TotalHitsAnyRacer = 0,

        /// <summary>Opens the instant a racer of the required colour touches it.</summary>
        AnyHitByRequiredColor = 1,

        /// <summary>N impacts, but only ones from the required colour count.</summary>
        RequiredColorHitCount = 2,

        /// <summary>N distinct racers, any colour, each contributing at most once.</summary>
        UniqueRacerHitsAnyColor = 3,

        /// <summary>N distinct racers of the required colour, each contributing at most once.</summary>
        UniqueRacerHitsByRequiredColor = 4,

        /// <summary>The first impact from anyone opens it.</summary>
        SingleUseAnyRacer = 5
    }

    public enum WallRemovalMode
    {
        /// <summary>Collider off, then the visual sinks and shrinks away over a moment.</summary>
        ShrinkOut = 0,

        /// <summary>Collider and visual both off on the same step.</summary>
        Instant = 1
    }

    /// <summary>
    /// Makes any wall openable. Add it alongside an <see cref="ArenaWall"/>; the wall is built,
    /// resolved and collided with exactly as before until its condition is met.
    ///
    /// The component only holds rules and state - counting and breaking are driven by
    /// <see cref="BreakableWallSystem"/>, which sees the real impact events from the mover.
    /// </summary>
    [RequireComponent(typeof(ArenaWall))]
    [DisallowMultipleComponent]
    public class BreakableWall : MonoBehaviour
    {
        [SerializeField] private string id = "";

        [Header("Rule")]
        [SerializeField] private BreakCondition condition = BreakCondition.TotalHitsAnyRacer;

        [Tooltip("Impacts (or unique racers) needed. Ignored by the single-hit rules.")]
        [SerializeField] private int requiredHits = 20;

        [Tooltip("Colour a racer must match for the colour-gated rules.")]
        [SerializeField] private Color requiredColor = Color.red;

        [Tooltip("How close a racer colour has to be to count. 0 = exact.")]
        [Range(0f, 1f)] [SerializeField] private float colorTolerance = 0.25f;

        [Header("Contact debounce")]
        [Tooltip("Seconds before the same racer can register another impact on this wall. Stops a " +
                 "racer sliding along the face from counting every step.")]
        [SerializeField] private float contactCooldownPerRacer = 0.35f;

        [Header("Removal")]
        [SerializeField] private WallRemovalMode removalMode = WallRemovalMode.ShrinkOut;

        [SerializeField] private float removalDuration = 0.35f;

        [Header("Feedback")]
        [Tooltip("Tint the wall toward its required colour so a colour gate reads as one.")]
        [SerializeField] private bool showAccentColor = true;

        [Tooltip("Cosmetic tint override (alpha > 0 to use). Rainbow gate layers set this without " +
                 "touching the break rule.")]
        [SerializeField] private Color accentOverride = new Color(0f, 0f, 0f, 0f);

        [Tooltip("Brighten as it nears breaking, so a fragile wall reads as fragile.")]
        [SerializeField] private bool showProgress = true;

        [SerializeField] private float hitFlashDuration = 0.12f;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public Color AccentOverride => accentOverride;
        public void SetAccentOverride(Color value) => accentOverride = value;
        public BreakCondition Condition => condition;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public Color RequiredColor => requiredColor;
        public float ColorTolerance => colorTolerance;
        public float ContactCooldownPerRacer => Mathf.Max(0f, contactCooldownPerRacer);
        public WallRemovalMode RemovalMode => removalMode;
        public float RemovalDuration => Mathf.Max(0.01f, removalDuration);
        public bool ShowAccentColor => showAccentColor;
        public bool ShowProgress => showProgress;
        public float HitFlashDuration => Mathf.Max(0f, hitFlashDuration);

        /// <summary>True when this rule only counts racers matching <see cref="RequiredColor"/>.</summary>
        public bool IsColorGated =>
            condition == BreakCondition.AnyHitByRequiredColor ||
            condition == BreakCondition.RequiredColorHitCount ||
            condition == BreakCondition.UniqueRacerHitsByRequiredColor;

        /// <summary>True when a racer may only ever contribute one count.</summary>
        public bool CountsUniqueRacersOnly =>
            condition == BreakCondition.UniqueRacerHitsAnyColor ||
            condition == BreakCondition.UniqueRacerHitsByRequiredColor;

        /// <summary>Hits needed for this rule; the single-hit rules always need one.</summary>
        public int ResolveTarget()
        {
            switch (condition)
            {
                case BreakCondition.AnyHitByRequiredColor:
                case BreakCondition.SingleUseAnyRacer:
                    return 1;
                default:
                    return RequiredHits;
            }
        }

        /// <summary>True when a racer colour is close enough to count for a colour-gated rule.</summary>
        public bool ColorMatches(Color color)
        {
            float dr = color.r - requiredColor.r;
            float dg = color.g - requiredColor.g;
            float db = color.b - requiredColor.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db) <= colorTolerance;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsColorGated ? requiredColor : new Color(1f, 0.7f, 0.15f, 1f);

            Vector3 p = transform.position;
            Vector3 s = transform.lossyScale;
            Gizmos.DrawWireCube(p, s * 1.03f);
        }
    }
}
