using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// A wall placed by hand in the Scene view. The transform is the authored footprint: move and
    /// scale it like any cube.
    ///
    /// A BoundaryFill wall additionally swallows the dead space behind it, so non-playable regions
    /// read as solid mass instead of a thin bar floating on a grey floor. Filling never moves the
    /// inner face - see <see cref="WallFillMath"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ArenaWall : MonoBehaviour
    {
        [SerializeField] private ArenaWallType wallType = ArenaWallType.Internal;
        [SerializeField] private WallFillMode fillMode = WallFillMode.FixedThickness;

        [Tooltip("Which way the dead space lies. The opposite face is the playable one and never moves.")]
        [SerializeField] private FillDirection fillDirection = FillDirection.PlusX;

        [Tooltip("Height override. 0 uses the arena's wall height.")]
        [SerializeField] private float heightOverride = 0f;

        public ArenaWallType WallType => wallType;
        public WallFillMode FillMode => fillMode;
        public FillDirection Direction => fillDirection;

        /// <summary>The footprint exactly as authored, before any fill is applied.</summary>
        public Rect AuthoredFootprint { get; private set; }

        /// <summary>The footprint actually built, after fill.</summary>
        public Rect ResolvedFootprint { get; private set; }

        /// <summary>
        /// Resolves the final footprint and writes it back onto the transform. Called once when the
        /// arena is built, and from OnValidate so the Scene view previews the real mass.
        /// </summary>
        public Rect Resolve(Rect arenaBounds, float wallHeight, float groundY, bool applyToTransform)
        {
            Rect authored = AuthoredFootprint.width > 0f && Application.isPlaying
                ? AuthoredFootprint
                : CurrentFootprint();

            AuthoredFootprint = authored;

            Rect resolved = wallType == ArenaWallType.BoundaryFill && fillMode == WallFillMode.ExtendToArenaBounds
                ? WallFillMath.Extend(authored, arenaBounds, fillDirection)
                : authored;

            ResolvedFootprint = resolved;

            if (applyToTransform)
            {
                float height = heightOverride > 0f ? heightOverride : wallHeight;
                transform.position = new Vector3(resolved.center.x, groundY + height * 0.5f, resolved.center.y);
                transform.localScale = new Vector3(resolved.width, height, resolved.height);
                transform.rotation = Quaternion.identity;
            }

            return resolved;
        }

        /// <summary>Footprint read straight off the transform, ignoring any rotation.</summary>
        public Rect CurrentFootprint()
        {
            Vector3 p = transform.position;
            Vector3 s = transform.lossyScale;
            return WallFillMath.FromCenterSize(new Vector2(p.x, p.z), new Vector2(s.x, s.z));
        }

        /// <summary>Makes sure the object can actually block a racer once the arena is built.</summary>
        [Tooltip("The builder baked a material onto this wall (door slab, locked gate); never replace it at play time.")]
        [SerializeField] private bool keepBakedMaterial = false;

        public void SetKeepBakedMaterial(bool value) => keepBakedMaterial = value;

        public void PrepareForPlay(Material material)
        {
            gameObject.layer = SimulationLayers.Wall;

            if (GetComponent<MeshFilter>() == null)
            {
                // An authored wall may have been created as an empty; give it the cube it implies.
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh cube = probe.GetComponent<MeshFilter>().sharedMesh;
                DestroyProbe(probe);

                gameObject.AddComponent<MeshFilter>().sharedMesh = cube;
            }

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();

            // A wall with a baked look (glass gate, rock model) keeps the material its builder
            // gave it; the shared wall material is only for plain geometry.
            var breakable = GetComponent<BreakableWall>();
            bool customVisual = keepBakedMaterial || (breakable != null && breakable.CustomVisual);
            if (material != null && !customVisual) renderer.sharedMaterial = material;

            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = Vector3.one;
        }

        private static void DestroyProbe(GameObject probe)
        {
            if (Application.isPlaying) Destroy(probe);
            else DestroyImmediate(probe);
        }

        private void OnDrawGizmosSelected()
        {
            if (wallType != ArenaWallType.BoundaryFill) return;

            // Show which face is pinned.
            Rect r = CurrentFootprint();
            float inner = WallFillMath.InnerFace(r, fillDirection);
            int axis = WallFillMath.Axis(fillDirection);

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Vector3 a, b;
            if (axis == 0)
            {
                a = new Vector3(inner, transform.position.y, r.yMin);
                b = new Vector3(inner, transform.position.y, r.yMax);
            }
            else
            {
                a = new Vector3(r.xMin, transform.position.y, inner);
                b = new Vector3(r.xMax, transform.position.y, inner);
            }

            Gizmos.DrawLine(a, b);

            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = axis == 0
                ? new Vector3(WallFillMath.Sign(fillDirection), 0f, 0f)
                : new Vector3(0f, 0f, WallFillMath.Sign(fillDirection));

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawLine(mid, mid + dir * 3f);
        }
    }
}
