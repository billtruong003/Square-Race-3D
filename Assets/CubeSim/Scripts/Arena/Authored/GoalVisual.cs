using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.Arena.Authored
{
    /// <summary>How a goal area presents itself. Gameplay is identical; only the dressing differs.</summary>
    public enum GoalVisualType
    {
        /// <summary>Nothing built. The map supplies its own art.</summary>
        None = 0,

        /// <summary>A raised, glowing platform filling the goal footprint.</summary>
        FinishPad = 1,

        /// <summary>A ring standing on the pad, readable as a destination from above.</summary>
        Portal = 2,

        /// <summary>Two posts and a lintel across the entry face of the goal.</summary>
        Gate = 3,

        /// <summary>A flat outlined patch - the lightest option.</summary>
        MarkerZone = 4
    }

    /// <summary>
    /// Builds the readable destination for a route map.
    ///
    /// Kept separate from <see cref="GoalArea"/> on purpose: the area is the gameplay rule, this is
    /// the presentation, and a map picks whichever style suits it without either affecting the other.
    /// </summary>
    public static class GoalVisualFactory
    {
        public static void Build(GoalArea goal, MaterialLibrary materials, float groundY,
            float wallHeight, Transform parent)
        {
            if (goal == null || goal.VisualType == GoalVisualType.None) return;

            Rect footprint = goal.Footprint;
            Color color = goal.VisualColor;
            Material material = materials.GetGoalMaterial(goal.Id, color, goal.VisualEmission);

            var root = new GameObject("GoalVisual_" + goal.Id).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            switch (goal.VisualType)
            {
                case GoalVisualType.FinishPad:
                    BuildPad(root, footprint, groundY, material, 0.12f);
                    BuildCheckerEdge(root, footprint, groundY, materials, color);
                    break;

                case GoalVisualType.Portal:
                    BuildPad(root, footprint, groundY, material, 0.1f);
                    BuildPortalRing(root, footprint, groundY, wallHeight, material);
                    break;

                case GoalVisualType.Gate:
                    BuildPad(root, footprint, groundY, material, 0.1f);
                    BuildGate(root, footprint, groundY, wallHeight, material);
                    break;

                case GoalVisualType.MarkerZone:
                    BuildPad(root, footprint, groundY, material, 0.03f);
                    break;
            }
        }

        /// <summary>
        /// Hazards get the same flat glowing pad treatment as goals. In the reference the red danger
        /// zone is as visually loud as the green destination, and it reads as the counterpart to it.
        /// </summary>
        public static void BuildHazard(HazardArea hazard, MaterialLibrary materials, float groundY,
            Transform parent)
        {
            if (hazard == null) return;

            Rect footprint = hazard.Footprint;
            Color color = hazard.IsLethal ? new Color(0.95f, 0.08f, 0.10f) : new Color(0.92f, 0.12f, 0.13f);
            Material material = materials.GetGoalMaterial("hazard_" + hazard.Id, color, 0.9f);

            var root = new GameObject("HazardVisual_" + hazard.Id).transform;
            root.SetParent(parent, false);

            BuildPad(root, footprint, groundY, material, 0.1f);

            // A few dark bars across it, so it reads as hazardous rather than as another goal.
            Material dark = materials.GetGoalMaterial("hazard_bars", color * 0.12f, 0f);
            for (int i = 0; i < 4; i++)
            {
                GameObject bar = Box(root, "Bar_" + i, dark);
                float x = Mathf.Lerp(footprint.xMin, footprint.xMax, (i + 0.5f) / 4f);
                bar.transform.localPosition = new Vector3(x, groundY + 0.12f, footprint.center.y);
                bar.transform.localScale = new Vector3(footprint.width * 0.09f, 0.07f, footprint.height * 0.72f);
            }
        }

        private static void BuildPad(Transform parent, Rect footprint, float groundY,
            Material material, float height)
        {
            GameObject pad = Box(parent, "Pad", material);
            pad.transform.localPosition = new Vector3(footprint.center.x, groundY + height * 0.5f, footprint.center.y);
            pad.transform.localScale = new Vector3(footprint.width, height, footprint.height);
        }

        /// <summary>Alternating blocks along the entry edge - the universal "finish line" read.</summary>
        private static void BuildCheckerEdge(Transform parent, Rect footprint, float groundY,
            MaterialLibrary materials, Color color)
        {
            const int Blocks = 8;
            float blockWidth = footprint.height / Blocks;
            Material dark = materials.GetGoalMaterial("checker_dark", color * 0.15f, 0f);

            for (int i = 0; i < Blocks; i += 2)
            {
                GameObject block = Box(parent, "Checker_" + i, dark);
                float z = footprint.yMin + blockWidth * (i + 0.5f);
                block.transform.localPosition = new Vector3(footprint.xMin + 0.45f, groundY + 0.14f, z);
                block.transform.localScale = new Vector3(0.9f, 0.08f, blockWidth);
            }
        }

        private static void BuildPortalRing(Transform parent, Rect footprint, float groundY,
            float wallHeight, Material material)
        {
            const int Segments = 16;
            float radius = Mathf.Min(footprint.width, footprint.height) * 0.42f;
            float thickness = radius * 0.16f;
            var centre = new Vector3(footprint.center.x, groundY + radius + 0.2f, footprint.center.y);

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments);
                GameObject piece = Box(parent, "Ring_" + i, material);

                piece.transform.localPosition = centre + new Vector3(0f, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
                piece.transform.localRotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
                piece.transform.localScale = new Vector3(thickness, thickness, radius * 2f * Mathf.PI / Segments * 1.1f);
            }
        }

        private static void BuildGate(Transform parent, Rect footprint, float groundY,
            float wallHeight, Material material)
        {
            float height = wallHeight * 1.4f;
            float postSize = Mathf.Min(footprint.width, footprint.height) * 0.12f;
            float x = footprint.xMin + postSize;

            GameObject left = Box(parent, "Post_L", material);
            left.transform.localPosition = new Vector3(x, groundY + height * 0.5f, footprint.yMin + postSize);
            left.transform.localScale = new Vector3(postSize, height, postSize);

            GameObject right = Box(parent, "Post_R", material);
            right.transform.localPosition = new Vector3(x, groundY + height * 0.5f, footprint.yMax - postSize);
            right.transform.localScale = new Vector3(postSize, height, postSize);

            GameObject lintel = Box(parent, "Lintel", material);
            lintel.transform.localPosition = new Vector3(x, groundY + height, footprint.center.y);
            lintel.transform.localScale = new Vector3(postSize, postSize, footprint.height);
        }

        private static GameObject Box(Transform parent, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                // Decoration only. A collider here would block the racers it is meant to attract.
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
