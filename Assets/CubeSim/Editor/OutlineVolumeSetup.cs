using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Creates the Volume Profile for CubeSim scenes: the project's screen-space outline plus the
    /// camera finishing (bloom, tonemapping, colour, vignette).
    ///
    /// The outline lives in BillDev.SSOutline inside the predefined Assembly-CSharp, which an asmdef
    /// cannot reference. Rather than dragging a bridge assembly in, this resolves the volume
    /// component by name and configures it through SerializedObject - and degrades to a warning if
    /// the outline package is ever removed, instead of breaking the build.
    /// </summary>
    public static class OutlineVolumeSetup
    {
        public const string ProfilePath = "Assets/CubeSim/Data/CubeSimOutlineProfile.asset";
        private const string OutlineTypeName = "BillDev.SSOutline.OutlineVolume, Assembly-CSharp";

        [MenuItem("CubeSim/Create Volume Profile", priority = 60)]
        public static VolumeProfile CreateProfileMenu() => CreateProfile(new PostProcessingConfig());

        public static VolumeProfile CreateProfile() => CreateProfile(new PostProcessingConfig());

        public static VolumeProfile CreateProfile(PostProcessingConfig post)
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            bool isNew = profile == null;
            if (isNew)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Type outlineType = Type.GetType(OutlineTypeName);
            if (outlineType == null)
            {
                Debug.LogWarning("[CubeSim] BillDev.SSOutline.OutlineVolume not found; the scene will " +
                                 "render without outlines. Everything else is unaffected.");
                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            }

            VolumeComponent component = null;
            foreach (VolumeComponent c in profile.components)
            {
                if (outlineType.IsInstanceOfType(c)) { component = c; break; }
            }

            if (component == null)
            {
                component = profile.Add(outlineType, true);

                // A volume override is a sub-asset. Without this it lives only in memory and the
                // saved profile comes back empty.
                component.name = outlineType.Name;
                component.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            Configure(component);

            ConfigurePostProcessing(profile, post);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        /// <summary>
        /// Adds the URP finishing stack. Every emissive in the scene - pressure, goal, colour gates,
        /// trails, weapon pads - is flat without bloom, which is the single biggest gap against the
        /// reference footage.
        /// </summary>
        private static void ConfigurePostProcessing(VolumeProfile profile, PostProcessingConfig post)
        {
            if (post == null) post = new PostProcessingConfig();

            if (!post.enabled)
            {
                Remove<Bloom>(profile);
                Remove<Tonemapping>(profile);
                Remove<ColorAdjustments>(profile);
                Remove<Vignette>(profile);
                return;
            }

            if (post.bloom)
            {
                Bloom bloom = GetOrAdd<Bloom>(profile);
                Set(bloom.threshold, post.bloomThreshold);
                Set(bloom.intensity, post.bloomIntensity);
                Set(bloom.scatter, post.bloomScatter);
                Set(bloom.highQualityFiltering, true);
            }
            else Remove<Bloom>(profile);

            if (post.tonemapping)
            {
                Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
                // Neutral, not ACES. ACES rolls bright saturated colour toward white, which turned
                // the green goal into a white slab and the red hazard into orange. In the reference
                // the goal samples as a flat (0,253,0) - the accents stay pure at any brightness.
                Set(tonemapping.mode, TonemappingMode.Neutral);
            }
            else Remove<Tonemapping>(profile);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            Set(color.postExposure, post.postExposure);
            Set(color.contrast, post.contrast);
            Set(color.saturation, post.saturation);
            Set(color.colorFilter, post.colorFilter);

            if (post.vignette)
            {
                Vignette vignette = GetOrAdd<Vignette>(profile);
                Set(vignette.intensity, post.vignetteIntensity);
                Set(vignette.smoothness, post.vignetteSmoothness);
                Set(vignette.color, post.vignetteColor);
            }
            else Remove<Vignette>(profile);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;

            T component = profile.Add<T>(true);

            // Volume overrides are sub-assets; without this the saved profile comes back empty.
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void Remove<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T existing)) return;

            profile.Remove<T>();
            AssetDatabase.RemoveObjectFromAsset(existing);
            UnityEngine.Object.DestroyImmediate(existing, true);
        }

        private static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        /// <summary>
        /// Full-screen mode driven by the depth buffer. Everything opaque gets an outline - walls,
        /// skeletons, weapons and the racer trails - through the one existing pipeline.
        ///
        /// Normal-based edges are deliberately off: the skeleton model has enough internal normal
        /// discontinuities that it outlines every rib and reads as a black blob from a top-down
        /// camera. Depth alone gives clean silhouettes.
        /// </summary>
        private static void Configure(VolumeComponent component)
        {
            var so = new SerializedObject(component);

            SetOverride(so, "isActive", true);
            SetOverride(so, "useDepth", true);
            SetOverride(so, "useNormals", false);

            SetEnum(so, "mode", 0);                         // FullScreen
            SetInt(so, "thickness", 1);
            SetFloat(so, "outlineIntensity", 1f);
            SetFloat(so, "depthThreshold", 0.55f);
            SetFloat(so, "normalThreshold", 0.3f);
            SetFloat(so, "fadeDistanceStart", 120f);
            SetFloat(so, "fadeDistanceEnd", 260f);
            SetColor(so, "outlineColor", new Color(0.03f, 0.03f, 0.05f, 1f));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Parameter(SerializedObject so, string name, string valueField)
        {
            SerializedProperty parameter = so.FindProperty(name);
            if (parameter == null) return null;

            SerializedProperty overrideState = parameter.FindPropertyRelative("m_OverrideState");
            if (overrideState != null) overrideState.boolValue = true;

            return parameter.FindPropertyRelative(valueField);
        }

        private static void SetOverride(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = Parameter(so, name, "m_Value");
            if (p != null) p.boolValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = Parameter(so, name, "m_Value");
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = Parameter(so, name, "m_Value");
            if (p != null) p.intValue = value;
        }

        private static void SetEnum(SerializedObject so, string name, int value)
        {
            SerializedProperty p = Parameter(so, name, "m_Value");
            if (p != null) p.enumValueIndex = value;
        }

        private static void SetColor(SerializedObject so, string name, Color value)
        {
            SerializedProperty p = Parameter(so, name, "m_Value");
            if (p != null) p.colorValue = value;
        }

        /// <summary>Adds the global volume that applies the profile to a scene.</summary>
        public static GameObject CreateSceneVolume(VolumeProfile profile)
        {
            var go = new GameObject("OutlineVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return go;
        }
    }
}
