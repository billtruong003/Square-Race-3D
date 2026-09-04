using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>Floor that drags a racer along a fixed direction while it stands on it.</summary>
    public class ConveyorArea : ArenaRegion
    {
        [SerializeField] private Vector2 direction = Vector2.right;
        [SerializeField] private float speed = 6f;

        [Tooltip("The belt surface; the device system scrolls its texture along the drag direction.")]
        [SerializeField] private Renderer plate;

        [Tooltip("Metres one repeat of the belt texture covers.")]
        [SerializeField] private float tileLength = 5.6f;   // one herringbone repeat per 5.6 m: readable from the top-down camera

        public Renderer Plate => plate;
        public float TileLength => Mathf.Max(0.1f, tileLength);
        public void SetPlate(Renderer value) => plate = value;

        public Vector3 Direction
        {
            get
            {
                var d = new Vector3(direction.x, 0f, direction.y);
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
            }
        }

        public float Speed => Mathf.Max(0f, speed);

        public void Configure(Vector2 dir, float metresPerSecond)
        {
            direction = dir;
            speed = metresPerSecond;
        }

        protected override Color GizmoColor => new Color(0.3f, 0.9f, 0.9f, 0.9f);
    }
}
