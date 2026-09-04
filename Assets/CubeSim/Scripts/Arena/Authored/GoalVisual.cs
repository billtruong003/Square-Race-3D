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
                    // Solid colour pad, calm emission - the colour itself is the signal - with a
                    // brighter clean border frame carrying the glow instead of the whole slab.
                    Material padMaterial = materials.GetGoalMaterial(goal.Id + "_pad", color, 0.4f);
                    BuildPad(root, footprint, groundY, padMaterial, 0.1f);

                    Material frame = materials.GetGoalMaterial(goal.Id + "_frame",
                        Color.Lerp(color, Color.white, 0.35f), 1.2f);
                    BuildBorderFrame(root, footprint, groundY, frame, 0.35f, 0.16f);

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
        /// The deadzone look: a solid deep-red floor with almost no glow - the colour stays flat
        /// and honest - wrapped in an amber/black hazard-tape border so it reads as "danger" from
        /// any zoom without blooming all over the map.
        /// </summary>
        public static void BuildHazard(HazardArea hazard, MaterialLibrary materials, float groundY,
            Transform parent)
        {
            if (hazard == null) return;

            Rect footprint = hazard.Footprint;

            var root = new GameObject("HazardVisual_" + hazard.Id).transform;
            root.SetParent(parent, false);

            Material floor = materials.GetGoalMaterial("hazard_floor",
                new Color(0.52f, 0.07f, 0.09f), 0.06f);
            BuildPad(root, footprint, groundY, floor, 0.08f);

            Material amber = materials.GetGoalMaterial("hazard_tape_amber",
                new Color(1f, 0.65f, 0.05f), 0.3f);
            Material black = materials.GetGoalMaterial("hazard_tape_black",
                new Color(0.09f, 0.09f, 0.1f), 0f);
            BuildStripedBorder(root, footprint, groundY, amber, black, 0.4f, 0.14f);
        }

        /// <summary>Four clean bars framing the footprint - the goal's glow lives here.</summary>
        private static void BuildBorderFrame(Transform parent, Rect footprint, float groundY,
            Material material, float thickness, float height)
        {
            float y = groundY + height * 0.5f + 0.06f;

            GameObject north = Box(parent, "Frame_N", material);
            north.transform.localPosition = new Vector3(footprint.center.x, y, footprint.yMax - thickness * 0.5f);
            north.transform.localScale = new Vector3(footprint.width, height, thickness);

            GameObject south = Box(parent, "Frame_S", material);
            south.transform.localPosition = new Vector3(footprint.center.x, y, footprint.yMin + thickness * 0.5f);
            south.transform.localScale = new Vector3(footprint.width, height, thickness);

            GameObject west = Box(parent, "Frame_W", material);
            west.transform.localPosition = new Vector3(footprint.xMin + thickness * 0.5f, y, footprint.center.y);
            west.transform.localScale = new Vector3(thickness, height, footprint.height);

            GameObject east = Box(parent, "Frame_E", material);
            east.transform.localPosition = new Vector3(footprint.xMax - thickness * 0.5f, y, footprint.center.y);
            east.transform.localScale = new Vector3(thickness, height, footprint.height);
        }

        /// <summary>Hazard-tape border: alternating segments around the perimeter.</summary>
        private static void BuildStripedBorder(Transform parent, Rect footprint, float groundY,
            Material a, Material b, float thickness, float height)
        {
            float y = groundY + height * 0.5f + 0.05f;
            const float SegmentLength = 1.1f;

            void Edge(Vector3 start, Vector3 direction, float length, bool horizontal, string name)
            {
                int segments = Mathf.Max(1, Mathf.RoundToInt(length / SegmentLength));
                float step = length / segments;

                for (int i = 0; i < segments; i++)
                {
                    GameObject seg = Box(parent, $"{name}_{i}", i % 2 == 0 ? a : b);
                    Vector3 centre = start + direction * (step * (i + 0.5f));
                    seg.transform.localPosition = new Vector3(centre.x, y, centre.z);
                    seg.transform.localScale = horizontal
                        ? new Vector3(step, height, thickness)
                        : new Vector3(thickness, height, step);
                }
            }

            Edge(new Vector3(footprint.xMin, 0f, footprint.yMax - thickness * 0.5f), Vector3.right, footprint.width, true, "TapeN");
            Edge(new Vector3(footprint.xMin, 0f, footprint.yMin + thickness * 0.5f), Vector3.right, footprint.width, true, "TapeS");
            Edge(new Vector3(footprint.xMin + thickness * 0.5f, 0f, footprint.yMin), Vector3.forward, footprint.height, false, "TapeW");
            Edge(new Vector3(footprint.xMax - thickness * 0.5f, 0f, footprint.yMin), Vector3.forward, footprint.height, false, "TapeE");
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
