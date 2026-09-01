using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EditorTools
{
    public class TextureOptimizer : EditorWindow
    {
        // ─── Data Models ───────────────────────────────────────────────
        private class TextureInfo
        {
            public string       Path;
            public TextureImporter Importer;
            public int          OriginalMaxSize;
            public int          RecommendedSize;
            public bool         OriginalMipMap;
            public bool         OriginalCrunch;
            public int          OriginalCrunchQuality;
            public TextureImporterCompression OriginalCompression;
            public int          ActualWidth;
            public int          ActualHeight;
            public bool         NeedsResize;
            public bool         NeedsMipMap;
            public bool         NeedsCompression;
            public bool         NeedsCrunch;
            public bool         IsSelected = true;

            public bool HasIssue => NeedsResize || NeedsMipMap || NeedsCompression || NeedsCrunch;
        }

        // ─── Folder Tree Node ──────────────────────────────────────────
        private class FolderNode
        {
            public string           Name;
            public string           FullPath;
            public bool             Expanded;
            public bool             Selected = true;
            public List<FolderNode> Children  = new List<FolderNode>();
            public List<string>     Textures  = new List<string>();

            public int TotalTextureCount
            {
                get
                {
                    int c = Textures.Count;
                    foreach (var ch in Children) c += ch.TotalTextureCount;
                    return c;
                }
            }

            public long TotalFileSize
            {
                get
                {
                    long s = 0;
                    foreach (var t in Textures)
                    {
                        var fi = new FileInfo(t);
                        if (fi.Exists) s += fi.Length;
                    }
                    foreach (var ch in Children) s += ch.TotalFileSize;
                    return s;
                }
            }

            public void SetSelectedRecursive(bool val)
            {
                Selected = val;
                foreach (var ch in Children) ch.SetSelectedRecursive(val);
            }

            public List<string> GetSelectedTextures()
            {
                var result = new List<string>();
                if (Selected) result.AddRange(Textures);
                foreach (var ch in Children) result.AddRange(ch.GetSelectedTextures());
                return result;
            }

            public List<string> GetAllTextures()
            {
                var result = new List<string>(Textures);
                foreach (var ch in Children) result.AddRange(ch.GetAllTextures());
                return result;
            }
        }

        // ─── Settings ──────────────────────────────────────────────────
        private int     _maxTextureSize    = 2048;
        private bool    _enableMipMaps     = true;
        private bool    _autoFitSize       = true;
        private bool    _enableCrunch      = true;
        private int     _crunchQuality     = 50;
        private TextureImporterCompression _compression = TextureImporterCompression.Compressed;
        private string  _searchFolder      = "Assets";

        // ─── State ─────────────────────────────────────────────────────
        private List<TextureInfo> _textures       = new List<TextureInfo>();
        private List<string>      _unusedTextures  = new List<string>();
        private FolderNode        _unusedTree;
        private Vector2 _scrollTextures;
        private Vector2 _scrollUnused;
        private bool    _scanned;
        private bool    _selectAllOptimize = true;
        private int     _totalCount;
        private int     _issueCount;
        private long    _estimatedSavedBytes;

        // ─── Styles ────────────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _tagBad;
        private GUIStyle _tagInfo;
        private GUIStyle _folderStyle;
        private GUIStyle _fileStyle;
        private GUIStyle _statsStyle;
        private bool     _stylesReady;

        // ─── Tab ───────────────────────────────────────────────────────
        private int _tab;
        private readonly string[] _tabNames = { "Scan & Optimize", "Unused Textures" };

        // ─── POT Sizes ─────────────────────────────────────────────────
        private static readonly int[] POT_SIZES = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        [MenuItem("Tools/Texture Optimizer %#t")]
        public static void Open()
        {
            var w = GetWindow<TextureOptimizer>("Texture Optimizer");
            w.minSize = new Vector2(680, 520);
        }

        // ================================================================
        //  GUI
        // ================================================================
        private void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("TEXTURE OPTIMIZER", _headerStyle);
            EditorGUILayout.Space(4);

            _tab = GUILayout.Toolbar(_tab, _tabNames, GUILayout.Height(28));
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case 0: DrawOptimizeTab(); break;
                case 1: DrawUnusedTab();   break;
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 0 – Scan & Optimize
        // ────────────────────────────────────────────────────────────────
        private void DrawOptimizeTab()
        {
            // ── Settings ──
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            _searchFolder   = EditorGUILayout.TextField("Search Folder", _searchFolder);
            _maxTextureSize = EditorGUILayout.IntPopup("Max Texture Size",
                _maxTextureSize,
                new[] { "256", "512", "1024", "2048" },
                new[] { 256, 512, 1024, 2048 });

            _autoFitSize = EditorGUILayout.Toggle(
                new GUIContent("Auto-Fit Size",
                    "If actual texture pixels < maxSize, snap down to the smallest Power-of-Two that still fits the texture. " +
                    "E.g. a 300x200 texture will get maxSize=512 instead of 2048."),
                _autoFitSize);

            _enableMipMaps = EditorGUILayout.Toggle("Generate MipMaps", _enableMipMaps);
            _compression   = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", _compression);

            EditorGUILayout.Space(2);
            _enableCrunch = EditorGUILayout.Toggle(
                new GUIContent("Crunch Compression",
                    "Enable lossy crunch compression on top of the selected compression mode. " +
                    "Greatly reduces file size on disk and download size."),
                _enableCrunch);

            EditorGUI.BeginDisabledGroup(!_enableCrunch);
            _crunchQuality = EditorGUILayout.IntSlider("  Crunch Quality", _crunchQuality, 0, 100);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            // ── Buttons ──
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("Scan Textures", GUILayout.Height(30)))
                ScanTextures();
            GUI.backgroundColor = Color.white;

            EditorGUI.BeginDisabledGroup(!_scanned || _issueCount == 0);
            GUI.backgroundColor = new Color(0.3f, 1f, 0.5f);
            if (GUILayout.Button("Apply Selected Optimizations", GUILayout.Height(30)))
                ApplyOptimizations();
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!_scanned) return;

            // ── Stats ──
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"Total: {_totalCount}  |  Issues: {_issueCount}  |  Est. savings: ~{FormatBytes(_estimatedSavedBytes)}",
                _statsStyle);
            EditorGUILayout.EndVertical();

            // ── Select all ──
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            bool newAll = EditorGUILayout.ToggleLeft("Select / Deselect All", _selectAllOptimize, EditorStyles.boldLabel);
            if (newAll != _selectAllOptimize)
            {
                _selectAllOptimize = newAll;
                foreach (var t in _textures.Where(x => x.HasIssue))
                    t.IsSelected = _selectAllOptimize;
            }
            EditorGUILayout.EndHorizontal();

            // ── Results list ──
            _scrollTextures = EditorGUILayout.BeginScrollView(_scrollTextures);
            foreach (var info in _textures)
            {
                if (!info.HasIssue) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                info.IsSelected = EditorGUILayout.Toggle(info.IsSelected, GUILayout.Width(20));

                if (GUILayout.Button(info.Path, EditorStyles.label))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(info.Path));

                EditorGUILayout.EndHorizontal();

                // Tags
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(26);

                if (info.NeedsResize)
                {
                    GUILayout.Label($"Size {info.OriginalMaxSize} \u2192 {info.RecommendedSize}", _tagBad);

                    if (_autoFitSize && info.RecommendedSize < _maxTextureSize)
                        GUILayout.Label($"actual {info.ActualWidth}\u00d7{info.ActualHeight}", _tagInfo);
                }

                if (info.NeedsMipMap)
                    GUILayout.Label("MipMap OFF \u2192 ON", _tagBad);
                if (info.NeedsCompression)
                    GUILayout.Label($"{info.OriginalCompression} \u2192 {_compression}", _tagBad);
                if (info.NeedsCrunch)
                {
                    string crunchLabel = !info.OriginalCrunch
                        ? $"Crunch OFF \u2192 ON (Q{_crunchQuality})"
                        : $"Crunch Q{info.OriginalCrunchQuality} \u2192 Q{_crunchQuality}";
                    GUILayout.Label(crunchLabel, _tagBad);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 1 – Unused Textures (Folder Tree)
        // ────────────────────────────────────────────────────────────────
        private void DrawUnusedTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Detect textures not referenced by any asset. Results shown as a folder tree \u2014 " +
                "toggle entire branches or individual textures before deleting.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
            if (GUILayout.Button("Find Unused Textures", GUILayout.Height(30)))
            {
                FindUnusedTextures();
                _unusedTree = BuildFolderTree(_unusedTextures);
            }
            GUI.backgroundColor = Color.white;

            if (_unusedTree == null || _unusedTree.TotalTextureCount == 0)
            {
                EditorGUILayout.HelpBox("No unused textures found (or not scanned yet).", MessageType.Info);
                return;
            }

            // Stats
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var selectedPaths = _unusedTree.GetSelectedTextures();
            long selectedSize = 0;
            foreach (var p in selectedPaths)
            {
                var fi = new FileInfo(p);
                if (fi.Exists) selectedSize += fi.Length;
            }
            EditorGUILayout.LabelField(
                $"Total unused: {_unusedTree.TotalTextureCount}  |  " +
                $"Total size: {FormatBytes(_unusedTree.TotalFileSize)}  |  " +
                $"Selected: {selectedPaths.Count} ({FormatBytes(selectedSize)})",
                _statsStyle);
            EditorGUILayout.EndVertical();

            // Action buttons row 1
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
                _unusedTree.SetSelectedRecursive(true);
            if (GUILayout.Button("Deselect All"))
                _unusedTree.SetSelectedRecursive(false);
            if (GUILayout.Button("Expand All"))
                SetExpandedRecursive(_unusedTree, true);
            if (GUILayout.Button("Collapse All"))
                SetExpandedRecursive(_unusedTree, false);
            EditorGUILayout.EndHorizontal();

            // Action buttons row 2
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Selected to CSV"))
                ExportUnusedCSV(selectedPaths);

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            EditorGUI.BeginDisabledGroup(selectedPaths.Count == 0);
            if (GUILayout.Button($"Move Selected to Trash ({selectedPaths.Count})"))
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    $"Move {selectedPaths.Count} textures ({FormatBytes(selectedSize)}) to OS trash?\n" +
                    "You can undo this from your trash / recycle bin.",
                    "Yes, move to trash", "Cancel"))
                {
                    DeleteTextures(selectedPaths);
                    FindUnusedTextures();
                    _unusedTree = BuildFolderTree(_unusedTextures);
                }
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Tree View ──
            EditorGUILayout.Space(4);
            _scrollUnused = EditorGUILayout.BeginScrollView(_scrollUnused);
            foreach (var child in _unusedTree.Children)
                DrawFolderNode(child, 0);
            foreach (var tex in _unusedTree.Textures)
                DrawTextureLeaf(tex, 0);
            EditorGUILayout.EndScrollView();
        }

        // ─── Draw folder node recursively ──────────────────────────────
        private void DrawFolderNode(FolderNode node, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 18);

            // Checkbox – toggling a folder toggles all children
            bool newSel = EditorGUILayout.Toggle(node.Selected, GUILayout.Width(18));
            if (newSel != node.Selected)
                node.SetSelectedRecursive(newSel);

            // Foldout arrow
            node.Expanded = EditorGUILayout.Foldout(node.Expanded, "", true);

            // Folder label with stats
            int count = node.TotalTextureCount;
            string sizeStr = FormatBytes(node.TotalFileSize);
            GUILayout.Label($"\u25a0 {node.Name}/", _folderStyle, GUILayout.ExpandWidth(false));
            GUILayout.Label($"  ({count} textures, {sizeStr})", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!node.Expanded) return;

            foreach (var child in node.Children)
                DrawFolderNode(child, depth + 1);

            foreach (var tex in node.Textures)
                DrawTextureLeaf(tex, depth + 1);
        }

        private void DrawTextureLeaf(string path, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 18 + 22);

            var fi = new FileInfo(path);
            string fileName = Path.GetFileName(path);
            string size = fi.Exists ? FormatBytes(fi.Length) : "?";

            if (GUILayout.Button($"  {fileName}  ({size})", _fileStyle))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ================================================================
        //  Core Logic – Scan
        // ================================================================
        private void ScanTextures()
        {
            _textures.Clear();
            _issueCount = 0;
            _estimatedSavedBytes = 0;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _searchFolder });
            _totalCount = guids.Length;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/Editor/") || !path.StartsWith("Assets"))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                EditorUtility.DisplayProgressBar("Scanning Textures",
                    path, (float)i / guids.Length);

                // Read actual pixel dimensions from the source file
                GetOriginalTextureSize(importer, out int srcW, out int srcH);
                int maxDim = Mathf.Max(srcW, srcH);

                // Determine recommended size:
                // If auto-fit is ON and the real pixels are smaller than max,
                // snap down to the smallest POT that still contains the texture.
                int recommended = _maxTextureSize;
                if (_autoFitSize && maxDim > 0 && maxDim < _maxTextureSize)
                    recommended = GetSmallestPOTFitting(maxDim);

                bool needsResize = importer.maxTextureSize > recommended;
                bool needsMipMap = _enableMipMaps
                    && !importer.mipmapEnabled
                    && importer.textureType != TextureImporterType.Sprite;
                bool needsCompression = importer.textureCompression != _compression;
                bool needsCrunch = _enableCrunch
                    && (!importer.crunchedCompression || importer.compressionQuality != _crunchQuality);

                var info = new TextureInfo
                {
                    Path                  = path,
                    Importer              = importer,
                    OriginalMaxSize       = importer.maxTextureSize,
                    RecommendedSize       = recommended,
                    OriginalMipMap        = importer.mipmapEnabled,
                    OriginalCrunch        = importer.crunchedCompression,
                    OriginalCrunchQuality = importer.compressionQuality,
                    OriginalCompression   = importer.textureCompression,
                    ActualWidth           = srcW,
                    ActualHeight          = srcH,
                    NeedsResize           = needsResize,
                    NeedsMipMap           = needsMipMap,
                    NeedsCompression      = needsCompression,
                    NeedsCrunch           = needsCrunch,
                };

                if (info.NeedsResize)
                {
                    float ratio = (float)recommended / info.OriginalMaxSize;
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Exists)
                        _estimatedSavedBytes += (long)(fileInfo.Length * (1f - ratio * ratio));
                }

                if (info.HasIssue)
                    _issueCount++;

                _textures.Add(info);
            }

            EditorUtility.ClearProgressBar();
            _scanned = true;
            Debug.Log($"[TextureOptimizer] Scanned {_totalCount} textures \u2013 {_issueCount} issues found.");
        }

        // ================================================================
        //  Core Logic – Apply
        // ================================================================
        private void ApplyOptimizations()
        {
            var selected = _textures.Where(t => t.HasIssue && t.IsSelected).ToList();
            if (selected.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var info = selected[i];
                    EditorUtility.DisplayProgressBar("Optimizing",
                        info.Path, (float)i / selected.Count);

                    if (info.NeedsResize)
                        info.Importer.maxTextureSize = info.RecommendedSize;

                    if (info.NeedsMipMap)
                        info.Importer.mipmapEnabled = true;

                    if (info.NeedsCompression)
                        info.Importer.textureCompression = _compression;

                    if (info.NeedsCrunch)
                    {
                        info.Importer.crunchedCompression = true;
                        info.Importer.compressionQuality  = _crunchQuality;
                    }

                    info.Importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[TextureOptimizer] Optimized {selected.Count} textures.");
            ScanTextures();
        }

        // ================================================================
        //  Core Logic – Find Unused
        // ================================================================
        private void FindUnusedTextures()
        {
            _unusedTextures.Clear();

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { _searchFolder });
            var allTexturePaths = new HashSet<string>();
            foreach (var guid in texGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets") && !p.Contains("/Editor/"))
                    allTexturePaths.Add(p);
            }

            string[] allAssetGuids = AssetDatabase.FindAssets("", new[] { _searchFolder });
            var referencedTextures = new HashSet<string>();

            int total = allAssetGuids.Length;
            for (int i = 0; i < total; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(allAssetGuids[i]);
                if (allTexturePaths.Contains(assetPath)) continue;
                if (AssetDatabase.IsValidFolder(assetPath)) continue;

                if (i % 200 == 0)
                    EditorUtility.DisplayProgressBar("Finding Unused Textures",
                        $"Checking dependencies\u2026 {i}/{total}", (float)i / total);

                string[] deps = AssetDatabase.GetDependencies(assetPath, false);
                foreach (var dep in deps)
                {
                    if (allTexturePaths.Contains(dep))
                        referencedTextures.Add(dep);
                }
            }

            EditorUtility.ClearProgressBar();

            // Scenes in build settings (recursive deps)
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                string[] sceneDeps = AssetDatabase.GetDependencies(scene.path, true);
                foreach (var dep in sceneDeps)
                {
                    if (allTexturePaths.Contains(dep))
                        referencedTextures.Add(dep);
                }
            }

            // Auto-whitelist special folders (may be loaded at runtime)
            foreach (var rt in allTexturePaths.Where(p =>
                p.Contains("/Resources/") ||
                p.Contains("/StreamingAssets/") ||
                p.Contains("/Addressables/")))
            {
                referencedTextures.Add(rt);
            }

            _unusedTextures = allTexturePaths
                .Where(p => !referencedTextures.Contains(p))
                .OrderBy(p => p)
                .ToList();

            Debug.Log($"[TextureOptimizer] Found {_unusedTextures.Count} potentially unused textures out of {allTexturePaths.Count}.");
        }

        // ================================================================
        //  Folder Tree Builder
        // ================================================================
        private FolderNode BuildFolderTree(List<string> paths)
        {
            var root = new FolderNode { Name = "Assets", FullPath = "Assets", Expanded = true };

            foreach (var path in paths)
            {
                string[] parts = path.Split('/');
                var current = root;

                for (int i = 1; i < parts.Length - 1; i++)
                {
                    string folderName = parts[i];
                    var child = current.Children.FirstOrDefault(c => c.Name == folderName);
                    if (child == null)
                    {
                        child = new FolderNode
                        {
                            Name     = folderName,
                            FullPath = string.Join("/", parts, 0, i + 1),
                            Expanded = false,
                        };
                        current.Children.Add(child);
                    }
                    current = child;
                }

                current.Textures.Add(path);
            }

            SortTree(root);
            return root;
        }

        private void SortTree(FolderNode node)
        {
            node.Children.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
            node.Textures.Sort();
            foreach (var c in node.Children) SortTree(c);
        }

        private void SetExpandedRecursive(FolderNode node, bool expanded)
        {
            node.Expanded = expanded;
            foreach (var c in node.Children)
                SetExpandedRecursive(c, expanded);
        }

        // ================================================================
        //  Delete & Export
        // ================================================================
        private void DeleteTextures(List<string> paths)
        {
            foreach (var p in paths)
                AssetDatabase.MoveAssetToTrash(p);
            AssetDatabase.Refresh();
            Debug.Log($"[TextureOptimizer] Moved {paths.Count} textures to trash.");
        }

        private void ExportUnusedCSV(List<string> paths)
        {
            string savePath = EditorUtility.SaveFilePanel("Save Unused Textures List",
                "", "unused_textures", "csv");
            if (string.IsNullOrEmpty(savePath)) return;

            var lines = new List<string> { "Path,FileSize(KB)" };
            foreach (var p in paths)
            {
                var fi = new FileInfo(p);
                long kb = fi.Exists ? fi.Length / 1024 : 0;
                lines.Add($"\"{p}\",{kb}");
            }
            File.WriteAllLines(savePath, lines);
            Debug.Log($"[TextureOptimizer] Exported {paths.Count} entries to {savePath}");
        }

        // ================================================================
        //  Helpers
        // ================================================================

        /// <summary>
        /// Returns the smallest POT size >= maxDimension, clamped to _maxTextureSize.
        /// e.g. 300 -> 512, 600 -> 1024, 2000 -> 2048
        /// </summary>
        private int GetSmallestPOTFitting(int maxDimension)
        {
            foreach (int pot in POT_SIZES)
            {
                if (pot >= maxDimension)
                    return Mathf.Min(pot, _maxTextureSize);
            }
            return _maxTextureSize;
        }

        /// <summary>
        /// Read the original source texture dimensions via TextureImporter internal API.
        /// Falls back to loading the imported Texture2D if reflection fails.
        /// </summary>
        private static void GetOriginalTextureSize(TextureImporter importer, out int width, out int height)
        {
            width = 0;
            height = 0;

            // Try the internal method (avoids loading the full texture asset)
            var method = typeof(TextureImporter).GetMethod(
                "GetWidthAndHeight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                object[] args = new object[] { 0, 0 };
                method.Invoke(importer, args);
                width  = (int)args[0];
                height = (int)args[1];
                if (width > 0 && height > 0) return;
            }

            // Fallback: load texture (returns imported size, not source, but usable)
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            if (tex != null)
            {
                width  = tex.width;
                height = tex.height;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)           return $"{bytes} B";
            if (bytes < 1024 * 1024)    return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        // ─── Styles ────────────────────────────────────────────────────
        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 16,
                alignment = TextAnchor.MiddleCenter
            };

            _statsStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                richText  = true
            };

            _tagBad = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.9f, 0.3f, 0.2f, 0.85f)) },
                padding   = new RectOffset(6, 6, 2, 2),
                margin    = new RectOffset(2, 2, 2, 2),
                fontStyle = FontStyle.Bold,
                fontSize  = 10
            };

            _tagInfo = new GUIStyle(_tagBad)
            {
                normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.4f, 0.55f, 0.75f, 0.85f)) }
            };

            _folderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            _fileStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                richText  = true
            };

            _stylesReady = true;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = Enumerable.Repeat(col, w * h).ToArray();
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}