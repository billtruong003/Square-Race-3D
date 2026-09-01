using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Appearance data for an episode. Kept separate from simulation data so an agent can restyle a
    /// run without touching a single gameplay value.
    /// </summary>
    [Serializable]
    public class VisualTheme
    {
        [Tooltip("Toon shader from the project. Falls back to URP/Lit when unavailable.")]
        public string shaderName = "CleanRender/ToonLit";

        // Measured off the reference video rather than eyeballed. There the floor renders at sRGB 78
        // and a wall top at 66 - barely a step apart, both dark. The whole arena is a low-contrast
        // dark field, and the only bright things on screen are the racers, the goal, the hazard and
        // the pressure. An earlier pass here pushed the floor to 141 and the walls to 26, which reads
        // as black bars laid on a white sheet and buries every accent colour.
        public Color floorColor = new Color(0.36f, 0.36f, 0.38f, 1f);
        public Color wallColor = new Color(0.30f, 0.30f, 0.33f, 1f);

        // ToonLit paints everything past its shadow threshold with this flat colour, so at a top-down
        // camera it - not the albedo - is what most of the arena actually renders as.
        public Color shadowColor = new Color(0.19f, 0.19f, 0.22f, 1f);

        public Color pressureColor = new Color(1f, 0.55f, 0.06f, 1f);
        [Range(0f, 8f)] public float pressureEmission = 0.5f;

        [Range(0f, 6f)] public float racerEmission = 0.55f;

        [Tooltip("Used for racers when the team list runs out of colours.")]
        public List<Color> palette = new List<Color>
        {
            new Color(0.95f, 0.16f, 0.16f), // red
            new Color(0.16f, 0.85f, 0.24f), // green
            new Color(0.18f, 0.36f, 0.98f), // blue
            new Color(0.98f, 0.86f, 0.14f), // yellow
            new Color(0.98f, 0.45f, 0.10f), // orange
            new Color(0.85f, 0.20f, 0.90f), // magenta
            new Color(0.20f, 0.90f, 0.92f), // cyan
            new Color(0.55f, 0.22f, 0.95f), // violet
            new Color(0.95f, 0.55f, 0.72f), // pink
            new Color(0.72f, 0.95f, 0.30f)  // lime
        };

        [Tooltip("Camera finishing: bloom, tonemapping, vignette.")]
        public PostProcessingConfig post = new PostProcessingConfig();

        [Header("Lighting")]
        public Color ambientColor = new Color(0.20f, 0.20f, 0.24f, 1f);
        public Color lightColor = new Color(1f, 0.97f, 0.92f, 1f);
        [Range(0f, 4f)] public float lightIntensity = 1.05f;
        public Vector3 lightEuler = new Vector3(62f, 24f, 0f);
    }
}
