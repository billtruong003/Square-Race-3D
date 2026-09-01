using UnityEngine;
using UnityEditor;

public class StylizedSkyboxGUI : ShaderGUI
{
    private bool _foldSun = true;
    private bool _foldMoon = true;
    private bool _foldSky = true;
    private bool _foldHorizon = true;
    private bool _foldStars = true;
    private bool _foldClouds = true;
    private bool _foldClouds2 = true;
    private bool _foldCloudColors = true;
    private bool _foldPerf = false;

    private static GUIStyle _headerStyle;
    private static GUIStyle _bannerTitleStyle;
    private static GUIStyle _bannerSubStyle;
    private static GUIStyle _ratingStyle;
    private static GUIStyle _featureRowStyle;
    private static bool _stylesInit;

    private static void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _headerStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold, fontSize = 12 };
        _bannerTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = new Color(0.9f, 0.95f, 1f) } };
        _bannerSubStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f) } };
        _ratingStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
        _featureRowStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        InitStyles();
        Material mat = materialEditor.target as Material;
        if (mat == null) return;

        EditorGUILayout.Space(4);
        DrawBanner("VR Stylized Skybox", "Optimized for Single Pass Instanced VR");
        EditorGUILayout.Space(6);

        _foldSun = DrawSection("Sun", _foldSun, () =>
        {
            DrawProp(materialEditor, properties, "_SunColor", "Sun Color (HDR)");
            DrawProp(materialEditor, properties, "_SunRadius", "Sun Size");
            DrawProp(materialEditor, properties, "_SunSharpness", "Edge Sharpness");
        });

        _foldMoon = DrawSection("Moon", _foldMoon, () =>
        {
            DrawProp(materialEditor, properties, "_MoonColor", "Moon Color (HDR)");
            DrawProp(materialEditor, properties, "_MoonRadius", "Moon Size");
            DrawProp(materialEditor, properties, "_MoonSharpness", "Edge Sharpness");
            DrawProp(materialEditor, properties, "_MoonOffset", "Crescent Offset");
        });

        _foldSky = DrawSection("Sky Colors", _foldSky, () =>
        {
            EditorGUILayout.LabelField("Day Sky", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_DayTopColor", "  Top Color");
            DrawProp(materialEditor, properties, "_DayBottomColor", "  Bottom Color");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Night Sky", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_NightTopColor", "  Top Color");
            DrawProp(materialEditor, properties, "_NightBottomColor", "  Bottom Color");
        });

        _foldHorizon = DrawSection("Horizon", _foldHorizon, () =>
        {
            DrawProp(materialEditor, properties, "_HorizonColorDay", "Day Horizon");
            DrawProp(materialEditor, properties, "_HorizonColorNight", "Night Horizon");
            DrawProp(materialEditor, properties, "_HorizonWidth", "Width");
            DrawProp(materialEditor, properties, "_OffsetHorizon", "Vertical Offset");
        });

        _foldStars = DrawToggleSection("Stars", _foldStars, materialEditor, properties, "_EnableStars", "_STARS_ON", () =>
        {
            DrawProp(materialEditor, properties, "_Stars", "Stars Texture");
            DrawProp(materialEditor, properties, "_StarsCutoff", "Brightness Cutoff");
            DrawProp(materialEditor, properties, "_StarsSpeed", "UV Scale");
            DrawProp(materialEditor, properties, "_StarsSkyColor", "Tint Color");
        });

        _foldClouds = DrawToggleSection("Clouds - Primary", _foldClouds, materialEditor, properties, "_EnableClouds", "_CLOUDS_ON", () =>
        {
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_BaseNoise", "  Base Noise");
            DrawProp(materialEditor, properties, "_Distort", "  Detail Noise");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scale & Distortion", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_BaseNoiseScale", "  Base Scale");
            DrawProp(materialEditor, properties, "_DistortScale", "  Detail Scale");
            DrawProp(materialEditor, properties, "_Distortion", "  Distortion");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_BaseNoiseSpeed", "  Base Scroll");
            DrawProp(materialEditor, properties, "_CloudsLayerSpeed", "  Cloud Scroll");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_CloudCutoff", "  Cutoff");
            DrawProp(materialEditor, properties, "_Fuzziness", "  Softness");
            DrawProp(materialEditor, properties, "_HorizonCloudsFade", "  Horizon Fade");
        });

        bool cloudsEnabled = mat.IsKeywordEnabled("_CLOUDS_ON");
        EditorGUI.BeginDisabledGroup(!cloudsEnabled);
        _foldClouds2 = DrawToggleSection("Clouds - Secondary Layer", _foldClouds2, materialEditor, properties, "_EnableClouds2", "_CLOUDS2_ON", () =>
        {
            if (!cloudsEnabled)
            {
                EditorGUILayout.HelpBox("Enable Primary Clouds first", MessageType.Info);
                return;
            }
            DrawProp(materialEditor, properties, "_SecNoise", "Noise Texture");
            DrawProp(materialEditor, properties, "_SecNoiseScale", "Scale");
            DrawProp(materialEditor, properties, "_CloudCutoff2", "Cutoff");
            DrawProp(materialEditor, properties, "_Fuzziness2", "Softness");
            DrawProp(materialEditor, properties, "_OpacitySec", "Opacity");
        });
        EditorGUI.EndDisabledGroup();

        _foldCloudColors = DrawSection("Cloud Colors", _foldCloudColors, () =>
        {
            DrawProp(materialEditor, properties, "_ColorStretch", "Color Stretch");
            DrawProp(materialEditor, properties, "_ColorOffset", "Color Offset");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Day Clouds", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_CloudColorDayEdge", "  Edge Color");
            DrawProp(materialEditor, properties, "_CloudColorDayMain", "  Core Color");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Night Clouds", EditorStyles.boldLabel);
            DrawProp(materialEditor, properties, "_CloudColorNightEdge", "  Edge Color");
            DrawProp(materialEditor, properties, "_CloudColorNightMain", "  Core Color");
        });

        EditorGUILayout.Space(8);
        DrawPerformanceSection(mat);
        EditorGUILayout.Space(8);
    }

    private void DrawBanner(string title, string subtitle)
    {
        var rect = EditorGUILayout.GetControlRect(false, 42);
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
        EditorGUI.DrawRect(inner, new Color(0.22f, 0.22f, 0.28f, 1f));
        EditorGUI.LabelField(new Rect(inner.x + 10, inner.y + 4, inner.width - 20, 18), title, _bannerTitleStyle);
        EditorGUI.LabelField(new Rect(inner.x + 10, inner.y + 22, inner.width - 20, 14), subtitle, _bannerSubStyle);
    }

    private bool DrawSection(string label, bool foldout, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        var bgRect = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.DrawRect(bgRect, new Color(0.24f, 0.24f, 0.24f, 1f));
        foldout = EditorGUI.Foldout(bgRect, foldout, " " + label, true, _headerStyle);
        if (foldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);
            drawContent();
            EditorGUILayout.Space(4);
            EditorGUI.indentLevel--;
        }
        return foldout;
    }

    private bool DrawToggleSection(string label, bool foldout, MaterialEditor materialEditor, MaterialProperty[] properties, string toggleProp, string keyword, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        MaterialProperty toggle = FindProperty(toggleProp, properties, false);
        if (toggle == null) return foldout;

        Material mat = materialEditor.target as Material;
        bool enabled = toggle.floatValue > 0.5f;

        var bgRect = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.DrawRect(bgRect, enabled ? new Color(0.2f, 0.3f, 0.2f, 1f) : new Color(0.25f, 0.22f, 0.22f, 1f));

        var toggleRect = new Rect(bgRect.x + bgRect.width - 40, bgRect.y + 2, 18, 18);
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUI.Toggle(toggleRect, enabled);
        if (EditorGUI.EndChangeCheck())
        {
            toggle.floatValue = enabled ? 1f : 0f;
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        foldout = EditorGUI.Foldout(bgRect, foldout, " " + label, true, _headerStyle);

        if (foldout && enabled)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);
            drawContent();
            EditorGUILayout.Space(4);
            EditorGUI.indentLevel--;
        }
        else if (foldout && !enabled)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("Feature disabled", MessageType.None);
            EditorGUI.indentLevel--;
        }

        return foldout;
    }

    private void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name, string displayName)
    {
        MaterialProperty prop = FindProperty(name, props, false);
        if (prop != null) editor.ShaderProperty(prop, new GUIContent(displayName));
    }

    private void DrawPerformanceSection(Material mat)
    {
        _foldPerf = DrawSection("VR Performance", _foldPerf, () =>
        {
            bool stars = mat.IsKeywordEnabled("_STARS_ON");
            bool clouds = mat.IsKeywordEnabled("_CLOUDS_ON");
            bool clouds2 = mat.IsKeywordEnabled("_CLOUDS2_ON");

            int texSamples = (stars ? 1 : 0) + (clouds ? 2 : 0) + (clouds2 ? 1 : 0);
            int aluCost = 15 + (stars ? 5 : 0) + (clouds ? 10 : 0) + (clouds2 ? 6 : 0);

            string rating = texSamples <= 1 ? "Excellent" : (texSamples <= 3 ? "Good" : "Moderate");
            Color ratingColor = texSamples <= 1 ? new Color(0.3f, 0.9f, 0.3f) : (texSamples <= 3 ? new Color(0.9f, 0.9f, 0.3f) : new Color(0.9f, 0.6f, 0.2f));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Color oldColor = GUI.contentColor;
            GUI.contentColor = ratingColor;
            EditorGUILayout.LabelField(rating, _ratingStyle);
            GUI.contentColor = oldColor;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Texture Samples: {texSamples}/frame/pixel", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Est. ALU Instructions: ~{aluCost}/pixel", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            DrawFeatureRow("Stars", stars, 1, 5);
            DrawFeatureRow("Primary Clouds", clouds, 2, 10);
            DrawFeatureRow("Secondary Clouds", clouds2, 1, 6);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("VR Optimizations Active:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Single Pass Instanced Stereo", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Vertex Shader Offloading", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Half Precision", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Angular Dot-Product", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("ZWrite Off", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Branch-free design", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        });
    }

    private void DrawFeatureRow(string name, bool enabled, int texCost, int aluCost)
    {
        EditorGUILayout.BeginHorizontal();
        Color oldColor = GUI.contentColor;
        GUI.contentColor = enabled ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField($"[{(enabled ? "ON" : "OFF")}] {name}", _featureRowStyle, GUILayout.Width(200));
        GUI.contentColor = oldColor;
        EditorGUILayout.LabelField(enabled ? $"+{texCost} tex, +{aluCost} ALU" : "0 cost", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
}