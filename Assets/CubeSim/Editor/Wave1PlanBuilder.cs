using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Arena;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Wave 1 of the niche test: six formats built strictly to VIDEO_FORMAT_RULES.md, each with
    /// its own rule-compliant map library (TW/RC/SD/SW/RB/RM prefixes, 54 maps), two videos per
    /// format, and a round count that never exceeds the pool - so no map can repeat inside a
    /// video, and the two videos of a format start at different points in the pool - plus
    /// a fresh random seed drawn per round at build time - the "boc secret". Seeds are stamped
    /// into the scenes, so a recording is still perfectly reproducible afterwards.
    /// </summary>
    public static class Wave1PlanBuilder
    {
        public const string SceneFolder = "Assets/CubeSim/Scenes/Wave1";
        private const string TemplateFolder = "Assets/CubeSim/Arenas/Templates";
        private const string PrefabFolder = "Assets/CubeSim/Arenas/Wave1";
        private const int VideosPerFormat = 2;

        private class Format
        {
            public string Tag;
            public string[] Maps;
            public WinCondition Win;
            public float Duration;
            public float MaxHealth;
            public int WeaponCount = -1;
            public int RacerCount;
            public int TeamCount;
            public float SawDamage = 0.5f;
            public int Finishers = 1;

            /// <summary>
            /// Rounds in one video. Measured, not guessed: a goal race is over in ~25-30s, so it
            /// needs three times the rounds of a survival format to fill the same nine minutes.
            /// </summary>
            public int Rounds;

            // Pressure: what the squeeze is for on this format's maps.
            public EpisodeDirector.PressureProfile Pressure = EpisodeDirector.PressureProfile.Park;
            public float PressureDelay = 10f;
            public float PressureSpeed = 0.3f;
            public float PressureInsetX = 22f;     // Chase: stop before the goal. Collapse: pit half-width.
            public float PressureInsetZ = 12f;     // Collapse only.

            // Builder knobs for this format's maps.
            public int BreakableHits = 12;
            public int MegaHits = 400;
            public int RockTile;                   // >0: 'B' masses cut into boulders this many cells a side
        }

        private static readonly Format[] Formats =
        {
            // F01 Team Knife War: 10v10, sealed pens, no goal/hazard/rotor, slow squeeze.
            new Format
            {
                Tag = "TeamWar", Maps = new[] { "TW01", "TW02", "TW03", "TW04", "TW05", "TW06", "TW07", "TW08", "TW09", "TW10", "TW14", "TW15" },
                Win = WinCondition.LastTeamAlive, Duration = 300f, Rounds = 6,
                WeaponCount = 2, RacerCount = 20, TeamCount = 2,
                Pressure = EpisodeDirector.PressureProfile.Collapse,
                PressureDelay = 20f, PressureSpeed = 0.45f, PressureInsetX = 29f, PressureInsetZ = 15f,
                BreakableHits = 60,   // one merged pen door vs ten racers (~2.5-5 hits/s): sealed 12-24s
            },
            // F03 Race: goal mandatory, squeeze never kills, and NO knives - killing racers
            // just thins the field that has to open the course. Rounds run ~28s, so a nine
            // minute video needs eighteen of them.
            new Format
            {
                Tag = "Race", Maps = new[] { "RC01", "RC02", "RC03", "RC04", "RC05", "RC06", "RC07", "RC08", "RC09", "RC10", "RC11", "RC12", "RC13", "RC14", "RC15", "RC16", "RC17", "RC18", "RC19", "RC20", "RC21", "RC22", "RC23", "RC24", "RC25", "RC26", "RC27", "RC28", "RC29", "RC30" },
                Win = WinCondition.ReachGoal, Duration = 300f, Finishers = 3, Rounds = 12,
                WeaponCount = 0,
                Pressure = EpisodeDirector.PressureProfile.Chase,
                PressureDelay = 10f, PressureSpeed = 0.5f, PressureInsetX = 54f,
            },
            // F04 Sudden Death: one heart, no goal, no hazard, fast squeeze.
            new Format
            {
                Tag = "Sudden", Maps = new[] { "SD01", "SD02", "SD03", "SD04", "SD05", "SD06", "SD07", "SD08", "SD09", "SD10", "SD11", "SD12", "SD13", "SD14", "SD15", "SD16", "SD17", "SD18", "SD19", "SD20" },
                Win = WinCondition.LastAlive, Duration = 300f, Rounds = 6,
                MaxHealth = 1f, WeaponCount = 1,
                Pressure = EpisodeDirector.PressureProfile.Collapse,
                PressureDelay = 8f, PressureSpeed = 0.5f, PressureInsetX = 29f, PressureInsetZ = 15f,
            },
            // F08 Saw Gauntlet: the rotors are the killer, one knife only, slow squeeze feeds them.
            new Format
            {
                Tag = "Saw", Maps = new[] { "SW01", "SW02", "SW03", "SW04", "SW05", "SW06", "SW07", "SW08", "SW09", "SW10", "SW11", "SW12", "SW13", "SW14", "SW15" },
                Win = WinCondition.LastAlive, Duration = 300f, Rounds = 6,
                WeaponCount = 1, SawDamage = 1f,
                Pressure = EpisodeDirector.PressureProfile.Collapse,
                PressureDelay = 15f, PressureSpeed = 0.5f, PressureInsetX = 29f, PressureInsetZ = 15f,
            },
            // F07 Rainbow Rush: colour gates (only the matching cube opens a pane) plus white
            // panes anyone can grind down. No knives - every cube lost is a colour that can no
            // longer open its own gate.
            new Format
            {
                Tag = "Rainbow", Maps = new[] { "RB01", "RB02", "RB03", "RB04", "RB05", "RB06", "RB07", "RB08", "RB09", "RB10", "RB11", "RB12", "RB13", "RB14", "RB15", "RB16" },
                Win = WinCondition.ReachGoal, Duration = 300f, Finishers = 3, Rounds = 8,
                WeaponCount = 0,
                Pressure = EpisodeDirector.PressureProfile.Chase,
                PressureDelay = 12f, PressureSpeed = 0.4f, PressureInsetX = 54f,
            },
            // F06 Rock Mine: dig through the rock to the goal. No knives - the diggers are
            // the resource.
            new Format
            {
                Tag = "RockMine", Maps = new[] { "RM01", "RM02", "RM03", "RM04", "RM05", "RM06", "RM07", "RM08", "RM09", "RM10", "RM11", "RM12", "RM13", "RM14", "RM15", "RM16" },
                Win = WinCondition.ReachGoal, Duration = 300f, Finishers = 3, Rounds = 8,
                WeaponCount = 0,
                Pressure = EpisodeDirector.PressureProfile.Chase,
                PressureDelay = 15f, PressureSpeed = 0.35f, PressureInsetX = 54f,
                // Boulders of 2x2 cells, 3 hits each: a mine that gets tunnelled, not a slab that
                // pops. Plugs at 60 hits are a real dig for ten racers, not a 250-hit wall nobody
                // finishes before the squeeze arrives.
                BreakableHits = 3, MegaHits = 60, RockTile = 2,
            },
            // F07 Coin Rush: no goal, no knives, the clock decides - most coins wins. Pressure
            // parked: the field must stay open for the coins to keep respawning into play.
            new Format
            {
                Tag = "CoinRush", Maps = new[] { "CR01", "CR02", "CR03", "CR04", "CR05", "CR06", "CR07", "CR08", "CR09", "CR10" },
                Win = WinCondition.MostCoins, Duration = 90f, Rounds = 5,
                WeaponCount = 0,
                Pressure = EpisodeDirector.PressureProfile.Park,
            },
            // F08 Four-Way War: four sealed pens, four teams of five, collapse to the centre pit.
            new Format
            {
                Tag = "FourWay", Maps = new[] { "TW11", "TW12", "TW13", "TW16", "TW17", "TW18" },
                Win = WinCondition.LastTeamAlive, Duration = 300f, Rounds = 6,
                WeaponCount = 2, RacerCount = 20, TeamCount = 4,
                Pressure = EpisodeDirector.PressureProfile.Collapse,
                PressureDelay = 25f, PressureSpeed = 0.45f, PressureInsetX = 29f, PressureInsetZ = 15f,
                BreakableHits = 60,
            },
        };

        [MenuItem("CubeSim/Build Wave 1 (formats + 12 videos)", priority = 4)]
        public static void BuildAll()
        {
            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null)
            {
                Debug.LogError("[CubeSim] Arena library missing; build the map pack first.");
                return;
            }

            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries) entries.Add(existing);

            foreach (Format format in Formats)
            {
                foreach (string map in format.Maps)
                {
                    GameObject prefab = AsciiArenaBuilder.Build(
                        $"{TemplateFolder}/{map}.txt",
                        new AsciiArenaBuilder.Settings
                        {
                            ArenaId = map,
                            CourseSize = new Vector2(68f, 38f),
                            WallHeight = 2.8f,
                            VisualFillPadding = 22f,
                            DesignedCorridorWidth = 2.8f,
                            BreakableHits = format.BreakableHits,
                            MegaBlockHits = format.MegaHits,
                            RockTileCells = format.RockTile,
                            RainbowLayerHits = 2,
                            SawDamage = format.SawDamage,
                        },
                        $"{PrefabFolder}/{map}.prefab");

                    entries.RemoveAll(e => e.id == map);
                    entries.Add(new AuthoredArenaLibrary.Entry { id = map, prefab = prefab });
                }
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            BuildScenes();
        }

        private static void BuildScenes()
        {
            System.IO.Directory.CreateDirectory(SceneFolder);

            // The secret draw: a fresh RNG per build. Seeds land in the scenes (and the log), so
            // every video is a surprise at build time yet fully reproducible afterwards.
            var draw = new System.Random(unchecked(System.Environment.TickCount));
            var report = new System.Text.StringBuilder("[CubeSim] Wave 1 seeds:\n");

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
                        Debug.LogError("[CubeSim] Episode scene has no director; aborting wave 1.");
                        return;
                    }

                    string name = $"W1_{format.Tag}_V{v + 1}";
                    report.Append($"  {name}:");

                    int roundCount = Mathf.Max(1, format.Rounds);

                    // Each video deals its format's maps in its own shuffled order (Fisher-Yates on
                    // the same draw), so two videos of one format never show the same sequence of
                    // layouts even when the pool is exactly one video long. Inside a video a map
                    // still never repeats: rounds never exceed the pool.
                    string[] order = (string[])format.Maps.Clone();
                    for (int i = order.Length - 1; i > 0; i--)
                    {
                        int j = draw.Next(i + 1);
                        (order[i], order[j]) = (order[j], order[i]);
                    }

                    var rounds = new List<EpisodeDirector.RoundSpec>();
                    for (int r = 0; r < roundCount; r++)
                    {
                        int seed = draw.Next(100000, 999999);
                        report.Append($" {order[r % order.Length]}:{seed}");

                        var spec = new EpisodeDirector.RoundSpec
                        {
                            arenaId = order[r % order.Length],
                            seed = seed,
                            winCondition = format.Win,
                            maxDuration = format.Duration,
                            requiredFinishers = format.Finishers,
                            maxHealth = format.MaxHealth,
                            weaponCount = format.WeaponCount,
                            racerCount = format.RacerCount,
                            teamCount = format.TeamCount,
                        };

                        spec.pressureProfile = format.Pressure;
                        spec.pressureStartDelay = format.PressureDelay;
                        spec.pressureSpeed = format.PressureSpeed;
                        spec.pressureTargetInset = format.PressureInsetX;
                        spec.pressureTargetInsetZ = format.PressureInsetZ;

                        rounds.Add(spec);
                    }

                    report.Append('\n');
                    director.SetRounds(rounds);
                    EditorUtility.SetDirty(director);

                    EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{name}.unity");
                    sceneCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            Debug.Log($"[CubeSim] Wave 1 built: {sceneCount} video scenes in {SceneFolder}. " +
                      "Record with CubeSim/Record Wave 1.");
        }
    }
}
