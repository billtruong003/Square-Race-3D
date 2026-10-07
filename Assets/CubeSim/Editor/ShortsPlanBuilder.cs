using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Arena;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// The Shorts line: one portrait map per video, one round, 30-55 seconds, 1080x1920.
    ///
    /// Every landscape template has a transposed twin in Templates/Shorts (V_xxx.txt, 26 x 48
    /// cells = a 38 x 68 m court, spawn pens at the top, goal at the bottom). Each twin becomes
    /// its own scene under Scenes/Shorts with a single round tuned to end fast: the chase runs
    /// downhill at 0.9 m/s, collapses start after five seconds, coin rushes last forty. The
    /// recorder reads the round's portrait flag and captures 1080x1920.
    /// </summary>
    public static class ShortsPlanBuilder
    {
        public const string TemplateFolder = "Assets/CubeSim/Arenas/Templates/Shorts";
        public const string PrefabFolder = "Assets/CubeSim/Arenas/Shorts";
        public const string SceneFolder = "Assets/CubeSim/Scenes/Shorts";

        public sealed class Tuning
        {
            public WinCondition Win;
            public float Duration;
            public float MaxHealth;
            public int Weapons;
            public int Racers;
            public int Teams;
            public EpisodeDirector.PressureProfile Pressure;
            public float PressureDelay;
            public float PressureSpeed;
            public float InsetX = 54f;
            public float InsetZ = 12f;
            public int BreakableHits = 3;
            public int MegaHits = 60;
            public int RockTile = 2;
            public float SawDamage = 0.5f;
            public int Finishers = 1;
            public ModeKind Mode = ModeKind.None;
        }

        public static Tuning TuningFor(string map)
        {
            string prefix = map.Substring(0, 2);
            switch (prefix)
            {
                case "RC":
                    return new Tuning { Win = WinCondition.ReachGoal, Duration = 300f, Weapons = 0, Racers = 12, Finishers = 3,
                        Pressure = EpisodeDirector.PressureProfile.ChaseDown, PressureDelay = 3f, PressureSpeed = 1.5f, InsetX = 54f };
                case "RB":
                case "RM":
                    return new Tuning { Win = WinCondition.ReachGoal, Duration = 300f, Weapons = 0, Racers = 12, Finishers = 3,
                        Pressure = EpisodeDirector.PressureProfile.ChaseDown, PressureDelay = 5f, PressureSpeed = 1.1f, InsetX = 54f };
                case "SD":
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, MaxHealth = 1f, Weapons = 1, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 16f, InsetZ = 33f };
                case "SW":
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, MaxHealth = 2f, Weapons = 1, SawDamage = 1f, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 16f, InsetZ = 33f };
                case "TW":
                    // TW16-18 also draw four sealed pens; they used to be played as two teams.
                    bool fourWay = map == "TW11" || map == "TW12" || map == "TW13" ||
                                   map == "TW16" || map == "TW17" || map == "TW18";
                    return new Tuning { Win = WinCondition.LastTeamAlive, Duration = 300f, MaxHealth = 2f, Weapons = 2,
                        Racers = 20, Teams = fourWay ? 4 : 2, BreakableHits = 40,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 8f, PressureSpeed = 0.45f, InsetX = 16f, InsetZ = 33f };
                case "LB":
                    // Lucky Block: no knife on the floor, every weapon comes out of a crate.
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, MaxHealth = 2f, Weapons = 0, Racers = 12,
                        Mode = ModeKind.LuckyBlock,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 15f, PressureSpeed = 0.5f, InsetX = 16f, InsetZ = 33f };
                case "CR":
                    return new Tuning { Win = WinCondition.MostCoins, Duration = 40f, Weapons = 0, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Park, PressureDelay = 100000f, PressureSpeed = 0f };
                default:
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, Weapons = 1, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 16f, InsetZ = 33f };
            }
        }

        /// <summary>The one round spec a Short runs; the seed scout tests exactly this spec.</summary>
        public static EpisodeDirector.RoundSpec RoundFor(string id, Tuning tune, int seed) => new EpisodeDirector.RoundSpec
        {
            arenaId = id,
            seed = seed,
            winCondition = tune.Win,
            maxDuration = tune.Duration,
            maxHealth = tune.MaxHealth,
            weaponCount = tune.Weapons,
            racerCount = tune.Racers,
            teamCount = tune.Teams,
            pressureProfile = tune.Pressure,
            pressureStartDelay = tune.PressureDelay,
            pressureSpeed = tune.PressureSpeed,
            pressureTargetInset = tune.InsetX,
            pressureTargetInsetZ = tune.InsetZ,
            portrait = true,
            requiredFinishers = tune.Finishers,
            modeKind = tune.Mode,
        };

        /// <summary>True when every goal cell sits in the bottom quarter of the template. ('R' is a spawn.)</summary>
        private static bool GoalAtBottom(string templatePath)
        {
            string[] rows = File.ReadAllLines(templatePath).Where(r => r.Trim().Length > 0).ToArray();
            int first = -1;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].IndexOf('G') >= 0) { first = r; break; }
            }
            return first >= 0 && first >= rows.Length * 3 / 4;
        }

        [MenuItem("CubeSim/Build Shorts (portrait, 1 map = 1 video)", priority = 7)]
        public static void BuildAll() => Build(null, false);

        [MenuItem("CubeSim/Build Shorts (scouted seeds)", priority = 8)]
        public static void BuildAllScouted() => Build(null, true);

        /// <summary>
        /// Builds the portrait scenes whose map id passes <paramref name="include"/> (null = all).
        /// With <paramref name="scout"/>, each map's seed is the best of a batch of headless test
        /// runs (SeedScout) instead of a blind draw.
        /// </summary>
        public static void Build(System.Func<string, bool> include, bool scout)
        {
            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null)
            {
                Debug.LogError("[CubeSim] Arena library missing; build the map pack first.");
                return;
            }

            string[] templates = Directory.GetFiles(TemplateFolder, "V_*.txt")
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            if (templates.Length == 0)
            {
                Debug.LogError($"[CubeSim] No portrait templates in {TemplateFolder}.");
                return;
            }

            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(SceneFolder);

            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries) entries.Add(existing);

            var draw = new System.Random(unchecked(System.Environment.TickCount));
            var report = new System.Text.StringBuilder("[CubeSim] Shorts seeds:\n");
            int built = 0;

            foreach (string template in templates)
            {
                string id = Path.GetFileNameWithoutExtension(template);       // V_RC13
                string map = id.Substring(2);                                  // RC13
                if (include != null && !include(map)) continue;
                Tuning tune = TuningFor(map);
                // The old top-down slab walled off goals that were not on the bottom edge, so those
                // courts were parked. The walking-distance zone follows any route to any goal, so
                // only parks when the zone is switched off.
                if (!EpisodeDirector.LegacySlabsAsZones &&
                    tune.Win == WinCondition.ReachGoal && !GoalAtBottom(template))
                {
                    tune.Pressure = EpisodeDirector.PressureProfile.Park;
                    tune.PressureDelay = 100000f;
                    tune.PressureSpeed = 0f;
                }

                GameObject prefab = AsciiArenaBuilder.Build(template, new AsciiArenaBuilder.Settings
                {
                    ArenaId = id,
                    CourseSize = new Vector2(38f, 68f),
                    WallHeight = 2.8f,
                    VisualFillPadding = 22f,
                    DesignedCorridorWidth = 2.8f,
                    BreakableHits = tune.BreakableHits,
                    MegaBlockHits = tune.MegaHits,
                    RockTileCells = tune.RockTile,
                    RainbowLayerHits = 2,
                    SawDamage = tune.SawDamage,
                }, $"{PrefabFolder}/{id}.prefab");

                entries.RemoveAll(e => e.id == id);
                entries.Add(new AuthoredArenaLibrary.Entry { id = id, prefab = prefab });

                Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
                var director = Object.FindFirstObjectByType<EpisodeDirector>();
                if (director == null)
                {
                    Debug.LogError("[CubeSim] Episode scene has no director; aborting shorts.");
                    return;
                }

                int seed = draw.Next(100000, 999999);
                if (scout)
                {
                    // The lab loads the arena from the library, so the fresh prefab must be in it.
                    library.SetEntries(entries);
                    SeedScout.Result best = SeedScout.Pick(id, tune, seed, 16);
                    if (best != null) seed = best.Seed;
                    report.Append($"  S_{map}: {seed}  {(best != null ? best.Summary : "scout failed")}\n");
                }
                else
                {
                    report.Append($"  S_{map}: {seed}\n");
                }

                director.SetRounds(new List<EpisodeDirector.RoundSpec> { RoundFor(id, tune, seed) });

                // Shorts pacing: a one-second title, a quick round card, the winner held long
                // enough to read. The leaderboard becomes a strip across the top (portrait HUD).
                var dso = new SerializedObject(director);
                dso.FindProperty("introDuration").floatValue = 1.2f;
                dso.FindProperty("roundCardDuration").floatValue = 0.9f;
                dso.FindProperty("winnerCardDuration").floatValue = 2.5f;
                dso.ApplyModifiedPropertiesWithoutUndo();

                var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
                if (bootstrap != null)
                {
                    var bso = new SerializedObject(bootstrap);
                    bso.FindProperty("showLeaderboard").boolValue = true;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstrap);
                }

                EditorUtility.SetDirty(director);
                EditorSceneManager.SaveScene(scene, $"{SceneFolder}/S_{map}.unity");
                built++;
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            Debug.Log($"[CubeSim] Shorts built: {built} scenes in {SceneFolder}. Record with CubeSim/Record Shorts.");
        }
    }
}
