using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Wraps the user-authored eye cube (Assets/Eyes/Cube.prefab - body material for tinting, eye
    /// plane on the top face, proportions hand-tuned and therefore sacred) into a racer model:
    /// a wrapper prefab carrying an <see cref="EyeCubeVisual"/> wired to the five eye textures,
    /// registered in the racer visual library as "EyeCube".
    ///
    /// The authored prefab is nested, never modified - retuning the cube in Assets/Eyes flows
    /// into every racer on the next build.
    /// </summary>
    public static class EyeCubeAssetBuilder
    {
        private const string SourcePrefabPath = "Assets/Eyes/Cube.prefab";
        private const string WrapperPath = "Assets/CubeSim/Prefabs/EyeCubeRacer.prefab";

        private const string CenterTexture = "Assets/Eyes/New Project.png";
        private const string SouthEastTexture = "Assets/Eyes/New Project (1).png";
        private const string SouthWestTexture = "Assets/Eyes/New Project (2).png";
        private const string NorthWestTexture = "Assets/Eyes/New Project (3).png";
        private const string NorthEastTexture = "Assets/Eyes/New Project (4).png";

        [MenuItem("CubeSim/Build Eye Cube Racer", priority = 13)]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                Debug.LogError($"[CubeSim] Eye cube source missing at {SourcePrefabPath}.");
                return;
            }

            Directory.CreateDirectory("Assets/CubeSim/Prefabs");

            var root = new GameObject("EyeCubeRacer");
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(source);
                body.transform.SetParent(root.transform, false);
                // The authored prefab keeps a stray scene position; the proportions and scales
                // inside it are hand-tuned and stay untouched - only the root offset is zeroed.
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;

                Renderer eye = FindEyeRenderer(body);
                if (eye == null)
                {
                    Debug.LogError("[CubeSim] No eye plane renderer found in the cube prefab.");
                    return;
                }

                var eyes = root.AddComponent<EyeCubeVisual>();
                eyes.Configure(eye,
                    AssetDatabase.LoadAssetAtPath<Texture2D>(CenterTexture),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(NorthEastTexture),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(NorthWestTexture),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(SouthEastTexture),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(SouthWestTexture));

                PrefabUtility.SaveAsPrefabAsset(root, WrapperPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RegisterLibraryEntry();
            AssetDatabase.SaveAssets();
            Debug.Log($"[CubeSim] Eye cube racer built at {WrapperPath} and registered as 'EyeCube'.");
        }

        /// <summary>The eye plane is the quad under the cube body - the only non-cube renderer.</summary>
        private static Renderer FindEyeRenderer(GameObject body)
        {
            foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && filter.sharedMesh.name.Contains("Plane"))
                {
                    return filter.GetComponent<Renderer>();
                }
            }

            return null;
        }

        private static void RegisterLibraryEntry()
        {
            var library = AssetDatabase.LoadAssetAtPath<RacerVisualLibrary>(SkeletonAssetBuilder.VisualLibraryPath);
            if (library == null)
            {
                Debug.LogError("[CubeSim] Racer visual library missing; build it first.");
                return;
            }

            var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);

            // Through SerializedObject end to end, so the change is applied on the serialized
            // stream itself and survives the save no matter what else holds the instance.
            var serialized = new SerializedObject(library);
            SerializedProperty list = serialized.FindProperty("entries");

            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                string id = list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                if (string.Equals(id, "EyeCube", System.StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
            }

            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = "EyeCube";
            entry.FindPropertyRelative("prefab").objectReferenceValue = wrapper;
            entry.FindPropertyRelative("animatorController").objectReferenceValue = null;
            entry.FindPropertyRelative("nativeHeight").floatValue = 1f;
            entry.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
            entry.FindPropertyRelative("yOffset").floatValue = 0f;
            entry.FindPropertyRelative("portrait").objectReferenceValue = null;
            entry.FindPropertyRelative("displayName").stringValue = "";
            entry.FindPropertyRelative("handBoneName").stringValue = "";

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }
    }
}
