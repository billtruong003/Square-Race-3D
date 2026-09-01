using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// The two project-owned accent materials the arena needs, on the project's own ToonLit shader.
    ///
    /// The environment's own stone comes from the Kenney atlas via <see cref="KenneyKit"/>; what is
    /// left here is the pair of signal colours that have to read instantly on camera. The turf,
    /// soil and stone tones that used to live here went with the rock-built arena.
    /// </summary>
    internal class ArenaMaterials
    {
        private const string Root = "Assets/ChallengeShow/Materials";

        public Material finish;
        public Material hazard;

        public static ArenaMaterials Load() => new()
        {
            finish = GetOrCreate("Arena_Finish", new Color(1f, 0.83f, 0.25f), new Color(0.45f, 0.34f, 0.18f)),
            // Warning accent on the arm's pivot, so the obstacle reads as the hazard it is.
            hazard = GetOrCreate("Arena_Hazard", new Color(0.95f, 0.35f, 0.18f), new Color(0.42f, 0.14f, 0.08f))
        };

        private static Material GetOrCreate(string name, Color baseColor, Color shadowColor)
        {
            string path = $"{Root}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", shadowColor);
            if (mat.HasProperty("_UseLocalToon"))
            {
                mat.SetFloat("_UseLocalToon", 1f);
                mat.EnableKeyword("_USE_LOCAL_TOON");
            }
            if (mat.HasProperty("_Threshold")) mat.SetFloat("_Threshold", 0.28f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
