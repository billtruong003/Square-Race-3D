using System;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Camera finishing for an episode. Data-driven like everything else, so a future theme can dial
    /// the look without touching code.
    ///
    /// The reference videos lean heavily on bloom: the pressure, the goal, the colour gates and the
    /// trails are all emissive, and without bloom they read as flat coloured shapes.
    /// </summary>
    [Serializable]
    public class PostProcessingConfig
    {
        public bool enabled = true;

        [Header("Bloom")]
        public bool bloom = true;

        [Tooltip("Brightness a pixel needs before it blooms. Low values make the whole scene glow.")]
        public float bloomThreshold = 1.0f;

        [Tooltip("Bloom should halo the emissive shapes, not repaint them. Too much and the green " +
                 "goal turns white and the red hazard turns orange, which is what the reference " +
                 "never does - there the goal samples as pure (0,253,0).")]
        public float bloomIntensity = 2.0f;

        [Range(0f, 1f)] public float bloomScatter = 0.6f;

        [Header("Tonemapping and colour")]
        public bool tonemapping = true;

        [Tooltip("-100..100. Slight positive keeps the darks rich without crushing the corridors.")]
        public float contrast = 8f;

        [Tooltip("-100..100. The reference palette is punchy.")]
        public float saturation = 14f;

        [Tooltip("Exposure in EV. Small negative values stop the floor washing out under bloom.")]
        public float postExposure = 0.1f;

        public Color colorFilter = Color.white;

        [Header("Vignette")]
        public bool vignette = true;

        [Range(0f, 1f)] public float vignetteIntensity = 0.26f;

        [Range(0.01f, 1f)] public float vignetteSmoothness = 0.65f;

        public Color vignetteColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [Header("Camera")]
        [Tooltip("Fast approximate antialiasing. Cheap, and it holds up better than SMAA once " +
                 "YouTube re-encodes the footage.")]
        public bool antialiasing = true;
    }
}
