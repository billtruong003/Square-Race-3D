using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Project-owned accent materials for the environment macros.
    ///
    /// These exist so the bastion standards and the finish accent are real assets rather than
    /// materials built with `new Material(...)` at build time. The old builder did the latter, which
    /// embedded five one-off material instances straight into the scene: they could never batch with
    /// anything, they were invisible to any material audit, and they were recreated on every rebuild.
    ///
    /// Family materials are keyed by colour, so two families sharing an accent share a material and
    /// the asset path stays deterministic across rebakes.
    /// </summary>
    public static class EnvironmentMaterials
    {
        private const string Root = "Assets/ChallengeShow/Environment/Materials";
        private static readonly Dictionary<string, Material> Cache = new();

        /// <summary>The finish line's warning accent, shared by every landmark that needs it.</summary>
        public static Material Accent() =>
            GetOrCreate("Accent_Finish", new Color(1f, 0.83f, 0.25f));

        /// <summary>
        /// A family's heraldic colour, pulled toward the stone so it reads as paint rather than an
        /// emissive strip. At full saturation these bars were the brightest thing in the frame.
        /// </summary>
        public static Material Family(Color accent)
        {
            Color tinted = Color.Lerp(accent, new Color(0.62f, 0.64f, 0.70f), 0.32f);
            string key = ColorUtility.ToHtmlStringRGB(tinted);
            return GetOrCreate($"Family_{key}", tinted);
        }

        private static Material GetOrCreate(string name, Color color)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

            KenneyMacroBaker.EnsureFolder(Root);
            string path = $"{Root}/{name}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0f);

            Cache[name] = mat;
            return mat;
        }
    }
}
