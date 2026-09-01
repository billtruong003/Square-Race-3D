using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// The cloud sea beneath the arena.
    ///
    /// Owns one box volume rendered by CleanRender/CloudSea, plus the tiling noise the shader reads.
    /// The noise is generated here rather than authored as an asset so the look is reproducible from
    /// a seed and there is no texture to keep in sync with the shader.
    ///
    /// Runs in edit mode as well as play mode: the cloud sea is a composition element, so it has to
    /// be visible while the arena is being framed, not only once the show is running.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class CloudSea : MonoBehaviour
    {
        public enum Quality
        {
            Low,
            Medium,
            High
        }

        [Header("Quality")]
        [Tooltip("Low for fast iteration, Medium for working preview, High for recording.")]
        [SerializeField] private Quality quality = Quality.Medium;

        [Header("Shape")]
        [SerializeField] private Color litColor = new(1f, 0.99f, 0.97f);
        [SerializeField] private Color shadowColor = new(0.52f, 0.63f, 0.80f);
        [Range(0f, 8f)][SerializeField] private float density = 2.4f;
        [Range(0f, 1f)][SerializeField] private float coverage = 0.58f;
        [SerializeField] private float noiseScale = 0.011f;
        [SerializeField] private float secondaryNoiseScale = 0.042f;
        [Range(0f, 1f)][SerializeField] private float distortion = 0.4f;
        [Range(0.01f, 0.5f)][SerializeField] private float edgeFade = 0.16f;
        [Range(0.01f, 0.6f)][SerializeField] private float verticalFade = 0.3f;
        [Range(0f, 1f)][SerializeField] private float lightInfluence = 0.7f;
        [Tooltip("Ceiling on how far one ray marches. The single most effective cost control, "
               + "because grazing rays would otherwise cross the whole volume for no visual gain.")]
        [SerializeField] private float maxMarchDistance = 240f;
        [SerializeField] private float scrollSpeed = 0.4f;

        [Header("Noise")]
        [Tooltip("Baked by Challenge Show/5. Bake Cloud Noise. R and G hold two tiling octaves.")]
        [SerializeField] private Texture2D noiseTexture;

        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseScale2Id = Shader.PropertyToID("_NoiseScale2");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
        private static readonly int VerticalFadeId = Shader.PropertyToID("_VerticalFade");
        private static readonly int StepCountId = Shader.PropertyToID("_StepCount");
        private static readonly int JitterId = Shader.PropertyToID("_Jitter");
        private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");
        private static readonly int MaxMarchId = Shader.PropertyToID("_MaxMarchDistance");

        private Material material;

        /// <summary>Raymarch steps per quality tier. The single biggest cost lever.</summary>
        private int StepsFor(Quality q) => q switch
        {
            Quality.Low => 8,
            Quality.High => 26,
            _ => 15
        };

        private float JitterFor(Quality q) => q == Quality.Low ? 1f : 0.85f;

        public Quality CurrentQuality
        {
            get => quality;
            set { quality = value; Apply(); }
        }

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;

            material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
            if (material == null) return;

            if (noiseTexture != null) material.SetTexture(NoiseTexId, noiseTexture);

            material.SetColor(ColorId, litColor);
            material.SetColor(ShadowColorId, shadowColor);
            material.SetFloat(DensityId, density);
            material.SetFloat(CoverageId, coverage);
            material.SetFloat(NoiseScaleId, noiseScale);
            material.SetFloat(NoiseScale2Id, secondaryNoiseScale);
            material.SetFloat(ScrollSpeedId, scrollSpeed);
            material.SetFloat(DistortionId, distortion);
            material.SetFloat(EdgeFadeId, edgeFade);
            material.SetFloat(VerticalFadeId, verticalFade);
            material.SetFloat(StepCountId, StepsFor(quality));
            material.SetFloat(JitterId, JitterFor(quality));
            material.SetFloat(LightInfluenceId, lightInfluence);
            // Low leans harder on the clamp, since it has the fewest steps to spend.
            material.SetFloat(MaxMarchId, quality == Quality.Low ? maxMarchDistance * 0.6f : maxMarchDistance);

            // The second octave is the other real cost; Low drops it entirely.
            if (quality == Quality.Low) material.DisableKeyword("_SECONDARY_NOISE");
            else material.EnableKeyword("_SECONDARY_NOISE");
        }

    }
}
