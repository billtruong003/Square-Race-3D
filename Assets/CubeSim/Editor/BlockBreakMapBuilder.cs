using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// "BlockBreak" - the break-the-blocks arena: a sealed goal chamber whose doors are breakable
    /// walls with hit countdowns. Built entirely from its ASCII template.
    /// </summary>
    public static class BlockBreakMapBuilder
    {
        public const string ArenaId = "BlockBreak";
        public const string PrefabPath = "Assets/CubeSim/Arenas/BlockBreak.prefab";
        public const string TemplatePath = "Assets/CubeSim/Arenas/Templates/BlockBreak.txt";

        [MenuItem("CubeSim/Build BlockBreak Map", priority = 52)]
        public static GameObject Build()
        {
            GameObject prefab = AsciiArenaBuilder.Build(TemplatePath, new AsciiArenaBuilder.Settings
            {
                ArenaId = ArenaId,
                CourseSize = new Vector2(68f, 38f),
                WallHeight = 2.8f,
                VisualFillPadding = 22f,
                DesignedCorridorWidth = 4.2f,

                // Measured, not guessed: a 140s test run registered 83 impacts spread over the six
                // doors - at 30 hits nothing opened and the pressure crushed the field outside.
                // Twelve opens the busiest door around the minute mark.
                BreakableHits = 12,
            }, PrefabPath);

            Debug.Log($"[CubeSim] BlockBreak map built from {TemplatePath}");
            return prefab;
        }

        public static void RegisterInLibrary(GameObject prefab)
        {
            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null) return;

            var entries = new List<AuthoredArenaLibrary.Entry>();
            foreach (AuthoredArenaLibrary.Entry existing in library.Entries)
            {
                if (existing.id != ArenaId) entries.Add(existing);
            }

            entries.Add(new AuthoredArenaLibrary.Entry { id = ArenaId, prefab = prefab });
            library.SetEntries(entries);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
