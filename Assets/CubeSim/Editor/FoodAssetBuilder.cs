using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the food visual library from the Kenney food pack: a dozen recognisable fruits
    /// and snacks, one ToonLit material carrying the pack's colormap, and per-model scale and
    /// rest height measured off the imported bounds. The pack is never modified.
    /// </summary>
    public static class FoodAssetBuilder
    {
        public const string LibraryPath = "Assets/CubeSim/Data/FoodVisualLibrary.asset";
        private const string MaterialPath = "Assets/CubeSim/Visuals/Food/CubeSimFood.mat";
        private const string Pack = "Assets/KenneyDungeon/FBX format 1/";
        private const string Colormap = Pack + "Textures/colormap.png";

        /// <summary>Longest side after scaling. Pellet grid spacing is 2.4m; this leaves air between them.</summary>
        private const float TargetSize = 1.9f;

        // Bright, distinct silhouettes that read from a top-down camera.
        private static readonly string[] Models =
        {
            "apple", "banana", "orange", "pear", "strawberry", "watermelon",
            "grapes", "pineapple", "carrot", "cheese", "burger-cheese", "cupcake",
        };

        [MenuItem("CubeSim/Build Food Assets", priority = 15)]
        public static FoodVisualLibrary BuildLibrary()
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");
            Directory.CreateDirectory("Assets/CubeSim/Visuals/Food");

            Material material = GetMaterial();
            var entries = new List<FoodVisualLibrary.Entry>();

            foreach (string id in Models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + id + ".fbx");
                if (prefab == null)
                {
                    Debug.LogWarning($"[CubeSim] Food model missing: {id}");
                    continue;
                }

                Measure(prefab, out float longest, out float bottom);
                float scale = TargetSize / Mathf.Max(0.01f, longest);

                entries.Add(new FoodVisualLibrary.Entry
                {
                    id = id,
                    prefab = prefab,
                    scale = scale,
                    restHeight = -bottom * scale,
                });
            }

            var library = AssetDatabase.LoadAssetAtPath<FoodVisualLibrary>(LibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<FoodVisualLibrary>();

            library.Configure(material, entries);

            if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CubeSim] Food library built: {entries.Count} models at {TargetSize}m.");
            return AssetDatabase.LoadAssetAtPath<FoodVisualLibrary>(LibraryPath);
        }

        private static Material GetMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(Colormap);

            material = new Material(shader) { name = "CubeSimFood" };
            material.SetColor("_BaseColor", Color.white);
            if (colormap != null) material.SetTexture("_BaseMap", colormap);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.55f, 0.5f, 0.5f));

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>Native longest side and bottom height, measured at unit scale.</summary>
        private static void Measure(GameObject prefab, out float longest, out float bottom)
        {
            longest = 1f;
            bottom = 0f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

                longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                bottom = bounds.min.y;
            }

            Object.DestroyImmediate(instance);
        }
    }
}
