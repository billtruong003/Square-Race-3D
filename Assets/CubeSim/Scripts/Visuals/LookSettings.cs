using UnityEngine;
using UnityEngine.Rendering;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The project-wide colour "look": one optional Volume Profile laid over every scene's own
    /// profile through a second, higher-priority global Volume.
    ///
    /// Looks only carry Bloom, Tonemapping, Colour Adjustments and Vignette. Volume blending lets
    /// every component a look does not override - the outline above all - fall through untouched
    /// from the scene's CubeSimOutlineProfile, which is never edited. No look = the original grade.
    ///
    /// Lives in Resources so all ~260 scenes pick the same look without being touched.
    /// </summary>
    [CreateAssetMenu(menuName = "CubeSim/Look Settings", fileName = "CubeSimLook")]
    public sealed class LookSettings : ScriptableObject
    {
        public const string ResourceName = "CubeSimLook";
        private const string VolumeName = "CubeSim_Look";

        [Tooltip("Laid over the scene's own profile. Empty = the original look.")]
        public VolumeProfile look;

        [Range(0f, 1f)] public float weight = 1f;

        public static LookSettings Load() => Resources.Load<LookSettings>(ResourceName);

        /// <summary>Creates, updates or removes the look volume in the active scene.</summary>
        public static void ApplyToScene()
        {
            LookSettings settings = Load();
            GameObject existing = GameObject.Find(VolumeName);

            if (settings == null || settings.look == null)
            {
                if (existing != null) existing.SetActive(false);
                return;
            }

            if (existing == null)
            {
                existing = new GameObject(VolumeName);
                existing.AddComponent<Volume>();
            }

            existing.SetActive(true);
            var volume = existing.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = settings.weight;
            volume.sharedProfile = settings.look;
        }
    }
}
