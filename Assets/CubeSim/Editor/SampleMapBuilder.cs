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
    /// Builds the Simulation-Central-inspired sample maps for audit - one map per archetype
    /// pulled from the channel's thumbnails: the dense maze race, the big central saw/rotor
    /// arena, the hurdle-lane relay, and the chew-through breakable field - plus an audit scene
    /// that plays them back to back with the pressure squeeze pinned out of the way.
    /// </summary>
    public static class SampleMapBuilder
    {
        private const string TemplateFolder = "Assets/CubeSim/Arenas/Templates";
        private const string PrefabFolder = "Assets/CubeSim/Arenas/Samples";
        private const string ScenePath = "Assets/CubeSim/Scenes/CubeSimulation_MapAudit.unity";

        private struct Sample
        {
            public string Id;
            public int BreakableHits;
            public WinCondition Win;
            public float Duration;
        }

        private static readonly Sample[] Samples =
        {
            new Sample { Id = "SampleMaze01",      BreakableHits = 12, Win = WinCondition.ReachGoal, Duration = 130f },
            new Sample { Id = "SampleSaw01",       BreakableHits = 12, Win = WinCondition.LastAlive, Duration = 150f },
            new Sample { Id = "SampleLanes01",     BreakableHits = 8,  Win = WinCondition.ReachGoal, Duration = 130f },
            // The block field is 80+ small breakables; three hits each keeps the chew moving.
            new Sample { Id = "SampleBreakGrid01", BreakableHits = 3,  Win = WinCondition.ReachGoal, Duration = 150f },
        };

        [MenuItem("CubeSim/Build Sample Maps (audit)", priority = 7)]
        public static void Build()
        {
            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null)
            {
                Debug.LogError("[CubeSim] Arena library missing; build the map pack first.");
                return;
            }

            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries) entries.Add(existing);

            foreach (Sample sample in Samples)
            {
                GameObject prefab = AsciiArenaBuilder.Build(
                    $"{TemplateFolder}/{sample.Id}.txt",
                    new AsciiArenaBuilder.Settings
                    {
                        ArenaId = sample.Id,
                        CourseSize = new Vector2(68f, 38f),
                        WallHeight = 2.8f,
                        VisualFillPadding = 22f,
                        DesignedCorridorWidth = 2.8f,
                        BreakableHits = sample.BreakableHits,
                        MegaBlockHits = 400,
                        RainbowLayerHits = 2,
                    },
                    $"{PrefabFolder}/{sample.Id}.prefab");

                entries.RemoveAll(e => e.id == sample.Id);
                entries.Add(new AuthoredArenaLibrary.Entry { id = sample.Id, prefab = prefab });
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            BuildAuditScene();
        }

        /// <summary>One round per sample, squeeze pinned away so the layouts can be judged.</summary>
        private static void BuildAuditScene()
        {
            Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);

            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director == null)
            {
                Debug.LogError("[CubeSim] Episode scene has no director; audit scene not built.");
                return;
            }

            var rounds = new List<EpisodeDirector.RoundSpec>();
            for (int i = 0; i < Samples.Length; i++)
            {
                rounds.Add(new EpisodeDirector.RoundSpec
                {
                    arenaId = Samples[i].Id,
                    seed = 909000 + i,
                    winCondition = Samples[i].Win,
                    maxDuration = Samples[i].Duration,
                    pressureStartDelay = 100000f,
                    pressureSpeed = 0f,
                });
            }

            director.SetRounds(rounds);
            EditorUtility.SetDirty(director);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CubeSim] Sample maps built and audit scene saved to {ScenePath}.");
        }
    }
}
