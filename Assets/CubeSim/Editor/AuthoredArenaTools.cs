using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Scene-view authoring helpers. Building a map is placement work, so these just create the
    /// right component on a correctly sized object and put it under the current selection.
    /// </summary>
    public static class AuthoredArenaTools
    {
        private const string Menu = "CubeSim/Authoring/";

        [MenuItem(Menu + "Create Authored Arena Root", priority = 100)]
        public static void CreateArenaRoot()
        {
            var go = new GameObject("AuthoredArena");
            go.AddComponent<AuthoredArena>();
            Undo.RegisterCreatedObjectUndo(go, "Create Authored Arena");
            Selection.activeGameObject = go;
        }

        [MenuItem(Menu + "Add Internal Wall", priority = 110)]
        public static void AddInternalWall() => CreateWall(ArenaWallType.Internal, WallFillMode.FixedThickness);

        [MenuItem(Menu + "Add Boundary Fill Wall", priority = 111)]
        public static void AddBoundaryFillWall() => CreateWall(ArenaWallType.BoundaryFill, WallFillMode.ExtendToArenaBounds);

        [MenuItem(Menu + "Add Spawn Area", priority = 120)]
        public static void AddSpawnArea() => CreateRegion<SpawnArea>("SpawnArea", new Vector3(6f, 1f, 6f));

        [MenuItem(Menu + "Add Goal Area", priority = 121)]
        public static void AddGoalArea() => CreateRegion<GoalArea>("GoalArea", new Vector3(6f, 1f, 5f));

        [MenuItem(Menu + "Add Weapon Spawn Area", priority = 122)]
        public static void AddWeaponArea() => CreateRegion<WeaponSpawnArea>("WeaponSpawnArea", new Vector3(5f, 1f, 4f));

        [MenuItem(Menu + "Add Hazard Area", priority = 123)]
        public static void AddHazard() => CreateRegion<HazardArea>("HazardArea", new Vector3(4f, 1f, 3f));

        [MenuItem(Menu + "Create Pressure Track", priority = 130)]
        public static void CreatePressureTrack()
        {
            var go = new GameObject("PressureTrack");
            go.AddComponent<PressureTrack>();
            Parent(go);
            Undo.RegisterCreatedObjectUndo(go, "Create Pressure Track");
            Selection.activeGameObject = go;
        }

        [MenuItem(Menu + "Add Pressure Segment", priority = 131)]
        public static void AddPressureSegment()
        {
            PressureTrack track = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PressureTrack>()
                : null;

            if (track == null)
            {
                Debug.LogWarning("[CubeSim] Select a PressureTrack (or one of its segments) first.");
                return;
            }

            int index = track.transform.childCount;
            var go = new GameObject($"Segment_{index:D2}");
            go.transform.SetParent(track.transform, false);
            go.transform.localScale = new Vector3(10f, 1f, 6f);
            go.AddComponent<PressureSegment>();

            Undo.RegisterCreatedObjectUndo(go, "Add Pressure Segment");
            Selection.activeGameObject = go;
        }

        private static void CreateWall(ArenaWallType type, WallFillMode fill)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = type == ArenaWallType.BoundaryFill ? "FillWall" : "Wall";
            go.transform.localScale = new Vector3(8f, 2.6f, 1.2f);

            ArenaWall wall = go.AddComponent<ArenaWall>();
            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)type;
            so.FindProperty("fillMode").enumValueIndex = (int)fill;
            so.ApplyModifiedPropertiesWithoutUndo();

            Parent(go);
            Undo.RegisterCreatedObjectUndo(go, "Add Arena Wall");
            Selection.activeGameObject = go;
        }

        private static void CreateRegion<T>(string name, Vector3 size) where T : Component
        {
            var go = new GameObject(name);
            go.transform.localScale = size;
            go.AddComponent<T>();

            Parent(go);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            Selection.activeGameObject = go;
        }

        private static void Parent(GameObject go)
        {
            if (Selection.activeGameObject == null) return;

            AuthoredArena arena = Selection.activeGameObject.GetComponentInParent<AuthoredArena>();
            if (arena != null) go.transform.SetParent(arena.transform, true);
        }

        // ---------------------------------------------------------------- validation

        [MenuItem(Menu + "Validate Selected Authored Arena", priority = 200)]
        public static void ValidateSelected()
        {
            AuthoredArena arena = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<AuthoredArena>()
                : Object.FindFirstObjectByType<AuthoredArena>();

            if (arena == null)
            {
                Debug.LogWarning("[CubeSim] No AuthoredArena found to validate.");
                return;
            }

            Debug.Log(Validate(arena, 1.1f));
        }

        /// <summary>
        /// Checks the problems that actually break a map. Ambiguous design choices are warnings, not
        /// errors - unusual geometry is allowed unless there is evidence it cannot work.
        /// </summary>
        public static string Validate(AuthoredArena arena, float racerDiameter)
        {
            arena.Collect();

            var sb = new StringBuilder($"[CubeSim] Validating authored arena '{arena.ArenaId}'\n");
            int errors = 0, warnings = 0;

            List<Rect> walls = arena.ResolveWalls(0f, false);
            Rect bounds = arena.Bounds;
            float half = racerDiameter * 0.5f;

            // Geometry sanity.
            for (int i = 0; i < walls.Count; i++)
            {
                if (walls[i].width <= 0.01f || walls[i].height <= 0.01f)
                {
                    sb.AppendLine($"  ERROR wall '{arena.Walls[i].name}' has zero size."); errors++;
                }
            }

            for (int i = 0; i < arena.Walls.Count; i++)
            {
                ArenaWall wall = arena.Walls[i];
                if (wall.WallType != ArenaWallType.BoundaryFill) continue;

                // Fill walls are allowed to run out to the visual fill bounds - that padding is what
                // makes the map read as solid rock rather than a bar on a floor. Anything wider than
                // that is a real mistake.
                Rect fill = arena.VisualFillBounds;
                Rect resolved = wall.ResolvedFootprint;
                if (resolved.width > fill.width + 0.5f || resolved.height > fill.height + 0.5f)
                {
                    sb.AppendLine($"  WARN fill wall '{wall.name}' extends past the visual fill bounds."); warnings++;
                }
            }

            errors += CheckRegions(sb, "spawn area", arena.SpawnAreas, walls, bounds, half, ref warnings);
            errors += CheckRegions(sb, "goal area", arena.GoalAreas, walls, bounds, half, ref warnings);
            errors += CheckRegions(sb, "weapon area", arena.WeaponAreas, walls, bounds, half, ref warnings);

            if (arena.SpawnAreas.Count == 0) { sb.AppendLine("  ERROR no SpawnArea; racers have nowhere to start."); errors++; }
            if (arena.GoalAreas.Count == 0) { sb.AppendLine("  WARN no GoalArea; this map cannot use WinCondition.ReachGoal."); warnings++; }
            if (arena.WeaponAreas.Count == 0) { sb.AppendLine("  WARN no WeaponSpawnArea; weapons fall back to the arena centre."); warnings++; }

            // Pressure track.
            if (arena.Track == null)
            {
                sb.AppendLine("  WARN no PressureTrack; this map cannot use PressureMode.AuthoredTrack."); warnings++;
            }
            else
            {
                IReadOnlyList<PressureSegment> segments = arena.Track.Segments;
                if (segments.Count == 0) { sb.AppendLine("  ERROR PressureTrack has no segments."); errors++; }

                for (int i = 0; i < segments.Count; i++)
                {
                    PressureSegment s = segments[i];
                    if (s.FillLength <= 0.01f) { sb.AppendLine($"  ERROR segment '{s.name}' has zero fill length."); errors++; }

                    if (i == 0) continue;

                    // Consecutive segments should touch or overlap, otherwise the route has a hole
                    // the pressure would skip over.
                    Rect a = segments[i - 1].Footprint;
                    Rect b = s.Footprint;
                    if (!Touches(a, b, 0.75f))
                    {
                        sb.AppendLine($"  WARN segments '{segments[i - 1].name}' and '{s.name}' do not meet; " +
                                      "the route has a gap."); warnings++;
                    }
                }

                sb.AppendLine($"  route: {segments.Count} segments, ~{arena.Track.EstimateDuration():F0}s to fill.");
            }

            if (arena.DesignedCorridorWidth < racerDiameter * 1.5f)
            {
                sb.AppendLine($"  WARN designed corridor width {arena.DesignedCorridorWidth:F2} is tight for a " +
                              $"{racerDiameter:F2} racer."); warnings++;
            }

            errors += CheckReachability(sb, arena, walls, bounds, racerDiameter, ref warnings);

            sb.AppendLine($"  walls={walls.Count} spawns={arena.SpawnAreas.Count} goals={arena.GoalAreas.Count} " +
                          $"weaponAreas={arena.WeaponAreas.Count} hazards={arena.Hazards.Count}");
            sb.AppendLine($"  RESULT errors={errors} warnings={warnings}");
            return sb.ToString();
        }

        /// <summary>
        /// Flood fills the walkable space from every spawn area and checks that the goal and weapon
        /// areas can actually be reached. Eyeballing a map is not enough - a set of long columns can
        /// seal the centre off completely while still looking fine from above.
        /// </summary>
        private static int CheckReachability(StringBuilder sb, AuthoredArena arena, List<Rect> walls,
            Rect bounds, float racerDiameter, ref int warnings)
        {
            // Breakable walls are doors: sealed now, open once their rule is met. Treating them as
            // solid would fail every map whose goal deliberately sits behind a block to smash.
            var blocking = new List<Rect>(walls.Count);
            for (int i = 0; i < arena.Walls.Count; i++)
            {
                ArenaWall wall = arena.Walls[i];
                if (wall == null || !wall.gameObject.activeSelf) continue;
                if (wall.GetComponent<BreakableWall>() != null) continue;
                if (wall.GetComponent<CyclingWall>() != null) continue;   // opens on a clock
                blocking.Add(wall.ResolvedFootprint);
            }

            walls = blocking;

            // Quarter-diameter cells, or a passage that is legal but narrower than the sampling
            // phase reads as sealed: a 2.8m mouth for a 2.0m racer leaves a 0.8m free lane, and a
            // half-diameter grid can step straight over it. The clearance backs off by the skin the
            // mover keeps, matching what the solver actually accepts as legal.
            float cell = Mathf.Max(0.25f, racerDiameter * 0.25f);
            float clearance = racerDiameter * 0.5f - 0.05f;

            int columns = Mathf.CeilToInt(bounds.width / cell);
            int rows = Mathf.CeilToInt(bounds.height / cell);
            if (columns <= 2 || rows <= 2) return 0;

            var free = new bool[columns, rows];
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    var p = new Vector2(bounds.xMin + (x + 0.5f) * cell, bounds.yMin + (y + 0.5f) * cell);
                    free[x, y] = !BlockedForRacer(p, clearance, walls);
                }
            }

            var visited = new bool[columns, rows];
            var queue = new Queue<Vector2Int>();

            for (int i = 0; i < arena.SpawnAreas.Count; i++)
            {
                SeedRegion(arena.SpawnAreas[i].Footprint, bounds, cell, columns, rows, free, visited, queue);
            }

            if (queue.Count == 0)
            {
                sb.AppendLine("  ERROR no walkable cell inside any spawn area.");
                return 1;
            }

            var offsets = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                for (int i = 0; i < offsets.Length; i++)
                {
                    int nx = c.x + offsets[i].x;
                    int ny = c.y + offsets[i].y;
                    if (nx < 0 || ny < 0 || nx >= columns || ny >= rows) continue;
                    if (visited[nx, ny] || !free[nx, ny]) continue;

                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            int errors = 0;
            errors += CheckRegionReachable(sb, "goal area", arena.GoalAreas, bounds, cell, columns, rows, visited);
            errors += CheckRegionReachable(sb, "weapon area", arena.WeaponAreas, bounds, cell, columns, rows, visited);

            int reachable = 0, total = 0;
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (!free[x, y]) continue;
                    total++;
                    if (visited[x, y]) reachable++;
                }
            }

            float share = total == 0 ? 0f : (float)reachable / total;
            sb.AppendLine($"  reachable open space from spawns: {share * 100f:F0}%");
            if (share < 0.75f)
            {
                sb.AppendLine("  WARN a quarter or more of the open space is walled off from the start.");
                warnings++;
            }

            return errors;
        }

        private static void SeedRegion(Rect region, Rect bounds, float cell, int columns, int rows,
            bool[,] free, bool[,] visited, Queue<Vector2Int> queue)
        {
            int xMin = Mathf.Clamp(Mathf.FloorToInt((region.xMin - bounds.xMin) / cell), 0, columns - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((region.xMax - bounds.xMin) / cell), 0, columns - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt((region.yMin - bounds.yMin) / cell), 0, rows - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt((region.yMax - bounds.yMin) / cell), 0, rows - 1);

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    if (!free[x, y] || visited[x, y]) continue;
                    visited[x, y] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                }
            }
        }

        private static int CheckRegionReachable<T>(StringBuilder sb, string label, List<T> regions,
            Rect bounds, float cell, int columns, int rows, bool[,] visited) where T : ArenaRegion
        {
            if (regions == null) return 0;

            int errors = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                Rect r = regions[i].Footprint;
                bool reached = false;

                int xMin = Mathf.Clamp(Mathf.FloorToInt((r.xMin - bounds.xMin) / cell), 0, columns - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt((r.xMax - bounds.xMin) / cell), 0, columns - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt((r.yMin - bounds.yMin) / cell), 0, rows - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt((r.yMax - bounds.yMin) / cell), 0, rows - 1);

                for (int x = xMin; x <= xMax && !reached; x++)
                {
                    for (int y = yMin; y <= yMax && !reached; y++) reached = visited[x, y];
                }

                if (!reached)
                {
                    sb.AppendLine($"  ERROR {label} '{regions[i].name}' cannot be reached from any spawn area.");
                    errors++;
                }
            }

            return errors;
        }

        private static bool BlockedForRacer(Vector2 point, float clearance, List<Rect> walls)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                Rect r = walls[i];
                if (point.x + clearance > r.xMin && point.x - clearance < r.xMax &&
                    point.y + clearance > r.yMin && point.y - clearance < r.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CheckRegions<T>(StringBuilder sb, string label, List<T> regions,
            List<Rect> walls, Rect bounds, float halfExtent, ref int warnings) where T : ArenaRegion
        {
            int errors = 0;
            if (regions == null) return 0;

            for (int i = 0; i < regions.Count; i++)
            {
                Rect r = regions[i].Footprint;

                if (r.width < halfExtent * 2f || r.height < halfExtent * 2f)
                {
                    sb.AppendLine($"  ERROR {label} '{regions[i].name}' is smaller than a racer."); errors++;
                }

                if (!Contains(bounds, r))
                {
                    sb.AppendLine($"  ERROR {label} '{regions[i].name}' lies outside the arena bounds."); errors++;
                }

                // Fully buried in solid geometry is unusable; partial overlap is a design choice.
                float covered = CoveredFraction(r, walls);
                if (covered > 0.98f)
                {
                    sb.AppendLine($"  ERROR {label} '{regions[i].name}' is completely inside wall geometry."); errors++;
                }
                else if (covered > 0.5f)
                {
                    sb.AppendLine($"  WARN {label} '{regions[i].name}' is {covered * 100f:F0}% inside walls."); warnings++;
                }
            }

            return errors;
        }

        /// <summary>Approximate share of a rect buried in walls, by point sampling.</summary>
        private static float CoveredFraction(Rect region, List<Rect> walls)
        {
            const int Steps = 7;
            int inside = 0, total = 0;

            for (int x = 0; x < Steps; x++)
            {
                for (int y = 0; y < Steps; y++)
                {
                    var p = new Vector2(
                        Mathf.Lerp(region.xMin, region.xMax, (x + 0.5f) / Steps),
                        Mathf.Lerp(region.yMin, region.yMax, (y + 0.5f) / Steps));

                    total++;
                    for (int w = 0; w < walls.Count; w++)
                    {
                        if (walls[w].Contains(p)) { inside++; break; }
                    }
                }
            }

            return total == 0 ? 0f : (float)inside / total;
        }

        private static bool Contains(Rect outer, Rect inner)
            => inner.xMin >= outer.xMin - 0.01f && inner.xMax <= outer.xMax + 0.01f &&
               inner.yMin >= outer.yMin - 0.01f && inner.yMax <= outer.yMax + 0.01f;

        private static bool Touches(Rect a, Rect b, float tolerance)
            => a.xMin - tolerance < b.xMax && a.xMax + tolerance > b.xMin &&
               a.yMin - tolerance < b.yMax && a.yMax + tolerance > b.yMin;
    }
}
