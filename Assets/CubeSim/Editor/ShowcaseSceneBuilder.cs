using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Arena;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the visual-review scene: the Showcase map (one exhibit of every element - wall,
    /// breakable, mega block, rainbow gate, door, rotor, goal, hazard, food field, weapon areas,
    /// spawns) run through the exact production pipeline, plus a <see cref="ShowcaseLabeler"/>
    /// that hangs a name tag over every element and every racer. Open the scene, press play,
    /// judge each visual by name.
    /// </summary>
    public static class ShowcaseSceneBuilder
    {
        private const string TemplatePath = "Assets/CubeSim/Arenas/Templates/Showcase.txt";
        private const string PrefabPath = "Assets/CubeSim/Arenas/Showcase.prefab";
        private const string ScenePath = "Assets/CubeSim/Scenes/CubeSimulation_Showcase.unity";

        [MenuItem("CubeSim/Build Showcase Scene", priority = 6)]
        public static void Build()
        {
            // The exhibit map, through the same builder and validation as every real map.
            GameObject prefab = AsciiArenaBuilder.Build(TemplatePath, new AsciiArenaBuilder.Settings
            {
                ArenaId = "Showcase",
                CourseSize = new Vector2(68f, 38f),
                WallHeight = 2.8f,
                VisualFillPadding = 22f,
                DesignedCorridorWidth = 2.8f,
                // Review durability: exhibits must survive the swarm long enough to be looked at.
                BreakableHits = 60,
                MegaBlockHits = 400,
                RainbowLayerHits = 40,
            }, PrefabPath);

            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null)
            {
                Debug.LogError("[CubeSim] Arena library missing; build the map pack first.");
                return;
            }

            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries)
            {
                if (existing.id != "Showcase") entries.Add(existing);
            }
            entries.Add(new AuthoredArenaLibrary.Entry { id = "Showcase", prefab = prefab });
            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            // One endless round on the exhibit map: pressure pinned far away so nothing squeezes
            // the gallery shut mid-review.
            Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);

            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director == null)
            {
                Debug.LogError("[CubeSim] Episode scene has no director; aborting showcase build.");
                return;
            }

            director.SetRounds(new List<EpisodeDirector.RoundSpec>
            {
                new EpisodeDirector.RoundSpec
                {
                    arenaId = "Showcase",
                    seed = 424242,
                    winCondition = WinCondition.None,
                    maxDuration = 5990f,
                    pressureStartDelay = 100000f,
                    pressureSpeed = 0f,
                    // Asset review only: nobody spawns, the exhibits just sit there to be looked at.
                    racerCount = -1,
                    weaponCount = 0,
                    pressureProfile = EpisodeDirector.PressureProfile.Park,
                }
            });
            EditorUtility.SetDirty(director);

            var labeler = new GameObject("ShowcaseLabels");
            labeler.AddComponent<ShowcaseLabeler>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CubeSim] Showcase scene saved to {ScenePath}. Open it and press play to review.");
        }
    }
}
