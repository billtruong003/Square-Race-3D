using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// A patch of ground the food system fills with pellets - the Pet Survival feeding ground.
    /// Pure data like every region; the pellets themselves live in <see cref="Core.FoodSystem"/>.
    /// </summary>
    public class FoodArea : ArenaRegion
    {
        [Tooltip("Metres between pellets. The grid is clipped against walls at build time.")]
        [SerializeField] private float spacing = 2.4f;

        public float Spacing => Mathf.Max(0.8f, spacing);

        protected override Color GizmoColor => new Color(1f, 0.35f, 0.35f, 0.9f);
    }
}
