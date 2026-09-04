using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the device showcase maps (DEV01 race gauntlet, DEV02 four-corner pit) into the
    /// arena library so they can be played from any episode scene by arena id.
    /// </summary>
    public static class DeviceTestMapBuilder
    {
        private const string TemplateFolder = "Assets/CubeSim/Arenas/Templates";
        private const string PrefabFolder = "Assets/CubeSim/Arenas/Wave1";
        private static readonly string[] Maps = { "DEV01", "DEV02", "DEV03", "DEV_LOOT", "DEV_SUMO", "DEV_OPEN" };

        [MenuItem("CubeSim/Build Device Test Maps", priority = 5)]
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

            foreach (string map in Maps)
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
                        BreakableHits = 3,
                        MegaBlockHits = 60,
                        RockTileCells = 2,
                        RainbowLayerHits = 2,
                        // Sumo: the void outside the ring is a fall, not a slow burn.
                        HazardDamagePerSecond = map == "DEV_SUMO" ? 0f : 1f,
                    },
                    $"{PrefabFolder}/{map}.prefab");

                entries.RemoveAll(e => e.id == map);
                entries.Add(new AuthoredArenaLibrary.Entry { id = map, prefab = prefab });
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[CubeSim] Device test maps built: " + string.Join(", ", Maps));
        }
    }
}
