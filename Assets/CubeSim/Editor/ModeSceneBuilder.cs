using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Scenes for the new modes (tier A and B of the design doc). One scene per mode video,
    /// landscape, several rounds, no clock unless the mode is a clock. Lucky Block rides on the
    /// lab map until crate maps are drawn; everything else reuses the existing pack.
    /// </summary>
    public static class ModeSceneBuilder
    {
        public const string SceneFolder = "Assets/CubeSim/Scenes/Modes";

        private sealed class Plan
        {
            public string Id;
            public ModeKind Mode = ModeKind.None;
            public WinCondition Win = WinCondition.LastAlive;
            public string[] Maps;
            public int Racers = 12;
            public int Teams = 0;
            public int Weapons = 0;
            public float Health = 2f;
            public float Duration = 300f;
            public int Finishers = 1;
            public EpisodeDirector.PressureProfile Pressure = EpisodeDirector.PressureProfile.Collapse;
            public float PressureDelay = 20f;
            public float PressureSpeed = 0.4f;
            public float InsetX = 29f;
            public float InsetZ = 15f;
            public int[] Eliminate;        // per round; null = not a knockout
            public bool GrandPrix;
        }

        private static readonly Plan[] Plans =
        {
            new Plan { Id = "Infection", Mode = ModeKind.Infection, Win = WinCondition.LastClean,
                Maps = new[] { "SD04", "SD07", "SD10", "SD11", "SD12", "SD15" }, Health = 2f,
                PressureDelay = 30f, PressureSpeed = 0.4f },
            new Plan { Id = "HotPotato", Mode = ModeKind.HotPotato, Win = WinCondition.LastAlive,
                Maps = new[] { "SD09", "SD13", "SD16", "SD17", "SD18", "SD19" }, Health = 1f,
                PressureDelay = 20f, PressureSpeed = 0.4f },
            new Plan { Id = "LuckyBlock", Mode = ModeKind.LuckyBlock, Win = WinCondition.LastAlive,
                Maps = new[] { "LB01", "LB02", "LB03", "LB04", "LB05", "LB06", "LB07", "LB08" }, Health = 2f,
                PressureDelay = 15f, PressureSpeed = 0.5f },
            new Plan { Id = "PaintWar", Mode = ModeKind.PaintWar, Win = WinCondition.MostTiles,
                Maps = new[] { "DEV_OPEN", "RC22", "RC26", "RC28", "CR05" }, Health = 3f, Duration = 60f,
                Pressure = EpisodeDirector.PressureProfile.Park, PressureDelay = 100000f, PressureSpeed = 0f },
            new Plan { Id = "TeamRace", Mode = ModeKind.None, Win = WinCondition.TeamFinishers,
                Maps = new[] { "RC19", "RC24", "RC22", "RC26", "RC15" }, Teams = 2, Health = 3f, Finishers = 3,
                Pressure = EpisodeDirector.PressureProfile.Chase, PressureDelay = 3f, PressureSpeed = 1.5f, InsetX = 54f },
            new Plan { Id = "Elimination", Mode = ModeKind.None, Win = WinCondition.ReachGoal,
                Maps = new[] { "RC19", "RC24", "RC22", "RC18", "RC25", "RC26", "RC15" }, Health = 3f,
                Eliminate = new[] { 3, 3, 1, 1, 1, 1, 1 },
                // Knockout wants finishers, not corpses: the slab only comes as a late closer.
                Pressure = EpisodeDirector.PressureProfile.Chase, PressureDelay = 30f, PressureSpeed = 1.2f, InsetX = 54f },
            new Plan { Id = "GrandPrix", Mode = ModeKind.None, Win = WinCondition.ReachGoal,
                Maps = new[] { "RC24", "RC19", "RC22", "RC26", "RC15", "RC28", "RC18", "RC25" }, Health = 3f, Finishers = 8,
                GrandPrix = true,
                Pressure = EpisodeDirector.PressureProfile.Chase, PressureDelay = 30f, PressureSpeed = 1.2f, InsetX = 54f },
        };

        /// <summary>Templates that no format builder owns (Lucky Block crates) get their prefab here.</summary>
        private static void EnsurePrefabs()
        {
            var library = AssetDatabase.LoadAssetAtPath<CubeSim.Arena.AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null) return;
            var entries = new List<CubeSim.Arena.AuthoredArenaLibrary.Entry>(library.Entries);
            bool changed = false;
            foreach (Plan plan in Plans)
            foreach (string map in plan.Maps)
            {
                if (library.Find(map) != null) continue;
                string template = $"Assets/CubeSim/Arenas/Templates/{map}.txt";
                if (!File.Exists(template)) { Debug.LogWarning($"[CubeSim] No template for {map}"); continue; }
                GameObject prefab = AsciiArenaBuilder.Build(template, new AsciiArenaBuilder.Settings
                {
                    ArenaId = map, CourseSize = new Vector2(68f, 38f), WallHeight = 2.8f, VisualFillPadding = 22f,
                    DesignedCorridorWidth = 2.8f, BreakableHits = 3, MegaBlockHits = 60, RockTileCells = 2, RainbowLayerHits = 2,
                }, $"Assets/CubeSim/Arenas/Wave1/{map}.prefab");
                entries.RemoveAll(e => e.id == map);
                entries.Add(new CubeSim.Arena.AuthoredArenaLibrary.Entry { id = map, prefab = prefab });
                changed = true;
            }
            if (changed) { library.SetEntries(entries); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets(); }
        }

        [MenuItem("CubeSim/Build Mode Scenes (tier A+B)", priority = 8)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SceneFolder);
            var draw = new System.Random(unchecked(System.Environment.TickCount));
            var report = new System.Text.StringBuilder("[CubeSim] Mode scenes:\n");

            EnsurePrefabs();

            foreach (Plan plan in Plans)
            {
                Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
                var director = Object.FindFirstObjectByType<EpisodeDirector>();
                if (director == null) { Debug.LogError("[CubeSim] Episode scene has no director."); return; }

                var rounds = new List<EpisodeDirector.RoundSpec>();
                for (int i = 0; i < plan.Maps.Length; i++)
                {
                    rounds.Add(new EpisodeDirector.RoundSpec
                    {
                        arenaId = plan.Maps[i],
                        seed = draw.Next(100000, 999999),
                        winCondition = plan.Win,
                        maxDuration = plan.Duration,
                        maxHealth = plan.Health,
                        weaponCount = plan.Weapons,
                        racerCount = plan.Racers,
                        teamCount = plan.Teams,
                        requiredFinishers = plan.Finishers,
                        pressureProfile = plan.Pressure,
                        pressureStartDelay = plan.PressureDelay,
                        pressureSpeed = plan.PressureSpeed,
                        pressureTargetInset = plan.InsetX,
                        pressureTargetInsetZ = plan.InsetZ,
                        modeKind = plan.Mode,
                        eliminateCount = plan.Eliminate != null ? plan.Eliminate[i] : 0,
                        grandPrix = plan.GrandPrix,
                        ruleLabel = plan.GrandPrix ? "GRAND PRIX  ·  10-8-6-5-4-3-2-1 by finish order"
                            : plan.Eliminate != null ? $"ELIMINATION  ·  last {plan.Eliminate[i]} home are OUT"
                            : "",
                        portrait = false,
                    });
                }

                director.SetRounds(rounds);
                var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
                if (bootstrap != null)
                {
                    var bso = new SerializedObject(bootstrap);
                    bso.FindProperty("showLeaderboard").boolValue = true;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstrap);
                }
                EditorUtility.SetDirty(director);
                EditorSceneManager.SaveScene(scene, $"{SceneFolder}/M_{plan.Id}.unity");
                report.Append($"  M_{plan.Id}: {plan.Maps.Length} rounds\n");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }
    }
}
