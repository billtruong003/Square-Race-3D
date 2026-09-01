using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds every template in Arenas/Templates/Pack into a prefab, registers it in the shared
    /// arena library, and reports one summary: which maps validate clean and which need their
    /// template fixed. The pack is what feeds the multi-round videos - one map is a round, a video
    /// is a hand of maps, and forty templates is weeks of uploads.
    /// </summary>
    public static class MapPackBuilder
    {
        public const string TemplateFolder = "Assets/CubeSim/Arenas/Templates/Pack";
        public const string PrefabFolder = "Assets/CubeSim/Arenas/Pack";

        [MenuItem("CubeSim/Build Map Pack", priority = 53)]
        public static List<string> BuildAll()
        {
            Directory.CreateDirectory(PrefabFolder);

            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            var entries = new List<AuthoredArenaLibrary.Entry>();
            if (library != null)
            {
                foreach (AuthoredArenaLibrary.Entry existing in library.Entries) entries.Add(existing);
            }

            var built = new List<string>();
            var failed = new List<string>();
            var summary = new StringBuilder("[CubeSim] Map pack build\n");

            foreach (string templatePath in Directory.GetFiles(TemplateFolder, "*.txt"))
            {
                string id = Path.GetFileNameWithoutExtension(templatePath);

                GameObject prefab;
                try
                {
                    prefab = AsciiArenaBuilder.Build(templatePath.Replace('\\', '/'),
                        new AsciiArenaBuilder.Settings
                        {
                            ArenaId = id,
                            CourseSize = new Vector2(68f, 38f),
                            WallHeight = 2.8f,
                            VisualFillPadding = 22f,
                            DesignedCorridorWidth = 2.8f,
                            BreakableHits = 12,
                            MegaBlockHits = 400,
                            RainbowLayerHits = 2,
                        }, $"{PrefabFolder}/{id}.prefab");
                }
                catch (System.Exception e)
                {
                    summary.AppendLine($"  FAIL  {id}: {e.Message}");
                    failed.Add(id);
                    continue;
                }

                string report = AuthoredArenaTools.Validate(
                    prefab.GetComponent<CubeSim.Arena.Authored.AuthoredArena>(), 2.0f);

                bool clean = report.Contains("errors=0");
                summary.AppendLine($"  {(clean ? " ok " : "ERR ")} {id}");
                if (!clean)
                {
                    summary.AppendLine(report);
                    failed.Add(id);
                    continue;
                }

                entries.RemoveAll(e => e.id == id);
                entries.Add(new AuthoredArenaLibrary.Entry { id = id, prefab = prefab });
                built.Add(id);
            }

            if (library != null)
            {
                library.SetEntries(entries);
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            summary.AppendLine($"  built {built.Count}, failed {failed.Count}");
            Debug.Log(summary.ToString());
            return failed;
        }
    }
}
