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
    /// rect slices into coloured one-cell layers, each openable only by the racer of that colour,
    /// a couple of hits each), 'N' is a neutral white glass pane (anyone may hit it, but it takes
    /// many hits), 'D' is a cycling door that
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
            // One heart per second: a straight dash across a 2-cell band costs a quarter heart, but
            // bouncing around on it for three seconds is death. At half this the floor was decor.
            public float HazardDamagePerSecond = 1.0f;

            /// <summary>Hits each breakable ('B') wall takes before it opens.</summary>
            public int BreakableHits = 40;

            /// <summary>Hits for a mega block ('M') - the four-digit-counter centrepiece.</summary>
            public int MegaBlockHits = 600;

            /// <summary>Hits per colour-gated rainbow layer ('C') - only the matching racer counts.</summary>
            public int RainbowLayerHits = 2;

            /// <summary>Hearts a saw blade takes per cut. Half a heart everywhere except the Saw format, so hazards wear racers down and knives finish them.</summary>
            public float SawDamage = 0.5f;

            /// <summary>Hits for a neutral white pane ('N') - anybody may break it, so it takes many.</summary>
            public int NeutralGlassHits = 10;

            /// <summary>
            /// Above 0, every merged 'B' mass is cut into boulders of at most this many cells a side,
            /// each its own breakable. A rock field then gets dug through tunnel by tunnel instead of
            /// vanishing as one slab. 0 keeps the merged wall (a team-war pen door wants that).
            /// </summary>
            public int RockTileCells = 0;

            /// <summary>Hearts a rotor bar ('O') takes off a racer per sweep. 0 = push only (a rotor
            /// is a spinning pusher; the saw blade 'S' is the thing that cuts).</summary>
            public float RotorDamage = 0f;
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
            BuildNeutralGlass(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildDoors(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildRotors(root.transform, grid, columns, rows, cellW, cellH, settings);
            BuildRegions(root.transform, grid, columns, rows, cellW, cellH, settings);
            AsciiDeviceBuilder.Build(root.transform, grid, columns, rows, cellW, cellH, settings);

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
                var chars = row.Select(c => c == '#' || c == 'B' || c == 'M' || c == 'C' ||
                                            c == 'N' || c == 'D' || c == 'K' || c == '?'
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
            foreach (RectInt merged in MergeRects(grid, columns, rows, c => c == 'B'))
            foreach (RectInt cells in TileRect(merged, settings.RockTileCells))
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

                AddRockVisual(go, index);
                MarkCustomVisual(go);
                MarkRock(go);

                index++;
            }
        }

        /// <summary>Cuts a rect into tiles of at most <paramref name="tile"/> cells a side; 0 = as is.</summary>
        private static IEnumerable<RectInt> TileRect(RectInt rect, int tile)
        {
            if (tile <= 0)
            {
                yield return rect;
                yield break;
            }

            for (int y = rect.yMin; y < rect.yMax; y += tile)
            for (int x = rect.xMin; x < rect.xMax; x += tile)
            {
                yield return new RectInt(x, y, Mathf.Min(tile, rect.xMax - x), Mathf.Min(tile, rect.yMax - y));
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
                GameObject go = MakeBreakable(holder, $"Mega_{index:D2}",
                    CellRect(cells, columns, rows, cellW, cellH),
                    settings, settings.MegaBlockHits, new Color(1f, 0.55f, 0.85f, 1f));

                AddRockVisual(go);
                MarkCustomVisual(go);
                MarkRock(go);
                index++;
            }
        }

        /// <summary>
        /// The colour-gate palette. These are the racer palette colours verbatim (VisualTheme),
        /// so a red pane is opened by the red cube and nobody else - the gate reads as "this one
        /// is yours" at a glance.
        /// </summary>
        private static readonly Color[] GateColors =
        {
            new Color(0.95f, 0.16f, 0.16f), // red
            new Color(0.98f, 0.45f, 0.10f), // orange
            new Color(0.98f, 0.86f, 0.14f), // yellow
            new Color(0.16f, 0.85f, 0.24f), // green
            new Color(0.18f, 0.36f, 0.98f), // blue
            new Color(0.20f, 0.90f, 0.92f), // cyan
            new Color(0.85f, 0.20f, 0.90f), // magenta
        };

        /// <summary>
        /// 'C': the rect slices into one-cell layers across the axis of travel, each layer its own
        /// breakable in the next rainbow colour - the layer-by-layer chew of the reference videos.
        /// Each layer is COLOUR GATED: only the racer wearing that colour can chip it, and it
        /// gives way in a couple of hits. The white 'N' panes are the opposite trade.
        /// </summary>
        private static void BuildRainbowGates(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            Color[] rainbow = GateColors;

            var holder = new GameObject("RainbowGates").transform;
            holder.SetParent(parent, false);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'C'))
            {
                // Layers stack along the wall's THICKNESS, so each one is a full sheet across
                // the path and the field chews through them one behind the other. Slicing the
                // long way instead produced a ladder of little bands: break one and a racer
                // slips through the hole while the rest are never touched.
                bool sliceColumns = cells.width <= cells.height;
                int layers = sliceColumns ? cells.width : cells.height;

                for (int layer = 0; layer < layers; layer++)
                {
                    RectInt strip = sliceColumns
                        ? new RectInt(cells.xMin + layer, cells.yMin, 1, cells.height)
                        : new RectInt(cells.xMin, cells.yMin + layer, cells.width, 1);

                    int colorIndex = layer % rainbow.Length;
                    GameObject go = MakeBreakable(holder, $"Gate_{index:D2}_{layer:D2}",
                        CellRect(strip, columns, rows, cellW, cellH), settings,
                        settings.RainbowLayerHits, rainbow[colorIndex]);

                    // Colour gate: only the matching racer chips this pane, and only a couple of
                    // hits are needed - the whole field has to sort itself out by colour.
                    var gso = new SerializedObject(go.GetComponent<BreakableWall>());
                    gso.FindProperty("condition").enumValueIndex = (int)BreakCondition.RequiredColorHitCount;
                    gso.FindProperty("requiredColor").colorValue = rainbow[colorIndex];
                    gso.FindProperty("colorTolerance").floatValue = 0.2f;
                    gso.ApplyModifiedPropertiesWithoutUndo();

                    // Stylized glass panel, one colour per layer - the full spectrum cycles.
                    Material glass = GetGlassMaterial(rainbow[colorIndex], colorIndex);
                    if (glass != null)
                    {
                        go.GetComponent<MeshRenderer>().sharedMaterial = glass;
                        MarkCustomVisual(go);
                    }
                }

                index++;
            }
        }

        /// <summary>
        /// 'N': neutral white glass. The opposite trade to a colour gate - anybody may hit it, so
        /// it takes many hits to give way. A rect stays whole (no colour slicing) because there is
        /// nothing to colour: it is one thick pane the whole field grinds down together.
        /// </summary>
        private static void BuildNeutralGlass(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("GlassPanes").transform;
            holder.SetParent(parent, false);

            var white = new Color(0.93f, 0.97f, 1f);

            int index = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == 'N'))
            {
                GameObject go = MakeBreakable(holder, $"Pane_{index:D2}",
                    CellRect(cells, columns, rows, cellW, cellH), settings,
                    settings.NeutralGlassHits, white);

                Material glass = GetGlassMaterial(white, 99);
                if (glass != null)
                {
                    go.GetComponent<MeshRenderer>().sharedMaterial = glass;
                    MarkCustomVisual(go);
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

                // A plain amber slab: from the top-down camera a door mesh is just an edge, while a
                // bright block that sinks into the floor is unmistakable.
                go.GetComponent<MeshRenderer>().sharedMaterial =
                    AsciiDeviceBuilder.GetPlateMaterialShared("CyclingDoorBlock", new Color(0.95f, 0.6f, 0.15f), new Color(1f, 0.55f, 0.1f) * 0.7f);
                MarkCustomVisual(go);

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
                // Fast enough to be a real threat: a racer at speed 10 cannot ignore a bar
                // sweeping at 45 deg/s. The old 24 read as decoration.
                rso.FindProperty("degreesPerSecond").floatValue = index % 2 == 0 ? 45f : -45f;
                rso.FindProperty("phaseDegrees").floatValue = index * 45f;
                rso.FindProperty("damagePerHit").floatValue = settings.RotorDamage;
                rso.ApplyModifiedPropertiesWithoutUndo();

                // The 'O' rect IS the sweep: mark the whole rotor room in the template and the
                // bars fill it (minus a hair so they never scrape the room's walls).
                float span = Mathf.Min(rect.width, rect.height) * 0.96f;
                float thickness = Mathf.Min(cellW, cellH);

                Material blade = null;   // rotors keep the wall look; the blade material is for saw blades

                GameObject barA = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barA.name = "BarA";
                barA.transform.SetParent(rotorGo.transform, false);
                barA.transform.localScale = new Vector3(span, settings.WallHeight, thickness);
                if (blade != null) barA.GetComponent<MeshRenderer>().sharedMaterial = blade;

                GameObject barB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barB.name = "BarB";
                barB.transform.SetParent(rotorGo.transform, false);
                barB.transform.localScale = new Vector3(thickness, settings.WallHeight, span);
                if (blade != null) barB.GetComponent<MeshRenderer>().sharedMaterial = blade;

                index++;
            }
        }

        private static GameObject MakeBreakable(Transform holder, string name, Rect footprint,
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

            return go;
        }

        private static void BuildRegions(Transform parent, string[] grid, int columns, int rows,
            float cellW, float cellH, Settings settings)
        {
            var holder = new GameObject("Regions").transform;
            holder.SetParent(parent, false);

            foreach (var (marker, name) in new[]
                     { ('L', "SpawnArea_Left"), ('R', "SpawnArea_Right"),
                       ('A', "SpawnArea_Top"), ('Z', "SpawnArea_Bottom") })
            {
                foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == marker))
                {
                    Region<SpawnArea>(holder, name, CellRect(cells, columns, rows, cellW, cellH));
                }
            }

            // ? Lucky Block crates: one-hit breakables that roll loot for whoever cracks them.
            int crateIndex = 0;
            foreach (RectInt cells in MergeRects(grid, columns, rows, c => c == '?'))
            {
                GameObject crate = MakeBreakable(holder, $"Crate_{crateIndex:D2}",
                    CellRect(cells, columns, rows, cellW, cellH), settings, 1, new Color(1f, 0.8f, 0.15f, 1f));
                crate.AddComponent<LootCrate>();
                var cso = new SerializedObject(crate.GetComponent<BreakableWall>());
                cso.FindProperty("removalMode").enumValueIndex = (int)WallRemovalMode.ShrinkOut;
                cso.FindProperty("removalDuration").floatValue = 0.25f;
                cso.ApplyModifiedPropertiesWithoutUndo();
                var mark = new GameObject("Mark");
                mark.transform.SetParent(crate.transform, false);
                mark.transform.localPosition = new Vector3(0f, 0.51f, 0f);
                mark.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var text = mark.AddComponent<TextMesh>();
                text.text = "?";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = 0.035f;
                text.fontStyle = FontStyle.Bold;
                text.color = new Color(0.1f, 0.05f, 0f);
                crateIndex++;
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
        internal static List<RectInt> MergeRects(string[] grid, int columns, int rows,
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
        internal static Rect CellRect(RectInt cells, int columns, int rows, float cellW, float cellH)
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

        internal static GameObject MakeWall(Transform parent, string name, Rect footprint, float height)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(footprint.center.x, height * 0.5f, footprint.center.y);
            go.transform.localScale = new Vector3(footprint.width, height, footprint.height);
            return go;
        }

        private const string BladeMaterialPath = "Assets/CubeSim/Visuals/Rotors/Blade.mat";

        /// <summary>
        /// A rotor bar is a blade now, and it has to read as one from above: dark steel with a hot
        /// red edge glow, nothing like the quarry gray of a wall it can be mistaken for.
        /// </summary>
        private static Material GetBladeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ??
                            Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory("Assets/CubeSim/Visuals/Rotors");
            material = new Material(shader) { name = "Blade" };
            material.SetColor("_BaseColor", new Color(0.62f, 0.08f, 0.1f));
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.3f, 0.03f, 0.05f));
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.9f, 0.05f, 0.05f) * 0.6f);
            }

            AssetDatabase.CreateAsset(material, BladeMaterialPath);
            return material;
        }

        private const string RockModelPath = "Assets/KenneyDungeon/rocks.fbx";
        private const string RockMaterialPath = "Assets/CubeSim/Visuals/Rocks/RockStone.mat";
        private const string GlassFolder = "Assets/CubeSim/Visuals/Glass";

        /// <summary>
        /// Stone-gray toon material for the rock models. The pack's colormap paints these rocks
        /// tomato red, which reads as a hazard from above - a flat quarry gray does not.
        /// </summary>
        private static Material GetRockMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ??
                            Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory("Assets/CubeSim/Visuals/Rocks");
            material = new Material(shader) { name = "RockStone" };
            material.SetColor("_BaseColor", new Color(0.56f, 0.54f, 0.5f));
            if (material.HasProperty("_ShadowColor"))
            {
                material.SetColor("_ShadowColor", new Color(0.3f, 0.29f, 0.28f));
            }

            AssetDatabase.CreateAsset(material, RockMaterialPath);
            return material;
        }

        /// <summary>
        /// Swaps a breakable's plain box look for the Kenney rock model: the collider cube stays
        /// (and keeps driving the shrink-out), its renderer goes dark, and the rock is fitted into
        /// the cube's unit space so any footprint reads as a boulder of that size.
        /// </summary>
        private static void AddRockVisual(GameObject wallGo, int variant = 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath);
            Material material = GetRockMaterial();
            if (prefab == null || material == null)
            {
                Debug.LogWarning("[CubeSim] Rock model or material missing; breakable keeps the box look.");
                return;
            }

            wallGo.GetComponent<MeshRenderer>().enabled = false;

            // Measure the model's native bounds BEFORE parenting - under the wall cube's
            // non-uniform scale, world bounds lie about the model's own size.
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rock.name = "RockVisual";
            rock.transform.position = Vector3.zero;
            rock.transform.rotation = Quaternion.identity;
            rock.transform.localScale = Vector3.one;

            var renderers = rock.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(rock);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials =
                    System.Linq.Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            // Map the native bounds onto the parent cube's [-0.5, 0.5] space, so the rock exactly
            // fills the wall's box no matter the footprint. Parent scale does the rest.
            Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.001f);
            // A hair over the footprint so neighbouring boulders overlap instead of showing seams;
            // height stays exact. Mirroring by index breaks the copy-paste look of a rock field.
            var fit = new Vector3(1.08f / size.x, 1f / size.y, 1.08f / size.z);
            if ((variant & 1) != 0) fit.x = -fit.x;
            if ((variant & 2) != 0) fit.z = -fit.z;

            rock.transform.SetParent(wallGo.transform, false);
            rock.transform.localRotation = Quaternion.identity;
            rock.transform.localScale = fit;
            rock.transform.localPosition = -Vector3.Scale(bounds.center, fit);

            foreach (Collider stray in rock.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(stray);
            }
        }

        /// <summary>One glass material asset per gate colour, created on demand and reused.</summary>
        private static Material GetGlassMaterial(Color color, int colorIndex)
        {
            Directory.CreateDirectory(GlassFolder);
            string path = $"{GlassFolder}/Glass_{colorIndex:D2}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("CubeSim/Glass");
                if (shader == null)
                {
                    Debug.LogWarning("[CubeSim] CubeSim/Glass shader missing; gate keeps the box look.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_TintColor", new Color(color.r, color.g, color.b, 0.5f));
            material.SetColor("_FresnelColor", Color.Lerp(color, Color.white, 0.6f) * 1.6f);
            material.SetFloat("_FresnelStrength", 1.1f);
            material.SetFloat("_SpecStrength", 1.2f);
            material.SetFloat("_RefractStrength", 0.14f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void MarkRock(GameObject wallGo)
        {
            var breakable = wallGo.GetComponent<BreakableWall>();
            if (breakable == null) return;
            var bso = new SerializedObject(breakable);
            bso.FindProperty("rock").boolValue = true;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void MarkCustomVisual(GameObject wallGo)
        {
            var wall = wallGo.GetComponent<ArenaWall>();
            if (wall != null)
            {
                var wso = new SerializedObject(wall);
                wso.FindProperty("keepBakedMaterial").boolValue = true;
                wso.ApplyModifiedPropertiesWithoutUndo();
            }

            var breakable = wallGo.GetComponent<BreakableWall>();
            if (breakable == null) return;

            var so = new SerializedObject(breakable);
            so.FindProperty("customVisual").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GameObject Region<T>(Transform parent, string name, Rect footprint)
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
