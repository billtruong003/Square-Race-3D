using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// Base for every rectangular area a designer places: spawn, goal, weapon spawn, hazard.
    ///
    /// The transform is the region: position is the centre, X and Z scale are the size. Nothing here
    /// carries gameplay code - the runtime systems read these as data, which is what lets a new map
    /// be built by placing objects rather than writing scripts.
    /// </summary>
    public abstract class ArenaRegion : MonoBehaviour
    {
        [SerializeField] private string id = "";

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public Rect Footprint
        {
            get
            {
                Vector3 p = transform.position;
                Vector3 s = transform.lossyScale;
                return WallFillMath.FromCenterSize(new Vector2(p.x, p.z), new Vector2(s.x, s.z));
            }
        }

        public Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

        public bool Contains(Vector3 position)
        {
            Rect r = Footprint;
            return position.x >= r.xMin && position.x <= r.xMax &&
                   position.z >= r.yMin && position.z <= r.yMax;
        }

        /// <summary>True when a box of this half extent is fully inside the region.</summary>
        public bool ContainsBox(Vector3 position, float halfExtent)
        {
            Rect r = Footprint;
            return position.x - halfExtent >= r.xMin && position.x + halfExtent <= r.xMax &&
                   position.z - halfExtent >= r.yMin && position.z + halfExtent <= r.yMax;
        }

        protected abstract Color GizmoColor { get; }

        private void OnDrawGizmos()
        {
            Rect r = Footprint;
            var center = new Vector3(r.center.x, transform.position.y, r.center.y);
            var size = new Vector3(r.width, 0.25f, r.height);

            Gizmos.color = GizmoColor;
            Gizmos.DrawWireCube(center, size);

            Color fill = GizmoColor;
            fill.a = 0.12f;
            Gizmos.color = fill;
            Gizmos.DrawCube(center, size);
        }
    }
}
