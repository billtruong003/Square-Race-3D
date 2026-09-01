using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Turns an authoring macro - a hierarchy of individual Kenney pieces - into a baked macro made
    /// of one combined mesh per material.
    ///
    /// The arena used to place every tile, wall and pebble directly into the production scene, which
    /// cost about 610 environment renderers and roughly 1,200 GameObjects for a single map, and made
    /// the hierarchy unreadable. The fix is a two-form workflow:
    ///
    ///   AUTHORING  Assets/ChallengeShow/Environment/Authoring/&lt;Name&gt;_AUTHOR.prefab
    ///              Individual pieces. Editable, re-runnable, never shipped in a scene.
    ///   BAKED      Assets/ChallengeShow/Environment/Prefabs/&lt;Category&gt;/&lt;Name&gt;.prefab
    ///              One renderer per material, plus sockets. This is what the arena instantiates.
    ///
    /// Deliberately NOT a whole-arena combiner. Macros stay chunked at the size of a real composition
    /// unit - a court, a 12 m span, the arm zone - so culling, reuse and obstacle swapping all keep
    /// working. Merging the map into one mesh would trade every one of those for a draw call.
    /// </summary>
    public static class KenneyMacroBaker
    {
        public const string EnvironmentRoot = "Assets/ChallengeShow/Environment";
        public const string AuthoringRoot = EnvironmentRoot + "/Authoring";
        public const string MeshRoot = EnvironmentRoot + "/Baked/Meshes";
        public const string PrefabRoot = EnvironmentRoot + "/Prefabs";

        /// <summary>Children whose names start with this are sockets and survive the bake.</summary>
        public const string SocketPrefix = "Socket_";

        public const string SocketEntrance = SocketPrefix + "Entrance";
        public const string SocketExit = SocketPrefix + "Exit";
        public const string SocketObstacle = SocketPrefix + "Obstacle";
        public const string SocketCameraHint = SocketPrefix + "CameraHint";

        /// <summary>
        /// Bake one authoring hierarchy into a baked prefab.
        ///
        /// <paramref name="authoringRoot"/> is consumed as data only - it is never modified, so the
        /// same authoring instance can be baked repeatedly and the vendor meshes underneath it are
        /// never touched.
        /// </summary>
        /// <returns>The baked prefab asset, or null if there was nothing to bake.</returns>
        public static GameObject Bake(GameObject authoringRoot, string macroName, string category)
        {
            if (authoringRoot == null)
            {
                Debug.LogError($"[MacroBaker] {macroName}: null authoring root.");
                return null;
            }

            EnsureFolder(MeshRoot);
            EnsureFolder($"{PrefabRoot}/{category}");

            var groups = GroupByMaterial(authoringRoot.transform);
            if (groups.Count == 0)
            {
                Debug.LogWarning($"[MacroBaker] {macroName}: no renderers found.");
                return null;
            }

            var baked = new GameObject(macroName);
            int combinedRenderers = 0, sourceRenderers = 0;

            foreach (var pair in groups)
            {
                sourceRenderers += pair.Value.Count;
                var mesh = CombineGroup(pair.Value, macroName, pair.Key);
                if (mesh == null) continue;

                var child = new GameObject($"Combined_{SafeName(pair.Key.name)}");
                child.transform.SetParent(baked.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;

                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterial = pair.Key;
                // Baked geometry never moves, so it is a legitimate static-batching candidate. This
                // is applied AFTER combining, not as a substitute for it.
                GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic |
                                                              StaticEditorFlags.OccluderStatic |
                                                              StaticEditorFlags.OccludeeStatic);
                combinedRenderers++;
            }

            CopySockets(authoringRoot.transform, baked.transform);

            // Visual macros carry no physics. Gameplay collision is authored separately and stays
            // the only source of colliders in the scene.
            foreach (var c in baked.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in baked.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);

            string prefabPath = $"{PrefabRoot}/{category}/{macroName}.prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(baked, prefabPath);
            Object.DestroyImmediate(baked);

            Debug.Log($"[MacroBaker] {macroName}: {sourceRenderers} source renderers -> " +
                      $"{combinedRenderers} combined ({groups.Count} material group(s)).");
            return asset;
        }

        /// <summary>
        /// Bucket every MeshFilter under the root by the material its renderer uses.
        ///
        /// Grouping is by material REFERENCE, not by name, so two objects only merge when they would
        /// genuinely have batched anyway. The Kenney kit shares one atlas across all 30 pieces, so in
        /// practice a macro collapses to one or two groups.
        /// </summary>
        private static Dictionary<Material, List<MeshFilter>> GroupByMaterial(Transform root)
        {
            var groups = new Dictionary<Material, List<MeshFilter>>();

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                // Multi-material renderers would need per-submesh splitting. The kit has none, so
                // rather than silently baking them wrong, skip and say so.
                if (mr.sharedMaterials.Length != 1 || mr.sharedMaterials[0] == null)
                {
                    Debug.LogWarning($"[MacroBaker] Skipping {mf.name}: expected exactly one material, " +
                                     $"found {mr.sharedMaterials.Length}.");
                    continue;
                }

                var mat = mr.sharedMaterials[0];
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<MeshFilter>();
                list.Add(mf);
            }
            return groups;
        }

        /// <summary>
        /// Combine one material's meshes into a single mesh asset at a deterministic path.
        ///
        /// Rebaking overwrites the existing asset in place rather than creating a new one, so prefabs
        /// and scenes keep their references and the folder never fills up with _1 / _2 / _final
        /// duplicates.
        /// </summary>
        private static Mesh CombineGroup(List<MeshFilter> filters, string macroName, Material material)
        {
            var combines = new List<CombineInstance>(filters.Count);
            Matrix4x4 rootToLocal = filters[0].transform.root.worldToLocalMatrix;

            foreach (var mf in filters)
                combines.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    // Bake each piece into the macro's own local space, which is what preserves the
                    // authored transforms - including the kit's base-centre pivots and any non-uniform
                    // scaling used on rubble.
                    transform = rootToLocal * mf.transform.localToWorldMatrix,
                    subMeshIndex = 0,
                });

            var combined = new Mesh { name = $"{macroName}_{SafeName(material.name)}" };
            // A macro can pass 65k verts once rubble is included; without this the combine silently
            // truncates.
            combined.indexFormat = IndexFormat.UInt32;
            combined.CombineMeshes(combines.ToArray(), true, true, false);
            combined.RecalculateBounds();

            string path = $"{MeshRoot}/{combined.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite contents so the GUID, and therefore every reference to it, survives.
                existing.Clear();
                EditorUtility.CopySerialized(combined, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(combined);
                return existing;
            }

            AssetDatabase.CreateAsset(combined, path);
            return combined;
        }

        /// <summary>
        /// Carry named sockets across to the baked form.
        ///
        /// Sockets are plain empty transforms, so they cost nothing at runtime and need no runtime
        /// component. They are how the arena recipe snaps lane sections together and finds where the
        /// obstacle belongs, which is what keeps assembly out of hard-coded coordinates.
        /// </summary>
        private static void CopySockets(Transform authoring, Transform baked)
        {
            foreach (var t in authoring.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(SocketPrefix)) continue;

                var socket = new GameObject(t.name);
                socket.transform.SetParent(baked, false);
                // Sockets are authored relative to the macro root, so world-relative placement keeps
                // them correct no matter how deep they sat in the authoring hierarchy.
                socket.transform.localPosition = authoring.InverseTransformPoint(t.position);
                socket.transform.localRotation = Quaternion.Inverse(authoring.rotation) * t.rotation;
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string SafeName(string s) => s.Replace(" ", "").Replace("(", "").Replace(")", "");
    }
}
