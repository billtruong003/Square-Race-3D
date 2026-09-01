using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Protects hand-authored scene presentation from the generated-arena rebuild.
    ///
    /// The arena builder works by destroying the whole <c>ChallengeArena</c> root and regenerating
    /// it. That is fine for geometry the recipe owns, but the scene also carries values a person
    /// tuned by eye and that no asset records: the skybox, whether fog is on, the sun's colour, the
    /// cloud volume's position and its dozen shader-facing numbers. Before this existed, one rebuild
    /// silently reset all of them.
    ///
    /// The rule is simple: <b>if it already exists in the scene, the scene wins.</b> The builder only
    /// authors presentation the first time, when there is nothing to preserve. Resetting to builder
    /// defaults is still possible, but it is now an explicit, separately named command rather than a
    /// side effect of rebuilding the lane.
    /// </summary>
    public class PresentationLock
    {
        /// <summary>Names of the generated children that hold hand-tuned presentation.</summary>
        private const string LightingName = "Lighting";
        private const string CloudName = "CloudSea";

        private Transform lighting;
        private Transform cloud;

        // RenderSettings has no scene-level serialization we can round-trip, so it is captured field
        // by field. Anything the builder writes has to be listed here or it will still be lost.
        private Material skybox;
        private UnityEngine.Rendering.AmbientMode ambientMode;
        private Color ambientSky, ambientEquator, ambientGround, fogColor;
        private float ambientIntensity, fogStart, fogEnd, fogDensity, reflectionIntensity;
        private bool fog;
        private FogMode fogMode;

        public bool HasLighting => lighting != null;
        public bool HasCloud => cloud != null;

        /// <summary>
        /// Detach the presentation objects from the doomed root and remember the render settings.
        ///
        /// Detaching rather than copying is deliberate: the live objects keep their exact component
        /// values, including any field a person changed that no builder knows about.
        /// </summary>
        public static PresentationLock Capture(GameObject generatedRoot)
        {
            var l = new PresentationLock
            {
                skybox = RenderSettings.skybox,
                ambientMode = RenderSettings.ambientMode,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                fog = RenderSettings.fog,
                fogMode = RenderSettings.fogMode,
                fogColor = RenderSettings.fogColor,
                fogStart = RenderSettings.fogStartDistance,
                fogEnd = RenderSettings.fogEndDistance,
                fogDensity = RenderSettings.fogDensity,
            };

            if (generatedRoot == null) return l;

            l.lighting = generatedRoot.transform.Find(LightingName);
            l.cloud = generatedRoot.transform.Find(CloudName);

            // Park them at the scene root so DestroyImmediate on the arena cannot take them with it.
            if (l.lighting != null) l.lighting.SetParent(null, true);
            if (l.cloud != null) l.cloud.SetParent(null, true);
            return l;
        }

        /// <summary>Re-parent the preserved objects under the freshly built root and restore settings.</summary>
        public void Restore(Transform newRoot)
        {
            if (lighting != null) lighting.SetParent(newRoot, true);
            if (cloud != null) cloud.SetParent(newRoot, true);

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogDensity = fogDensity;

            // The sun reference points at a Light that may have just been re-parented; re-resolve it
            // rather than leaving RenderSettings.sun dangling.
            if (lighting != null)
            {
                var sun = lighting.GetComponentInChildren<Light>();
                if (sun != null && sun.type == LightType.Directional) RenderSettings.sun = sun;
            }
        }
    }
}
