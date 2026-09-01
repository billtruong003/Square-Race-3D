using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Generates the whole macro library: authoring prefabs first, then their baked counterparts,
    /// then the arena recipe that composes them.
    ///
    /// Run this before building the arena. It is idempotent - every asset has a deterministic path,
    /// so rebaking overwrites in place and existing scene references survive.
    /// </summary>
    public static class EnvironmentMacroLibrary
    {
        private const string CatalogPath = "Assets/ChallengeShow/Data/ChallengeShowCatalog.asset";
        private const string RecipePath = "Assets/ChallengeShow/Environment/Data/ArenaRecipe_Video1.asset";

        // Lane geometry, mirroring the validated gameplay constants. These are read-only here: the
        // macros are laid out to serve the existing collider, never the other way round.
        private const float ArmZ = 16f;
        private const float ArmPivotHeight = 4.6f;
        private const float WallZ = 6.1f;

        [MenuItem("Challenge Show/2b. Build Environment Macros")]
        public static void BuildLibrary()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Macros] No catalog — run steps 1 and 2 first.");
                return;
            }

            KenneyMacroBaker.EnsureFolder(KenneyMacroBaker.AuthoringRoot);
            KenneyMacroBaker.EnsureFolder("Assets/ChallengeShow/Environment/Data");

            var baked = new Dictionary<string, GameObject>();

            // ---- lane sections -------------------------------------------------------------
            // Section spans are chosen so every validated gameplay Z falls inside the right macro:
            // the crystal wall (6.1) in ImpactZone, the spawn (8.5) in SpawnCourt, the arm (16) in
            // ArmZone, the finish (42) in FinishCourt.
            baked["ImpactZone"] = Make("ImpactZone", "Lane",
                r => MacroAuthoring.BuildImpactZone(r, 4, 1101, WallZ - 1.9f, WallZ + 0.1f));
            baked["SpawnCourt"] = Make("SpawnCourt", "Lane",
                r => MacroAuthoring.BuildSpawnCourt(r, 2, 1102));
            baked["ArmZone"] = Make("ArmZone", "Obstacles",
                r => MacroAuthoring.BuildArmZone(r, 4, 1103, -2f, ArmPivotHeight));
            baked["LaneStraight_12m"] = Make("LaneStraight_12m", "Lane",
                r => MacroAuthoring.BuildStraight(r, 4, 1104));
            baked["FinishCourt"] = Make("FinishCourt", "Lane",
                r => MacroAuthoring.BuildFinishCourt(r, 4, 1105));
            baked["LaneStraight_6m"] = Make("LaneStraight_6m", "Lane",
                r => MacroAuthoring.BuildStraight(r, 2, 1106));

            // ---- landmarks -----------------------------------------------------------------
            baked["FinishGate"] = Make("FinishGate", "Landmarks",
                r => MacroAuthoring.BuildFinishGate(r, 1201));

            // ---- bastions ------------------------------------------------------------------
            // One macro per family. They share the dungeon kit and differ only in footprint, how
            // solid the back wall is, and how much rubble they carry — enough that five stages read
            // as five places, not enough to become five architectural styles.
            var specs = new Dictionary<string, MacroAuthoring.BastionSpec>
            {
                // cols, rows, wallFill, raisedCentre, rubble
                { "Cactus",   new MacroAuthoring.BastionSpec(0, 2, 0.50f, false, 0.8f) },  // wide and low
                { "Cat",      new MacroAuthoring.BastionSpec(0, 2, 0.25f, false, 0.7f) },  // light and open
                { "Dog",      new MacroAuthoring.BastionSpec(3, 2, 0.85f, true,  0.9f) },  // the keep
                { "MoleRat",  new MacroAuthoring.BastionSpec(0, 2, 0.60f, false, 1.5f) },  // rugged
                { "Skeleton", new MacroAuthoring.BastionSpec(0, 2, 0.90f, false, 0.6f) },  // strong back wall
            };

            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                string key = family.familyName.Replace(" ", "");
                if (!specs.TryGetValue(key, out var spec))
                    spec = new MacroAuthoring.BastionSpec(0, 2, 0.6f, false, 0.8f);

                // cols 0 means "derive from the roster", so a family holding a 4.25 m boss gets a
                // wider stage than one holding three cats without anyone hand-tuning it.
                int cols = spec.Cols > 0
                    ? spec.Cols
                    : Mathf.Clamp(Mathf.CeilToInt(2f * SlotSpacingFor(family) / MacroAuthoring.Grid), 3, 5);
                var sized = new MacroAuthoring.BastionSpec(cols, spec.Rows, spec.WallFill,
                                                           spec.RaisedCentre, spec.Rubble);

                string macroName = key == "Dog" ? "DogKeep" : $"Bastion_{key}";
                string category = key == "Dog" ? "Landmarks" : "Bastions";
                var f = family;
                baked[macroName] = Make(macroName, category,
                    r => MacroAuthoring.BuildBastion(r, sized, f.displayColor, key.GetHashCode()));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteRecipe(catalog, baked);
            Debug.Log($"[Macros] Library built: {baked.Count} macros.");
        }

        /// <summary>
        /// Slot spacing for a family's roster.
        ///
        /// Lives here so the macro and the arena builder cannot drift apart: the bastion is sized
        /// from this number and the roster slots are placed from the same one.
        /// </summary>
        public static float SlotSpacingFor(MonsterFamilyDefinition family)
        {
            float tallest = Mathf.Max(1f, family.TallestUnitHeight);
            float widest = 1f;
            foreach (var u in family.ValidUnits) widest = Mathf.Max(widest, u.bodyWidth);
            return Mathf.Max(4f, tallest * 1.35f, widest * 1.9f);
        }

        /// <summary>
        /// Build one macro's authoring hierarchy, save it, and bake it.
        ///
        /// The authoring root is created in the active scene and destroyed immediately afterwards,
        /// so the production scene is never left holding loose pieces.
        /// </summary>
        private static GameObject Make(string name, string category, System.Action<Transform> author)
        {
            var root = new GameObject(name);
            try
            {
                author(root.transform);

                string authoringPath = $"{KenneyMacroBaker.AuthoringRoot}/{name}_AUTHOR.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, authoringPath);

                return KenneyMacroBaker.Bake(root, name, category);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Write the recipe for the current show.
        ///
        /// The lane reads quiet - LOUD - quiet - LOUD rather than holding one level of architectural
        /// intensity the whole way down, so the arm strike and the finish both land as events.
        /// </summary>
        private static void WriteRecipe(ChallengeShowCatalog catalog, Dictionary<string, GameObject> baked)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipe>(RecipePath);
            bool isNew = recipe == null;
            if (isNew) recipe = ScriptableObject.CreateInstance<ArenaRecipe>();

            recipe.laneStartZ = -6f;
            recipe.lane = new[]
            {
                Section(baked, "ImpactZone",       "LOUD - crystal wall"),
                Section(baked, "SpawnCourt",       "quiet - broad start"),
                Section(baked, "ArmZone",          "LOUD - the obstacle"),
                Section(baked, "LaneStraight_12m", "quiet - recovery run"),
                Section(baked, "FinishCourt",      "LOUD - arrival"),
                Section(baked, "LaneStraight_6m",  "quiet - tail"),
            };

            // Bastions, in catalog order: Cactus, Cat, Dog, MoleRat, Skeleton.
            //
            // Positioned for a camera looking down the lane from behind the spawn, so -X is screen
            // left, +X is screen right, and increasing Z recedes. Three depth bands give the shot a
            // foreground, a middle and a background instead of five siblings on one plane, and all
            // five sit outside the lane's colonnades.
            var placements = new List<ArenaRecipe.Placement>();
            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                string key = family.familyName.Replace(" ", "");
                string macroName = key == "Dog" ? "DogKeep" : $"Bastion_{key}";
                baked.TryGetValue(macroName, out var macro);

                placements.Add(key switch
                {
                    "Cactus" => Place("Cactus  (mid left)",       macro, new Vector3(-31f,  9f, 29f), -11f, 1.10f),
                    "Cat" => Place("Cat     (fore left)",         macro, new Vector3(-25f,  2f,  3f),  15f, 1.00f),
                    // Far and high, scaled up: at true scale three monsters 80 m out are specks.
                    "Dog" => Place("Dog     (background keep)",   macro, new Vector3(  5f, 13f, 78f),  -6f, 1.70f),
                    "MoleRat" => Place("MoleRat (mid right)",     macro, new Vector3( 32f,  7f, 36f), -15f, 1.15f),
                    "Skeleton" => Place("Skeleton(fore right)",   macro, new Vector3( 24f,  3f, -1f),  10f, 1.05f),
                    _ => Place(key, macro, Vector3.zero, 0f, 1f),
                });
            }
            recipe.bastions = placements.ToArray();

            recipe.landmarks = new[]
            {
                Place("Finish gate", baked.GetValueOrDefault("FinishGate"), new Vector3(0f, 0f, 42f), 0f, 1f),
            };

            if (isNew) AssetDatabase.CreateAsset(recipe, RecipePath);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        private static ArenaRecipe.LaneSection Section(Dictionary<string, GameObject> baked, string name, string beat)
        {
            baked.TryGetValue(name, out var macro);
            if (macro == null) Debug.LogWarning($"[Macros] Recipe references missing macro '{name}'.");
            return new ArenaRecipe.LaneSection { macro = macro, beat = beat };
        }

        private static ArenaRecipe.Placement Place(string label, GameObject macro, Vector3 pos, float yaw, float scale) =>
            new() { label = label, macro = macro, position = pos, yaw = yaw, scale = scale };
    }
}
