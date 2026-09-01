using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Lays the map pack out into eight ready-to-record videos of about ten minutes each: six
    /// rounds per video, formats alternating between goal races and last-standing fights, no map
    /// repeated within a video, fixed seeds throughout. Building produces eight scenes - recording
    /// one is: open it, run "Record Episode", upload the file.
    ///
    /// Chamber-style maps get the pinned squeeze (the field ends up hammering the doors); open maps
    /// keep the full sweep.
    /// </summary>
    public static class VideoPlanBuilder
    {
        private struct Round
        {
            public string Map;
            public bool Race; // true: first to the goal; false: last one standing

            public Round(string map, bool race) { Map = map; Race = race; }
        }

        /// <summary>Six rounds a video, formats interleaved, every family visited.</summary>
        private static readonly Round[][] Videos =
        {
            new[] { new Round("Comb01", false), new Round("Chamber01", true), new Round("Garden01", false),
                    new Round("Rainbow01", true), new Round("Mega01", true), new Round("Open01", false) },
            new[] { new Round("Comb02", false), new Round("Rooms01", true), new Round("Garden02", false),
                    new Round("Rainbow02", true), new Round("Chamber02", true), new Round("Gauntlet01", true) },
            new[] { new Round("Open02", false), new Round("Chamber03", true), new Round("Rainbow03", true),
                    new Round("Garden03", false), new Round("Mega02", true), new Round("Comb03", false) },
            new[] { new Round("Rooms02", true), new Round("Gauntlet02", true), new Round("Garden04", false),
                    new Round("Chamber04", true), new Round("Rainbow04", true), new Round("Open03", false) },
            new[] { new Round("Comb04", false), new Round("Mega03", true), new Round("Rooms03", true),
                    new Round("Garden05", false), new Round("Chamber05", true), new Round("Gauntlet03", true) },
            new[] { new Round("Open04", false), new Round("Rainbow05", true), new Round("Rooms04", true),
                    new Round("Mega04", true), new Round("Comb05", false), new Round("Chamber06", true) },
            new[] { new Round("Gauntlet04", true), new Round("Track01", true), new Round("Rainbow06", true),
                    new Round("Rooms05", true), new Round("Mega05", true), new Round("Open05", false) },
            new[] { new Round("BlockBreak", true), new Round("Arena5v5", false), new Round("Track02", true),
                    new Round("Gauntlet05", true), new Round("Track03", false), new Round("Track04", true) },
        };

        /// <summary>Chamber-like families get the squeeze pinned short of their doors.</summary>
        private static bool PinsPressure(string map) =>
            map.StartsWith("Chamber") || map.StartsWith("Mega") || map.StartsWith("Rainbow") ||
            map.StartsWith("Rooms") || map.StartsWith("Gauntlet") || map.StartsWith("Track") ||
            map == "BlockBreak";

        [MenuItem("CubeSim/Build Video Scenes (8)", priority = 4)]
        public static void BuildAll()
        {
            // Everything a video depends on, rebuilt once up front.
            CubeSimSceneBuilder.BuildEpisodeScene();
            MapPackBuilder.BuildAll();

            for (int v = 0; v < Videos.Length; v++)
            {
                string path = $"Assets/CubeSim/Scenes/CubeSimulation_Video{v + 1:D2}.unity";

                Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath,
                    OpenSceneMode.Single);

                var director = Object.FindFirstObjectByType<EpisodeDirector>();
                if (director == null)
                {
                    Debug.LogError("[CubeSim] Episode scene has no director; aborting video build.");
                    return;
                }

                var rounds = new List<EpisodeDirector.RoundSpec>();
                for (int r = 0; r < Videos[v].Length; r++)
                {
                    Round round = Videos[v][r];
                    var spec = new EpisodeDirector.RoundSpec
                    {
                        arenaId = round.Map,
                        seed = 202600 + v * 100 + r,
                        winCondition = round.Race ? WinCondition.ReachGoal : WinCondition.LastAlive,
                        maxDuration = round.Race ? 130f : 150f,
                    };

                    if (PinsPressure(round.Map))
                    {
                        spec.pressureTargetInset = 22f;
                        spec.pressureStartDelay = 10f;
                        spec.pressureSpeed = 0.25f;
                    }

                    rounds.Add(spec);
                }

                director.SetRounds(rounds);
                EditorUtility.SetDirty(director);
                EditorSceneManager.SaveScene(scene, path);
                Debug.Log($"[CubeSim] Video scene {v + 1}/8 saved to {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[CubeSim] All 8 video scenes built. Open one, run CubeSim/Record Episode.");
        }
    }
}
