using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena.Authored;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds an authored arena prefab from an ASCII template file - the map contract made
    /// executable. '#' is solid wall mass, 'B' is a breakable wall (a door racers bash open),
    /// 'M' is a mega block (one huge breakable with a big countdown), 'C' is a colour gate (the
    /// rect slices into coloured one-cell layers, each a breakable), 'D' is a cycling door that
    /// slides open and shut on a clock, 'O' is a rotor room (a spinning cross of bars), '.' is
    /// open floor, and marker letters declare regions (G goal, X hazard, W weapon area, F food
    /// field, L/R spawn areas). Region marker cells are open floor.
    ///
    /// The template is the single source of truth: geometry comes only from '#' cells, wall cells
    /// are merged into maximal rectangles rather than emitted one cube per cell, and the built
    /// prefab is diffed back against its own template through the silhouette check - so a map can
    /// never drift from what its file says.
    /// </summary>
    public static class AsciiArenaBuilder
    {
        public sealed class Settings
        {
            public string ArenaId;
            public Vector2 CourseSize;
            public float WallHeight = 2.8f;
            public float VisualFillPadding = 22f;
            public float DesignedCorridorWidth = 2.8f;
            public Color GoalColor = new Color(0.05f, 0.95f, 0.12f);
            public float GoalEmission = 0.55f;
            // Two seconds of standing in the red zone costs a heart - punishing, not instant.
            public float HazardDamagePerSecond = 0.5f;

            /// <summary>Hits each breakable ('B') wall takes before it opens.</summary>
            public int BreakableHits = 40;

            /// <summary>Hits for a mega block ('M') - the four-digit-counter centrepiece.</summary>
            public int MegaBlockHits = 600;

            /// <summary>Hits per rainbow gate layer ('R').</summary>
            public int RainbowLayerHits = 2;
        }

        /// <summary>
        /// Lines of the grid itself: comment lines (starting with "//") and blanks are skipped.
        /// </summary>
        public static string[] ParseTemplate(string text)
        {
            string[] rows = text.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Length > 0 && !l.StartsWith("//"))
                .ToArray();

            if (rows.Length == 0)
            {
                throw new System.ArgumentException("Template has no grid rows.");
            }

            int width = rows[0].Length;
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new System.ArgumentException(
                        $"Template row {y} is {rows[y].Length} wide, expected {width}.");
                }
            }

            return rows;
        }

        public static GameObject Build(string templatePath, Settings settings, string prefabPath)
        {
            string[] grid = ParseTemplate(File.ReadAllText(templatePath));
            int columns = grid[0].Length;
            int rows = grid.Length;

            float cellW = settings.CourseSize.x / columns;
            float cellH = settings.CourseSize.y / rows;

            var root = new GameObject(settings.ArenaId);
            AuthoredArena arena = root.AddComponent<AuthoredArena>();

            var so = new SerializedObject(arena);
            so.FindProperty("arenaId").stringValue = settings.ArenaId;
            so.FindProperty("size").vector2Value = settings.CourseSize;
            so.FindProperty("wallHeight").floatValue = settings.WallHeight;
            so.FindProperty("floorThickness").floatValue = 0.5f;
            so.FindProperty("designedCorridorWidth").floatValue = settings.DesignedCorridorWidth;
            so.FindProperty("visualFillPadding").floatValue = settings.VisualFillPadding;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildBorder(root.transform, settings);
            BuildWalls(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildBreakables(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildMegaBlocks(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildRainbowGates(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildDoors(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildRotors(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildRegions(root.transform, grid, columns, rows, cellW, cellH, settings);

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var built = prefab.GetComponent<AuthoredArena>();
            Debug.Log(AuthoredArenaTools.Validate(built, 2.0f));
            Debug.Log(AuthoredArenaSilhouette.Compare(built, CompareTemplate(grid)));

            return prefab;
        }

        /// <summary>
        /// The silhouette checker samples one ring of cells outside the course, where the
        /// boundary-fill masses live - so the compare template is the grid with markers erased
        /// and a solid ring wrapped around it.
        /// </summary>
        private static string[] CompareTemplate(string[] grid)
        {
            int width = grid[0].Length + 2;
            var result = new List<string> { new string(AuthoredArenaSilhouette.Solid, width) };

            foreach (string row in grid)
            {
                // Doors count as solid (their closed state is the map's shape); rotor rooms count
                // as open (the sweep passes, the room is playable space).
                var chars = row.Select(c => c == '#' || c == 'B' || c == 'M' || c == 'C' || c == 'D'
                    ? AuthoredArenaSilhouette.Solid
                    : AuthoredArenaSilhouette.Open);
                result.Add(AuthoredArenaSilhouette.Solid + new string(chars.ToArray()) +
                           AuthoredArenaSilhouette.Solid);
            }

            result.Add(new string(AuthoredArenaSilhouette.Solid, width));
            return result.ToArray();
        }

        // ---------------------------------------------------------------- geometry

        private static void BuildBorder(Transform parent, Settings settings)
        {
            var holder = new GameObject("Border").transform;
            holder.SetParent(parent, false);

            float halfW = settings.CourseSize.x * 0.5f;
            float halfH = settings.CourseSize.y * 0.5f;
            float spanX = halfW + settings.VisualFillPadding;
            float spanZ = halfH + settings.VisualFillPadding;

            FillWall(holder, "Border_Left", Rect.MinMaxRect(-halfW - 1f, -spanZ, -halfW, spanZ),
                FillDirection.MinusX, settings);
            FillWall(holder, "Border_Right", Rect.MinMaxRect(halfW, -spanZ, halfW + 1f, spanZ),
                FillDirection.PlusX, settings);
            FillWall(holder, "Border_Bottom", Rect.MinMaxRect(-spanX, -halfH - 1f, spanX, -halfH),
                FillDirection.MinusZ, settings);
            FillWall(holder, "Border_Top", Rect.MinMaxRect(-spanX, halfH, spanX, halfH + 1f),
                FillDirection.PlusZ, settings);
        }

        private static void BuildWalls(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Walls").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == '#'))
            {
                InternalWall(holder, $"Wall_{index++:D2}", CellRect(cells, columns, rows, cellW, cellH),
                    settings);
            }
        }

        /// <summary>
        /// 'B' rects become internal walls with a hit-count break rule - the reference channel's
        /// block-with-a-countdown, straight from the template.
        /// </summary>
        private static void BuildBreakables(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Breakables").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'B'))
            {
                GameObject go = MakeWall(holder, $"Break_{index:D2}",
                    CellRect(cells, columns, rows, cellW, cellH), settings.WallHeight);

                var wall = go.AddComponent<ArenaWall>();
                var so = new SerializedObject(wall);
                so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
                so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
                so.ApplyModifiedPropertiesWithoutUndo();

                var breakable = go.AddComponent<BreakableWall>();
                var bso = new SerializedObject(breakable);
                bso.FindProperty("id").stringValue = $"{index:D2}";
                bso.FindProperty("condition").enumValueIndex = (int)BreakCondition.TotalHitsAnyRacer;
                bso.FindProperty("requiredHits").intValue = Mathf.Max(1, settings.BreakableHits);
                bso.FindProperty("removalMode").enumValueIndex = (int)WallRemovalMode.ShrinkOut;
                bso.ApplyModifiedPropertiesWithoutUndo();

                index++;
            }
        }

        /// <summary>'M': one merged breakable whose whole point is the enormous countdown on it.</summary>
        private static void BuildMegaBlocks(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("MegaBlocks").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'M'))
            {
                MakeBreakable(holder, $"Mega_{index:D2}", CellRect(cells, columns, rows, cellW, cellH),
                    settings, settings.MegaBlockHits, new Color(1f, 0.55f, 0.85f, 1f));
                index++;
            }
        }

        /// <summary>
        /// 'R': the rect slices into one-cell layers across the axis of travel, each layer its own
        /// breakable in the next rainbow colour - the layer-by-layer chew of the reference videos.
        /// </summary>
        private static void BuildRainbowGates(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            Color[] rainbow =
            {
                new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.9f, 0.15f),
                new Color(0.25f, 0.85f, 0.3f), new Color(0.2f, 0.6f, 1f), new Color(0.6f, 0.3f, 0.95f),
                new Color(0.95f, 0.4f, 0.85f),
            };

            var holder = new GameObject("RainbowGates").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'C'))
            {
                bool sliceColumns = cells.width >= cells.height;
                int layers = sliceColumns ? cells.width : cells.height;

                for (int layer = 0; layer < layers; layer++)
                {
                    RectInt strip = sliceColumns
                        ? new RectInt(cells.xMin + layer, cells.yMin, 1, cells.height)
                        : new RectInt(cells.xMin, cells.yMin + layer, cells.width, 1);

                    MakeBreakable(holder, $"Gate_{index:D2}_{layer:D2}",
                        CellRect(strip, columns, rows, cellW, cellH), settings,
                        settings.RainbowLayerHits, rainbow[layer % rainbow.Length]);
                }

                index++;
            }
        }

        /// <summary>'D': a wall on a clock. Phases stagger by position so doors breathe in sequence.</summary>
        private static void BuildDoors(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Doors").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'D'))
            {
                GameObject go = MakeWall(holder, $"Door_{index:D2}",
                    CellRect(cells, columns, rows, cellW, cellH), settings.WallHeight);

                var wall = go.AddComponent<ArenaWall>();
                var so = new SerializedObject(wall);
                so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
                so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
                so.ApplyModifiedPropertiesWithoutUndo();

                var door = go.AddComponent<CyclingWall>();
                var dso = new SerializedObject(door);
                dso.FindProperty("openDuration").floatValue = 3f;
                dso.FindProperty("closedDuration").floatValue = 3f;
                dso.FindProperty("phaseOffset").floatValue = (cells.xMin * 0.7f + cells.yMin * 0.45f) % 6.8f;
                dso.ApplyModifiedPropertiesWithoutUndo();

                index++;
            }
        }

        /// <summary>
        /// 'O': a rotor room. The rect gets a spinning cross - two bars, one cell thick, spanning
        /// the rect - centred on it. The bars are plain colliders under a RotorObstacle, not
        /// ArenaWalls: the resolve pipeline must never straighten them out.
        /// </summary>
        private static void BuildRotors(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Rotors").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'O'))
            {
                Rect rect = CellRect(cells, columns, rows, cellW, cellH);

                var rotorGo = new GameObject($"Rotor_{index:D2}");
                rotorGo.transform.SetParent(holder, false);
                rotorGo.transform.localPosition = new Vector3(rect.center.x, settings.WallHeight * 0.5f, rect.center.y);

                var rotor = rotorGo.AddComponent<RotorObstacle>();
                var rso = new SerializedObject(rotor);
                rso.FindProperty("degreesPerSecond").floatValue = index % 2 == 0 ? 24f : -24f;
                rso.FindProperty("phaseDegrees").floatValue = index * 45f;
                rso.ApplyModifiedPropertiesWithoutUndo();

                float span = Mathf.Min(rect.width, rect.height);
                float thickness = Mathf.Min(cellW, cellH);

                GameObject barA = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barA.name = "BarA";
                barA.transform.SetParent(rotorGo.transform, false);
                barA.transform.localScale = new Vector3(span, settings.WallHeight, thickness);

                GameObject barB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barB.name = "BarB";
                barB.transform.SetParent(rotorGo.transform, false);
                barB.transform.localScale = new Vector3(thickness, settings.WallHeight, span);

                index++;
            }
        }

        private static void MakeBreakable(Transform holder, string name, Rect footprint,
            Settings settings, int hits, Color accent)
        {
            GameObject go = MakeWall(holder, name, footprint, settings.WallHeight);

            var wall = go.AddComponent<ArenaWall>();
            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
            so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
            so.ApplyModifiedPropertiesWithoutUndo();

            var breakable = go.AddComponent<BreakableWall>();
            var bso = new SerializedObject(breakable);
            bso.FindProperty("id").stringValue = name;
            bso.FindProperty("condition").enumValueIndex = (int)BreakCondition.TotalHitsAnyRacer;
            bso.FindProperty("requiredHits").intValue = Mathf.Max(1, hits);
            bso.FindProperty("removalMode").enumValueIndex = (int)WallRemovalMode.ShrinkOut;
            bso.FindProperty("accentOverride").colorValue = accent;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRegions(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Regions").transform;
            holder.SetParent(parent, false);

            foreach (var (marker, name) in new[]
                     { ('L', "SpawnArea_Left"), ('R', "SpawnArea_Right") })
            {
                foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == marker))
                {
                    Region<SpawnArea>(holder, name, CellRect(cells, columns, rows, cellW, cellH));
                }
            }

            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'G'))
            {
                GameObject goal = Region<GoalArea>(holder, "GoalArea",
                    CellRect(cells, columns, rows, cellW, cellH));

                var so = new SerializedObject(goal.GetComponent<GoalArea>());
                so.FindProperty("retireOnReach").boolValue = true;
                so.FindProperty("entryFraction").floatValue = 0.5f;
                so.FindProperty("visualType").enumValueIndex = (int)GoalVisualType.FinishPad;
                so.FindProperty("visualColor").colorValue = settings.GoalColor;
                so.FindProperty("visualEmission").floatValue = settings.GoalEmission;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'X'))
            {
                GameObject hazard = Region<HazardArea>(holder, "Hazard",
                    CellRect(cells, columns, rows, cellW, cellH));

                var so = new SerializedObject(hazard.GetComponent<HazardArea>());
                so.FindProperty("damagePerSecond").floatValue = settings.HazardDamagePerSecond;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'W'))
            {
                Region<WeaponSpawnArea>(holder, "WeaponArea",
                    CellRect(cells, columns, rows, cellW, cellH));
            }

            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'F'))
            {
                Region<FoodArea>(holder, "FoodArea",
                    CellRect(cells, columns, rows, cellW, cellH));
            }
        }

        /// <summary>
        /// Greedy rectangle merge: horizontal runs per row, then identical runs stacked across
        /// consecutive rows become one rect. Columns and bars come out as single wall objects, which
        /// is what keeps an authored map a handful of meaningful masses instead of cell confetti.
        /// </summary>
        private static List<RectInt> MergeRects(string[] grid, int columns, int rows,
            System.Func<char, bool> match)
        {
            var open = new List<RectInt>();
            var closed = new List<RectInt>();

            for (int y = 0; y < rows; y++)
            {
                var runs = new List<RectInt>();
                int x = 0;
                while (x < columns)
                {
                    if (!match(grid[y][x])) { x++; continue; }

                    int start = x;
                    while (x < columns && match(grid[y][x])) x++;
                    runs.Add(new RectInt(start, y, x - start, 1));
                }

                var next = new List<RectInt>();
                foreach (RectInt run in runs)
                {
                    int extended = open.FindIndex(r => r.xMin == run.xMin && r.xMax == run.xMax);
                    if (extended >= 0)
                    {
                        RectInt grown = open[extended];
                        grown.height += 1;
                        next.Add(grown);
                        open.RemoveAt(extended);
                    }
                    else
                    {
                        next.Add(run);
                    }
                }

                closed.AddRange(open);
                open = next;
            }

            closed.AddRange(open);
            return closed;
        }

        /// <summary>Grid cells to world XZ. Row 0 is the top of the map (+Z), like the file reads.</summary>
        private static Rect CellRect(RectInt cells, int columns, int rows, float cellW, float cellH)
        {
            float xMin = (cells.xMin - columns * 0.5f) * cellW;
            float xMax = (cells.xMax - columns * 0.5f) * cellW;
            float zMax = (rows * 0.5f - cells.yMin) * cellH;
            float zMin = (rows * 0.5f - cells.yMax) * cellH;
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        // ---------------------------------------------------------------- scene objects

        private static void FillWall(Transform parent, string name, Rect footprint,
            FillDirection direction, Settings settings)
        {
            GameObject go = MakeWall(parent, name, footprint, settings.WallHeight);
            var wall = go.AddComponent<ArenaWall>();

            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.BoundaryFill;
            so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.ExtendToArenaBounds;
            so.FindProperty("fillDirection").enumValueIndex = (int)direction;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InternalWall(Transform parent, string name, Rect footprint,
            Settings settings)
        {
            GameObject go = MakeWall(parent, name, footprint, settings.WallHeight);
            var wall = go.AddComponent<ArenaWall>();

            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
            so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject MakeWall(Transform parent, string name, Rect footprint, float height)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(footprint.center.x, height * 0.5f, footprint.center.y);
            go.transform.localScale = new Vector3(footprint.width, height, footprint.height);
            return go;
        }

        private static GameObject Region<T>(Transform parent, string name, Rect footprint)
            where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(footprint.center.x, 0.2f, footprint.center.y);
            go.transform.localScale = new Vector3(footprint.width, 1f, footprint.height);
            go.AddComponent<T>();
            return go;
        }
    }
}
