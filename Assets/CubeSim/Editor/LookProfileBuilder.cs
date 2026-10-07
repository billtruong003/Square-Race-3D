using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the alternative looks and switches between them project-wide.
    ///
    /// Each look is its own profile under Data/Looks holding only Bloom, Tonemapping, Colour
    /// Adjustments and Vignette. It is laid over the scene's CubeSimOutlineProfile by a second
    /// volume, so the outline and anything else a look does not set stay exactly as tuned. This
    /// class never opens or writes CubeSimOutlineProfile.
    /// </summary>
    public static class LookProfileBuilder
    {
        private const string Folder = "Assets/CubeSim/Data/Looks";
        private const string SettingsPath = "Assets/CubeSim/Resources/" + LookSettings.ResourceName + ".asset";

        private struct Look
        {
            public string Name;
            public TonemappingMode Tonemap;
            public float BloomThreshold, BloomIntensity, BloomScatter;
            public Color BloomTint;
            public float Exposure, Contrast, Saturation;
            public Color Filter;
            public float VignetteIntensity, VignetteSmoothness;
            public Color VignetteColor;
        }

        private static readonly Look[] Looks =
        {
            new Look { Name = "CleanPastel", Tonemap = TonemappingMode.Neutral,
                BloomThreshold = 1.1f, BloomIntensity = 0.8f, BloomScatter = 0.55f, BloomTint = Color.white,
                Exposure = 0f, Contrast = 6f, Saturation = 8f, Filter = Color.white,
                VignetteIntensity = 0f, VignetteSmoothness = 0.5f, VignetteColor = Color.black },
            new Look { Name = "PunchyArcade", Tonemap = TonemappingMode.ACES,
                BloomThreshold = 0.9f, BloomIntensity = 1.6f, BloomScatter = 0.65f, BloomTint = Color.white,
                Exposure = 0.15f, Contrast = 18f, Saturation = 28f, Filter = Color.white,
                VignetteIntensity = 0.22f, VignetteSmoothness = 0.5f, VignetteColor = Color.black },
            new Look { Name = "SoftToy", Tonemap = TonemappingMode.Neutral,
                BloomThreshold = 1.2f, BloomIntensity = 0.5f, BloomScatter = 0.8f, BloomTint = new Color(1f, 0.97f, 0.92f),
                Exposure = 0.25f, Contrast = -8f, Saturation = -6f, Filter = new Color(1f, 0.98f, 0.95f),
                VignetteIntensity = 0.18f, VignetteSmoothness = 0.9f, VignetteColor = new Color(0.25f, 0.18f, 0.15f) },
            new Look { Name = "NightNeon", Tonemap = TonemappingMode.ACES,
                BloomThreshold = 0.8f, BloomIntensity = 2.5f, BloomScatter = 0.75f, BloomTint = new Color(0.85f, 0.9f, 1f),
                Exposure = -0.2f, Contrast = 20f, Saturation = 35f, Filter = new Color(0.92f, 0.95f, 1f),
                VignetteIntensity = 0.35f, VignetteSmoothness = 0.6f, VignetteColor = new Color(0f, 0f, 0.03f) },
        };

        [MenuItem("CubeSim/Look/Build Look Profiles", priority = 200)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            foreach (Look look in Looks) BuildLook(look);
            EnsureSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[CubeSim] Look profiles ready in " + Folder + ". Pick one under CubeSim/Look.");
        }

        [MenuItem("CubeSim/Look/Original (scene profile only)", priority = 210)]
        public static void UseOriginal() => Select(null);

        [MenuItem("CubeSim/Look/Clean Pastel", priority = 211)]
        public static void UseCleanPastel() => Select("CleanPastel");

        [MenuItem("CubeSim/Look/Punchy Arcade", priority = 212)]
        public static void UsePunchyArcade() => Select("PunchyArcade");

        [MenuItem("CubeSim/Look/Soft Toy", priority = 213)]
        public static void UseSoftToy() => Select("SoftToy");

        [MenuItem("CubeSim/Look/Night Neon", priority = 214)]
        public static void UseNightNeon() => Select("NightNeon");

        public static void Select(string lookName)
        {
            LookSettings settings = EnsureSettings();
            VolumeProfile profile = null;
            if (!string.IsNullOrEmpty(lookName))
            {
                string path = $"{Folder}/Look_{lookName}.asset";
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    BuildAll();
                    profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                }
            }

            settings.look = profile;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (Application.isPlaying) LookSettings.ApplyToScene();
            Debug.Log("[CubeSim] Look: " + (profile != null ? profile.name : "original"));
        }

        private static LookSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LookSettings>(SettingsPath);
            if (settings != null) return settings;

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<LookSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void BuildLook(Look look)
        {
            string path = $"{Folder}/Look_{look.Name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return; // never overwrite a look someone has since tuned by hand

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(look.BloomThreshold);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.scatter.Override(look.BloomScatter);
            bloom.tint.Override(look.BloomTint);
            bloom.highQualityFiltering.Override(true);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(look.Tonemap);

            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(look.Exposure);
            grade.contrast.Override(look.Contrast);
            grade.saturation.Override(look.Saturation);
            grade.colorFilter.Override(look.Filter);

            var vignette = profile.Add<Vignette>(true);
            vignette.active = look.VignetteIntensity > 0f;
            vignette.intensity.Override(look.VignetteIntensity);
            vignette.smoothness.Override(look.VignetteSmoothness);
            vignette.color.Override(look.VignetteColor);

            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
        }
    }
}
