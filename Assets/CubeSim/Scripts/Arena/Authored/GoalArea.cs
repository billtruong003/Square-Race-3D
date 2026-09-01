using UnityEngine;

namespace CubeSim.Arena.Authored
{
    public enum GoalMode
    {
        /// <summary>A racer that enters the region has finished the course.</summary>
        ReachDestination = 0
    }

    /// <summary>
    /// The destination of an authored course. Purely a declared region - detection lives in
    /// <see cref="Core.GoalSystem"/> so nothing about goals leaks into the mover.
    /// </summary>
    public class GoalArea : ArenaRegion
    {
        [SerializeField] private GoalMode mode = GoalMode.ReachDestination;

        [Tooltip("A racer that reaches the goal stops moving and is no longer crushable.")]
        [SerializeField] private bool retireOnReach = true;

        [Tooltip("Fraction of the racer that must be inside. 1 = fully inside, 0 = centre only.")]
        [Range(0f, 1f)] [SerializeField] private float entryFraction = 0.5f;

        [Header("Presentation")]
        [Tooltip("How the destination is dressed. Gameplay is identical for every style.")]
        [SerializeField] private GoalVisualType visualType = GoalVisualType.FinishPad;

        [SerializeField] private Color visualColor = new Color(0.16f, 0.95f, 0.35f, 1f);

        [Range(0f, 4f)] [SerializeField] private float visualEmission = 0.8f;

        public GoalMode Mode => mode;
        public bool RetireOnReach => retireOnReach;
        public GoalVisualType VisualType => visualType;
        public Color VisualColor => visualColor;
        public float VisualEmission => visualEmission;

        /// <summary>True when the racer counts as having entered.</summary>
        public bool HasEntered(Vector3 position, float halfExtent)
        {
            float shrink = halfExtent * Mathf.Clamp01(entryFraction);
            Rect r = Footprint;

            return position.x + shrink >= r.xMin && position.x - shrink <= r.xMax &&
                   position.z + shrink >= r.yMin && position.z - shrink <= r.yMax;
        }

        protected override Color GizmoColor => new Color(0.2f, 1f, 0.35f, 0.95f);
    }
}
