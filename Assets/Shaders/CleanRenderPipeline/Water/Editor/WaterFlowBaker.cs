#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CleanRender.Editor
{
    /// <summary>
    /// Water Flow Baker: Generates flow map textures for waterfalls and rivers.
    /// Flow map encodes direction (RG channels) for noise scrolling.
    /// Allows painting custom flow directions onto a texture.
    /// </summary>
    public class WaterFlowBaker : EditorWindow
    {
        private enum FlowPreset
        {
            Downward,
            Diagonal45,
            DiagonalNeg45,
            Horizontal,
            Radial,
            Custom
        }

        private int resolution = 256;
        private FlowPreset preset = FlowPreset.Downward;
        private float flowAngle = 0f;
        private float flowStrength = 1f;
        private float noiseAmount = 0.1f;
        private Texture2D previewTex;
        private Texture2D resultTex;

        [MenuItem("Tools/CleanRender/Water Flow Baker")]
        public static void ShowWindow()
        {
            var w = GetWindow<WaterFlowBaker>("Flow Baker");
            w.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            GUILayout.Label("━━━ WATER FLOW MAP BAKER ━━━", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            resolution = EditorGUILayout.IntPopup("Resolution",
                resolution, new string[] { "64", "128", "256", "512" },
                new int[] { 64, 128, 256, 512 });

            preset = (FlowPreset)EditorGUILayout.EnumPopup("Flow Preset", preset);

            if (preset == FlowPreset.Custom)
                flowAngle = EditorGUILayout.Slider("Flow Angle (degrees)", flowAngle, 0, 360);

            flowStrength = EditorGUILayout.Slider("Flow Strength", flowStrength, 0.1f, 2f);
            noiseAmount = EditorGUILayout.Slider("Noise Variation", noiseAmount, 0f, 0.5f);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
                GenerateFlowMap();

            if (previewTex != null)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("Preview (RG = flow direction):");
                var rect = GUILayoutUtility.GetRect(256, 256);
                EditorGUI.DrawPreviewTexture(rect, previewTex);

                EditorGUILayout.Space(5);
                if (GUILayout.Button("Save as PNG", GUILayout.Height(30)))
                    SaveFlowMap();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Flow Map Guide:\n" +
                "• R channel = X direction (-1 to 1, encoded as 0 to 1)\n" +
                "• G channel = Y direction (-1 to 1, encoded as 0 to 1)\n" +
                "• 0.5, 0.5 = no flow\n" +
                "• Use 'Downward' for vertical waterfalls\n" +
                "• Use 'Diagonal' for angled waterfalls\n" +
                "• Assign to ToonWater material's Flow Map slot",
                MessageType.Info);
        }

        private void GenerateFlowMap()
        {
            resultTex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            Color[] pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / resolution;
                    float v = (float)y / resolution;

                    Vector2 flow = GetFlowDirection(u, v);
                    flow *= flowStrength;

                    if (noiseAmount > 0)
                    {
                        float nx = Mathf.PerlinNoise(u * 5f + 42.3f, v * 5f + 17.1f) * 2f - 1f;
                        float ny = Mathf.PerlinNoise(u * 5f + 73.2f, v * 5f + 91.4f) * 2f - 1f;
                        flow += new Vector2(nx, ny) * noiseAmount;
                    }

                    float r = flow.x * 0.5f + 0.5f;
                    float g = flow.y * 0.5f + 0.5f;

                    pixels[y * resolution + x] = new Color(r, g, 0.5f);
                }
            }

            resultTex.SetPixels(pixels);
            resultTex.Apply();
            previewTex = resultTex;
        }

        private Vector2 GetFlowDirection(float u, float v)
        {
            switch (preset)
            {
                case FlowPreset.Downward:
                    return new Vector2(0, -1);

                case FlowPreset.Diagonal45:
                    return new Vector2(0.707f, -0.707f);

                case FlowPreset.DiagonalNeg45:
                    return new Vector2(-0.707f, -0.707f);

                case FlowPreset.Horizontal:
                    return new Vector2(1, 0);

                case FlowPreset.Radial:
                    float cx = u - 0.5f;
                    float cy = v - 0.5f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    if (dist < 0.01f) return Vector2.zero;
                    return new Vector2(-cy, cx) / dist;

                case FlowPreset.Custom:
                    float rad = flowAngle * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                default:
                    return Vector2.zero;
            }
        }

        private void SaveFlowMap()
        {
            string path = EditorUtility.SaveFilePanel("Save Flow Map",
                "Assets", "FlowMap", "png");
            if (string.IsNullOrEmpty(path)) return;

            byte[] png = resultTex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, png);

            string assetPath = path;
            if (path.StartsWith(Application.dataPath))
                assetPath = "Assets" + path.Substring(Application.dataPath.Length);

            AssetDatabase.Refresh();

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            EditorUtility.DisplayDialog("Saved", $"Flow map saved to:\n{assetPath}\n\nImported as Linear, Uncompressed.", "OK");
        }
    }
}
#endif
