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

        private sealed class Tuning
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
        }

        private static Tuning TuningFor(string map)
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
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 15f, InsetZ = 29f };
                case "SW":
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, MaxHealth = 2f, Weapons = 1, SawDamage = 1f, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 15f, InsetZ = 29f };
                case "TW":
                    bool fourWay = map == "TW11" || map == "TW12" || map == "TW13";
                    return new Tuning { Win = WinCondition.LastTeamAlive, Duration = 300f, MaxHealth = 2f, Weapons = 2,
                        Racers = 20, Teams = fourWay ? 4 : 2, BreakableHits = 40,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 8f, PressureSpeed = 0.45f, InsetX = 15f, InsetZ = 29f };
                case "CR":
                    return new Tuning { Win = WinCondition.MostCoins, Duration = 40f, Weapons = 0, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Park, PressureDelay = 100000f, PressureSpeed = 0f };
                default:
                    return new Tuning { Win = WinCondition.LastAlive, Duration = 300f, Weapons = 1, Racers = 12,
                        Pressure = EpisodeDirector.PressureProfile.Collapse, PressureDelay = 5f, PressureSpeed = 0.5f, InsetX = 15f, InsetZ = 29f };
            }
        }

        /// <summary>True when every goal cell (G or R) sits in the bottom quarter of the template.</summary>
        private static bool GoalAtBottom(string templatePath)
        {
            string[] rows = File.ReadAllLines(templatePath).Where(r => r.Trim().Length > 0).ToArray();
            int first = -1;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].IndexOf('G') >= 0 || rows[r].IndexOf('R') >= 0) { first = r; break; }
            }
            return first >= 0 && first >= rows.Length * 3 / 4;
        }

        [MenuItem("CubeSim/Build Shorts (portrait, 1 map = 1 video)", priority = 7)]
        public static void BuildAll()
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
                Tuning tune = TuningFor(map);
                // A top-down slab only makes sense when the goal is the bottom edge. Looping courts
                // (goal in the middle, route doubles back upward) get parked pressure instead, or
                // the slab walls the goal off and the round times out with everyone stuck below it.
                if (tune.Win == WinCondition.ReachGoal && !GoalAtBottom(template))
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
                report.Append($"  S_{map}: {seed}\n");

                director.SetRounds(new List<EpisodeDirector.RoundSpec>
                {
                    new EpisodeDirector.RoundSpec
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
                    }
                });

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
