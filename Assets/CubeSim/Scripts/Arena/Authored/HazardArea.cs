using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>Kills or damages any racer that enters. The reusable hazard for authored maps.</summary>
    public class HazardArea : ArenaRegion
    {
        [Tooltip("Damage per second applied while a racer is inside. 0 or less kills outright.")]
        [SerializeField] private float damagePerSecond = 0f;

        public float DamagePerSecond => damagePerSecond;

        public bool IsLethal => damagePerSecond <= 0f;

        protected override Color GizmoColor => new Color(1f, 0.25f, 0.2f, 0.9f);
    }
}
