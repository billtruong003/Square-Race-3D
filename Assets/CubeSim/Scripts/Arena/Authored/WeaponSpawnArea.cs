using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>Where weapons may appear. The exact point inside comes from the run seed.</summary>
    public class WeaponSpawnArea : ArenaRegion
    {
        [Tooltip("Weight when several areas compete for the same weapon. Higher = more likely.")]
        [SerializeField] private float weight = 1f;

        public float Weight => Mathf.Max(0f, weight);

        protected override Color GizmoColor => new Color(1f, 0.85f, 0.2f, 0.9f);
    }
}
