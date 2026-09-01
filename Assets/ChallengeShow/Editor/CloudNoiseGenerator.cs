using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Bakes the tiling noise the cloud shader samples into a real asset.
    ///
    /// Generating it at runtime worked but did not survive a domain reload, so the material lost its
    /// texture every recompile and the cloud sea silently vanished from edit-mode screenshots. A
    /// baked asset is also what the shader wants: the field is fixed art, not something that needs
    /// to vary per session.
    /// </summary>
    public static class CloudNoiseGenerator
    {
        public const string AssetPath = "Assets/ChallengeShow/Art/CloudNoise.asset";
        private const int Resolution = 256;
        private const int Seed = 20260826;

        [MenuItem("Challenge Show/5. Bake Cloud Noise")]
        public static void Bake()
        {
            var tex = Generate(Resolution, Seed);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
            if (existing != null)
            {
                // Overwrite in place so every material already pointing at it keeps its reference.
                EditorUtility.CopySerialized(tex, existing);
                Object.DestroyImmediate(tex);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                EnsureFolder("Assets/ChallengeShow/Art");
                AssetDatabase.CreateAsset(tex, AssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChallengeShow] Baked cloud noise {Resolution}x{Resolution} to {AssetPath}");
        }

        public static Texture2D LoadOrBake()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
            if (tex != null) return tex;
            Bake();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        }

        /// <summary>Two octaves of tiling value-noise fBm packed into R and G.</summary>
        private static Texture2D Generate(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "CloudNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };

            var rng = new System.Random(seed);
            var offsets = new Vector2[6];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = new Vector2((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // Two independent bands: a broad one that shapes the billows and a finer one that
                // breaks their edges up.
                float a = TilingFbm(u, v, 4, offsets[0], offsets[1], offsets[2]);
                float b = TilingFbm(u, v, 9, offsets[3], offsets[4], offsets[5]);

                pixels[y * size + x] = new Color32(
                    (byte)(Mathf.Clamp01(a) * 255f),
                    (byte)(Mathf.Clamp01(b) * 255f),
                    0, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        private static float TilingFbm(float u, float v, int basePeriod, Vector2 o1, Vector2 o2, Vector2 o3)
        {
            float total = 0f, amplitude = 1f, norm = 0f;
            var offsets = new[] { o1, o2, o3 };

            for (int octave = 0; octave < 3; octave++)
            {
                int period = basePeriod << octave;
                total += PeriodicValueNoise(u, v, period, offsets[octave]) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
            }
            return total / Mathf.Max(norm, 1e-4f);
        }

        /// <summary>
        /// Value noise on a wrapped integer lattice. Unity's PerlinNoise is not periodic, and a
        /// non-tiling field shows an obvious repeat seam across a cloud sea this wide.
        /// </summary>
        private static float PeriodicValueNoise(float u, float v, int period, Vector2 offset)
        {
            float x = u * period;
            float y = v * period;

            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float sx = xf * xf * (3f - 2f * xf);
            float sy = yf * yf * (3f - 2f * yf);

            float n00 = Hash(Mod(xi, period), Mod(yi, period), offset);
            float n10 = Hash(Mod(xi + 1, period), Mod(yi, period), offset);
            float n01 = Hash(Mod(xi, period), Mod(yi + 1, period), offset);
            float n11 = Hash(Mod(xi + 1, period), Mod(yi + 1, period), offset);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private static float Hash(int x, int y, Vector2 offset)
        {
            float h = Mathf.Sin((x + offset.x) * 127.1f + (y + offset.y) * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
