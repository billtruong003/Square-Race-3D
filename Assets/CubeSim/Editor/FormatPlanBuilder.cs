using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// The channel-scheduling batch: ten video formats, five videos each, six rounds a video -
    /// fifty scenes ready to record. Every video has a unique seed set, maps rotate through each
    /// format's pool so no two videos of a format play the same hand, and format character comes
    /// from the round overrides (hearts, weapon count, teams, squeeze) on the shared episode
    /// scene. Record them one by one, or hand the whole folder to the batch recorder.
    /// </summary>
    public static class FormatPlanBuilder
    {
        public const string SceneFolder = "Assets/CubeSim/Scenes/Batch";
        private const int VideosPerFormat = 5;
        private const int RoundsPerVideo = 6;

        private class Format
        {
            public string Tag;
            public string[] Maps;
            public WinCondition Win;
            public float Duration;
            public bool PinPressure;       // pinned short of the doors (breakable-heavy maps)
            public bool NoSqueeze;         // races: pressure parked out of the way entirely
            public float MaxHealth;
            public int WeaponCount = -1;
            public int TeamCount;
            public float SqueezeSpeed;     // >0: faster squeeze than the config
        }

        private static readonly string[] RacePool =
        {
            "Track01", "Track02", "Track03", "Track04", "Comb01", "Comb02", "Comb03", "Comb04",
            "Comb05", "Gauntlet01", "Gauntlet02", "Gauntlet03", "SampleLanes01", "SampleMaze01",
        };

        private static readonly string[] BattlePool =
            { "Open01", "Open02", "Open03", "Open04", "Open05", "Arena5v5", "SampleSaw01" };

        private static readonly Format[] Formats =
        {
            new Format { Tag = "Race",     Maps = RacePool, Win = WinCondition.ReachGoal, Duration = 130f, NoSqueeze = true },
            new Format { Tag = "Battle",   Maps = BattlePool, Win = WinCondition.LastAlive, Duration = 150f },
            new Format { Tag = "Team",     Maps = new[] { "Arena5v5", "Open01", "Open02", "Open03", "Open04", "Open05" },
                         Win = WinCondition.LastAlive, Duration = 150f, TeamCount = 2 },
            new Format { Tag = "Squeeze",  Maps = new[] { "Chamber01", "Chamber02", "Chamber03", "Chamber04", "Chamber05", "Chamber06", "Rooms01", "Rooms03", "Rooms05" },
                         Win = WinCondition.LastAlive, Duration = 150f, SqueezeSpeed = 0.3f },
            new Format { Tag = "Break",    Maps = new[] { "BlockBreak", "SampleBreakGrid01", "Chamber01", "Chamber03", "Chamber05", "Mega01", "Mega02", "Mega03", "Mega04", "Mega05" },
                         Win = WinCondition.ReachGoal, Duration = 150f, PinPressure = true },
            new Format { Tag = "Rainbow",  Maps = new[] { "Rainbow01", "Rainbow02", "Rainbow03", "Rainbow04", "Rainbow05", "Rainbow06" },
                         Win = WinCondition.ReachGoal, Duration = 130f, PinPressure = true },
            new Format { Tag = "Rotor",    Maps = new[] { "SampleSaw01", "Open01", "SampleSaw01", "Open03", "SampleSaw01", "Open05" },
                         Win = WinCondition.LastAlive, Duration = 150f },
            new Format { Tag = "Hazard",   Maps = new[] { "Garden01", "Garden02", "Garden03", "Garden04", "Garden05", "Rooms02", "Rooms04" },
                         Win = WinCondition.LastAlive, Duration = 150f },
            new Format { Tag = "Sudden",   Maps = new[] { "Open01", "Open02", "Open03", "Open04", "Open05", "Comb01", "Comb02", "Comb03", "Comb04", "Comb05" },
                         Win = WinCondition.LastAlive, Duration = 130f, MaxHealth = 1f },
            new Format { Tag = "Frenzy",   Maps = new[] { "Open01", "Open02", "Open03", "Open04", "Open05", "Mega01", "Mega02", "Mega03", "Mega04", "Arena5v5" },
                         Win = WinCondition.LastAlive, Duration = 150f, WeaponCount = 6 },
        };

        [MenuItem("CubeSim/Build Format Plan (50 videos)", priority = 5)]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(SceneFolder);

            int sceneCount = 0;
            for (int f = 0; f < Formats.Length; f++)
            {
                Format format = Formats[f];
                for (int v = 0; v < VideosPerFormat; v++)
                {
                    Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath,
                        OpenSceneMode.Single);

                    var director = Object.FindFirstObjectByType<EpisodeDirector>();
                    if (director == null)
                    {
                        Debug.LogError("[CubeSim] Episode scene has no director; aborting format plan.");
                        return;
                    }

                    var rounds = new List<EpisodeDirector.RoundSpec>();
                    for (int r = 0; r < RoundsPerVideo; r++)
                    {
                        var spec = new EpisodeDirector.RoundSpec
                        {
                            arenaId = format.Maps[(v * RoundsPerVideo + r) % format.Maps.Length],
                            seed = 500000 + f * 1000 + v * 100 + r,
                            winCondition = format.Win,
                            maxDuration = format.Duration,
                            maxHealth = format.MaxHealth,
                            weaponCount = format.WeaponCount,
                            teamCount = format.TeamCount,
                        };

                        if (format.NoSqueeze)
                        {
                            spec.pressureStartDelay = 100000f;
                        }
                        else if (format.PinPressure)
                        {
                            spec.pressureTargetInset = 22f;
                            spec.pressureStartDelay = 10f;
                            spec.pressureSpeed = 0.25f;
                        }
                        else if (format.SqueezeSpeed > 0f)
                        {
                            spec.pressureSpeed = format.SqueezeSpeed;
                        }

                        rounds.Add(spec);
                    }

                    director.SetRounds(rounds);
                    EditorUtility.SetDirty(director);

                    string path = $"{SceneFolder}/F{f + 1:D2}_{format.Tag}_V{v + 1}.unity";
                    EditorSceneManager.SaveScene(scene, path);
                    sceneCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CubeSim] Format plan built: {sceneCount} scenes in {SceneFolder}. " +
                      "Run CubeSim/Record Batch to record them all.");
        }
    }
}
