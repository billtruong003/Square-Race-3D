using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// "Arena5v5" - the authored map from the 5 vs 5 reference video, built from its ASCII
    /// template. The template file is the map: geometry changes are edits to the file, never to
    /// this code, and the build diffs the prefab back against the template so they cannot drift.
    /// </summary>
    public static class Arena5v5MapBuilder
    {
        public const string ArenaId = "Arena5v5";
        public const string PrefabPath = "Assets/CubeSim/Arenas/Arena5v5.prefab";
        public const string TemplatePath = "Assets/CubeSim/Arenas/Templates/Arena5v5.txt";

        [MenuItem("CubeSim/Build 5v5 Reference Map", priority = 51)]
        public static GameObject Build()
        {
            GameObject prefab = AsciiArenaBuilder.Build(TemplatePath, new AsciiArenaBuilder.Settings
            {
                ArenaId = ArenaId,
                CourseSize = new Vector2(68f, 38f),
                WallHeight = 2.8f,

                // Outward-only mass so the camera never sees past the map; inner faces never move.
                VisualFillPadding = 22f,

                // The narrowest lane in the template is two cells of 68/48 m.
                DesignedCorridorWidth = 2.8f,
            }, PrefabPath);

            Debug.Log($"[CubeSim] 5v5 reference map built from {TemplatePath}");
            return prefab;
        }

        /// <summary>Registers this map alongside the serpentine one in the shared arena library.</summary>
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
