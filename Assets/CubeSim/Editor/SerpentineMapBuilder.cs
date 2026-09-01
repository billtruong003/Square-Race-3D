using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Composes the reference-style course "Serpentine01" out of the reusable authored modules.
    ///
    /// Every object it creates is one a designer could place by hand from the CubeSim/Authoring menu;
    /// nothing here is map-specific runtime code. The script exists so the map is reproducible and
    /// reviewable as a diff - the output is a normal prefab, editable in the Scene view.
    ///
    /// Layout (looking down, +X right, +Z up):
    ///
    ///   ##############################################
    ///   ##  LANE 3   start-left  ---->     [ GOAL ] ##
    ///   ##############################  ##############
    ///   ##  ^ transition B          #################
    ///   ##############################################
    ///   ##  LANE 2   <----                          ##
    ///   ##############################  ##############
    ///   #################          ^ transition A   ##
    ///   ##############################################
    ///   ##  [START]  LANE 1  ---->                  ##
    ///   ##############################################
    /// </summary>
    public static class SerpentineMapBuilder
    {
        public const string ArenaId = "Serpentine01";
        public const string PrefabPath = "Assets/CubeSim/Arenas/Serpentine01.prefab";
        public const string LibraryPath = "Assets/CubeSim/Data/AuthoredArenaLibrary.asset";

        // Arena is larger than the course, so the fill walls have real mass to occupy.
        private const float ArenaWidth = 72f;
        private const float ArenaDepth = 52f;
        private const float CourseMaxX = 30f;
        private const float CourseMaxZ = 20f;

        private const float LaneHeight = 8f;    // playable corridor width
        private const float BandHeight = 8f;    // solid mass between lanes
        private const float TransitionWidth = 8f;
        private const float WallHeight = 2.8f;

        // Lane centre lines.
        private const float Lane1Z = -16f;      // z in [-20, -12]
        private const float Lane2Z = 0f;        // z in [-4, 4]
        private const float Lane3Z = 16f;       // z in [12, 20]

        [MenuItem("CubeSim/Build Serpentine Reference Map", priority = 50)]
        public static GameObject Build()
        {
            Directory.CreateDirectory("Assets/CubeSim/Arenas");

            var root = new GameObject(ArenaId);
            AuthoredArena arena = root.AddComponent<AuthoredArena>();

            var so = new SerializedObject(arena);
            so.FindProperty("arenaId").stringValue = ArenaId;
            so.FindProperty("size").vector2Value = new Vector2(ArenaWidth, ArenaDepth);
            so.FindProperty("wallHeight").floatValue = WallHeight;
            so.FindProperty("floorThickness").floatValue = 0.5f;
            so.FindProperty("designedCorridorWidth").floatValue = LaneHeight;

            // Thick outer masses, same rule as the 5v5 map: outward only, inner faces unmoved.
            so.FindProperty("visualFillPadding").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildBorder(root.transform);
            BuildLaneSeparators(root.transform);
            BuildLaneObstacles(root.transform);
            BuildBreakableWalls(root.transform);
            BuildRegions(root.transform);
            BuildPressureTrack(root.transform);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            RegisterInLibrary(prefab);

            Debug.Log(AuthoredArenaTools.Validate(prefab.GetComponent<AuthoredArena>(), 1.1f));
            Debug.Log($"[CubeSim] Serpentine reference map saved to {PrefabPath}");
            return prefab;
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>
        /// The four boundary walls. Each is authored as a thin strip whose inner face is the course
        /// edge; ExtendToArenaBounds then swallows everything behind it, so the outside of the map is
        /// solid mass rather than exposed floor. The inner face never moves.
        /// </summary>
        private static void BuildBorder(Transform parent)
        {
            var holder = new GameObject("Border").transform;
            holder.SetParent(parent, false);

            FillWall(holder, "Border_Left",
                Rect.MinMaxRect(-CourseMaxX - 1f, -ArenaDepth * 0.5f, -CourseMaxX, ArenaDepth * 0.5f),
                FillDirection.MinusX);

            FillWall(holder, "Border_Right",
                Rect.MinMaxRect(CourseMaxX, -ArenaDepth * 0.5f, CourseMaxX + 1f, ArenaDepth * 0.5f),
                FillDirection.PlusX);

            FillWall(holder, "Border_Bottom",
                Rect.MinMaxRect(-ArenaWidth * 0.5f, -CourseMaxZ - 1f, ArenaWidth * 0.5f, -CourseMaxZ),
                FillDirection.MinusZ);

            FillWall(holder, "Border_Top",
                Rect.MinMaxRect(-ArenaWidth * 0.5f, CourseMaxZ, ArenaWidth * 0.5f, CourseMaxZ + 1f),
                FillDirection.PlusZ);
        }

        /// <summary>
        /// The masses between lanes. Each spans the full course except for the transition gap, which
        /// is what turns three parallel corridors into one serpentine route.
        /// </summary>
        private static void BuildLaneSeparators(Transform parent)
        {
            var holder = new GameObject("LaneSeparators").transform;
            holder.SetParent(parent, false);

            float bandAZ = Lane1Z + LaneHeight * 0.5f;                 // -12
            float bandBZ = Lane2Z + LaneHeight * 0.5f;                 // 4

            // Between lane 1 and lane 2: open on the right, so the route climbs at +X. The mass is
            // split so the breakable shortcuts can sit in the gaps as real openable doors.
            InternalWall(holder, "Mass_L1L2_A", Rect.MinMaxRect(-CourseMaxX, bandAZ, -24.5f, bandAZ + BandHeight));
            InternalWall(holder, "Mass_L1L2_B", Rect.MinMaxRect(-19.5f, bandAZ, -4.5f, bandAZ + BandHeight));
            InternalWall(holder, "Mass_L1L2_C", Rect.MinMaxRect(0.5f, bandAZ, 11.5f, bandAZ + BandHeight));
            InternalWall(holder, "Mass_L1L2_D", Rect.MinMaxRect(16.5f, bandAZ, CourseMaxX - TransitionWidth, bandAZ + BandHeight));

            // Between lane 2 and lane 3: open on the left, so the route climbs at -X.
            InternalWall(holder, "Mass_L2L3_A", Rect.MinMaxRect(-CourseMaxX + TransitionWidth, bandBZ, -16.25f, bandBZ + BandHeight));
            InternalWall(holder, "Mass_L2L3_B", Rect.MinMaxRect(-11.75f, bandBZ, 9.5f, bandBZ + BandHeight));
            InternalWall(holder, "Mass_L2L3_C", Rect.MinMaxRect(14.5f, bandBZ, 19.5f, bandBZ + BandHeight));
            InternalWall(holder, "Mass_L2L3_D", Rect.MinMaxRect(24.5f, bandBZ, CourseMaxX, bandBZ + BandHeight));
        }

        /// <summary>
        /// Obstacles inside the lanes, so a straight corridor still produces varied bounces.
        ///
        /// Every block is centred in its lane and no deeper than 2 m, which leaves a 3 m channel on
        /// each side. An off-centre block left only 1.2 m and a larger racer could not pass.
        /// </summary>
        private static void BuildLaneObstacles(Transform parent)
        {
            var holder = new GameObject("Obstacles").transform;
            holder.SetParent(parent, false);

            AddBlock(holder, "Block_L1_A", new Vector2(-10f, Lane1Z), new Vector2(5f, 2f));
            AddBlock(holder, "Block_L1_B", new Vector2(8f, Lane1Z), new Vector2(6f, 2f));
            AddBlock(holder, "Pillar_L1", new Vector2(20f, Lane1Z), new Vector2(2f, 2f));

            AddBlock(holder, "Block_L2_A", new Vector2(14f, Lane2Z), new Vector2(7f, 2f));
            AddBlock(holder, "Block_L2_B", new Vector2(-6f, Lane2Z), new Vector2(6f, 2f));
            AddBlock(holder, "Pillar_L2", new Vector2(-20f, Lane2Z), new Vector2(2f, 2f));

            AddBlock(holder, "Block_L3_A", new Vector2(-14f, Lane3Z), new Vector2(6f, 2f));
            AddBlock(holder, "Block_L3_B", new Vector2(4f, Lane3Z), new Vector2(7f, 2f));
        }

        /// <summary>
        /// Four openable walls, one per rule family, placed so each creates a real shortcut through
        /// the lane masses. They are ordinary ArenaWalls with a BreakableWall component added - no
        /// map-specific runtime code anywhere.
        /// </summary>
        private static void BuildBreakableWalls(Transform parent)
        {
            var holder = new GameObject("BreakableWalls").transform;
            holder.SetParent(parent, false);

            float bandAZ = Lane1Z + LaneHeight * 0.5f;   // -12 .. -4
            float bandBZ = Lane2Z + LaneHeight * 0.5f;   //   4 .. 12

            // Shortcut between lane 1 and lane 2 on the left: needs a lot of collective battering.
            Breakable(holder, "Shortcut_Hits",
                WallFillMath.FromCenterSize(new Vector2(-22f, bandAZ + BandHeight * 0.5f), new Vector2(5f, BandHeight)),
                BreakCondition.TotalHitsAnyRacer, 25, Color.white);

            // Colour gate: only the red racer opens it, and one touch is enough.
            Breakable(holder, "ColorGate_Red",
                WallFillMath.FromCenterSize(new Vector2(-2f, bandAZ + BandHeight * 0.5f), new Vector2(5f, BandHeight)),
                BreakCondition.AnyHitByRequiredColor, 1, new Color(0.95f, 0.16f, 0.16f));

            // Needs a crowd: several different racers must each touch it once.
            Breakable(holder, "Crowd_Unique",
                WallFillMath.FromCenterSize(new Vector2(12f, bandBZ + BandHeight * 0.5f), new Vector2(5f, BandHeight)),
                BreakCondition.UniqueRacerHitsAnyColor, 4, Color.white);

            // Opens for the first racer that reaches it - a free door into the final lane.
            Breakable(holder, "Door_SingleUse",
                WallFillMath.FromCenterSize(new Vector2(-14f, bandBZ + BandHeight * 0.5f), new Vector2(4.5f, BandHeight)),
                BreakCondition.SingleUseAnyRacer, 1, Color.white);

            // Only blue impacts count, and it takes a few of them.
            Breakable(holder, "ColorCount_Blue",
                WallFillMath.FromCenterSize(new Vector2(14f, bandAZ + BandHeight * 0.5f), new Vector2(5f, BandHeight)),
                BreakCondition.RequiredColorHitCount, 3, new Color(0.18f, 0.36f, 0.98f), 0.3f);

            // Warm-coloured racers only, and each of them counts once. The wider tolerance is what
            // makes this reachable: a strict match would need two identically coloured racers.
            Breakable(holder, "ColorCrowd_Warm",
                WallFillMath.FromCenterSize(new Vector2(22f, bandBZ + BandHeight * 0.5f), new Vector2(5f, BandHeight)),
                BreakCondition.UniqueRacerHitsByRequiredColor, 2, new Color(0.95f, 0.3f, 0.2f), 0.55f);
        }

        private static void Breakable(Transform parent, string name, Rect footprint,
            BreakCondition condition, int requiredHits, Color requiredColor, float tolerance = 0.25f)
        {
            GameObject go = InternalWall(parent, name, footprint);
            BreakableWall breakable = go.AddComponent<BreakableWall>();

            var so = new SerializedObject(breakable);
            so.FindProperty("id").stringValue = name;
            so.FindProperty("condition").enumValueIndex = (int)condition;
            so.FindProperty("requiredHits").intValue = requiredHits;
            so.FindProperty("requiredColor").colorValue = requiredColor;
            so.FindProperty("colorTolerance").floatValue = tolerance;
            so.FindProperty("contactCooldownPerRacer").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRegions(Transform parent)
        {
            var holder = new GameObject("Regions").transform;
            holder.SetParent(parent, false);

            // Start: left end of the bottom lane.
            Region<SpawnArea>(holder, "SpawnArea_Start",
                new Vector2(-24f, Lane1Z), new Vector2(9f, LaneHeight - 1.5f));

            // Destination: right end of the top lane.
            GameObject goalGo = Region<GoalArea>(holder, "GoalArea_Finish",
                new Vector2(25.5f, Lane3Z), new Vector2(8f, LaneHeight - 1f));

            var goalSo = new SerializedObject(goalGo.GetComponent<GoalArea>());
            goalSo.FindProperty("retireOnReach").boolValue = true;
            goalSo.FindProperty("entryFraction").floatValue = 0.5f;
            goalSo.FindProperty("visualType").enumValueIndex = (int)GoalVisualType.Gate;
            goalSo.FindProperty("visualColor").colorValue = new Color(0.18f, 0.98f, 0.42f);
            goalSo.FindProperty("visualEmission").floatValue = 0.9f;
            goalSo.ApplyModifiedPropertiesWithoutUndo();

            // Two weapon chambers along the route; the seed picks which one and where inside it.
            Region<WeaponSpawnArea>(holder, "WeaponArea_Lane1",
                new Vector2(0f, Lane1Z), new Vector2(10f, LaneHeight - 3f));

            Region<WeaponSpawnArea>(holder, "WeaponArea_Lane2",
                new Vector2(-2f, Lane2Z), new Vector2(10f, LaneHeight - 3f));

            // A damaging patch rather than an instant-kill strip: clipping it costs health, standing
            // in it kills. A lethal area across a corridor every racer must cross wipes the field.
            GameObject hazardGo = Region<HazardArea>(holder, "Hazard_Lane2",
                new Vector2(24f, Lane2Z - 2.4f), new Vector2(3f, 2.6f));

            var hazardSo = new SerializedObject(hazardGo.GetComponent<HazardArea>());
            hazardSo.FindProperty("damagePerSecond").floatValue = 55f;
            hazardSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The serpentine route, in order. Each segment fills its own lane or transition and hands
        /// over to the next, so the pressure follows the racers around the course.
        /// </summary>
        private static void BuildPressureTrack(Transform parent)
        {
            var trackGo = new GameObject("PressureTrack");
            trackGo.transform.SetParent(parent, false);
            PressureTrack track = trackGo.AddComponent<PressureTrack>();

            var so = new SerializedObject(track);
            so.FindProperty("startDelay").floatValue = 10f;
            so.FindProperty("speedScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            float bandAZ = Lane1Z + LaneHeight * 0.5f;
            float bandBZ = Lane2Z + LaneHeight * 0.5f;

            Segment(trackGo.transform, "Segment_00_Lane1",
                Rect.MinMaxRect(-CourseMaxX, Lane1Z - LaneHeight * 0.5f, CourseMaxX, bandAZ),
                FillDirection.PlusX, 2.6f);

            Segment(trackGo.transform, "Segment_01_TransitionA",
                Rect.MinMaxRect(CourseMaxX - TransitionWidth, bandAZ, CourseMaxX, bandAZ + BandHeight),
                FillDirection.PlusZ, 2.6f);

            Segment(trackGo.transform, "Segment_02_Lane2",
                Rect.MinMaxRect(-CourseMaxX, Lane2Z - LaneHeight * 0.5f, CourseMaxX, bandBZ),
                FillDirection.MinusX, 2.6f);

            Segment(trackGo.transform, "Segment_03_TransitionB",
                Rect.MinMaxRect(-CourseMaxX, bandBZ, -CourseMaxX + TransitionWidth, bandBZ + BandHeight),
                FillDirection.PlusZ, 2.6f);

            Segment(trackGo.transform, "Segment_04_Lane3",
                Rect.MinMaxRect(-CourseMaxX, Lane3Z - LaneHeight * 0.5f, CourseMaxX, CourseMaxZ),
                FillDirection.PlusX, 2.6f);
        }

        private static void Segment(Transform parent, string name, Rect footprint,
            FillDirection direction, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(footprint.center.x, 0.4f, footprint.center.y);
            go.transform.localScale = new Vector3(footprint.width, 1f, footprint.height);

            PressureSegment segment = go.AddComponent<PressureSegment>();
            var so = new SerializedObject(segment);
            so.FindProperty("fillDirection").enumValueIndex = (int)direction;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("startDelay").floatValue = 0f;
            so.FindProperty("completionFraction").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject FillWall(Transform parent, string name, Rect footprint, FillDirection direction)
        {
            GameObject go = MakeWallObject(parent, name, footprint);
            ArenaWall wall = go.AddComponent<ArenaWall>();

            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.BoundaryFill;
            so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.ExtendToArenaBounds;
            so.FindProperty("fillDirection").enumValueIndex = (int)direction;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static GameObject InternalWall(Transform parent, string name, Rect footprint)
        {
            GameObject go = MakeWallObject(parent, name, footprint);
            ArenaWall wall = go.AddComponent<ArenaWall>();

            var so = new SerializedObject(wall);
            so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
            so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static void AddBlock(Transform parent, string name, Vector2 center, Vector2 size)
            => InternalWall(parent, name, WallFillMath.FromCenterSize(center, size));

        private static GameObject MakeWallObject(Transform parent, string name, Rect footprint)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(footprint.center.x, WallHeight * 0.5f, footprint.center.y);
            go.transform.localScale = new Vector3(footprint.width, WallHeight, footprint.height);
            return go;
        }

        private static GameObject Region<T>(Transform parent, string name, Vector2 center, Vector2 size)
            where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, 0.2f, center.y);
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            go.AddComponent<T>();
            return go;
        }

        // ---------------------------------------------------------------- library

        public static AuthoredArenaLibrary RegisterInLibrary(GameObject prefab)
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(LibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<AuthoredArenaLibrary>();

            // Keep any other authored maps that are already registered.
            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries)
            {
                if (existing.id != ArenaId && existing.prefab != null) entries.Add(existing);
            }

            entries.Add(new AuthoredArenaLibrary.Entry { id = ArenaId, prefab = prefab });
            library.SetEntries(entries);

            if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(LibraryPath);
        }
    }
}
