using UnityEditor;
using UnityEngine;

public class ToonLavaGUI : ShaderGUI
{
    private bool _foldTextures = true;
    private bool _foldScrolling = false;
    private bool _foldColors = true;
    private bool _foldEdgeGlow = true;
    private bool _foldTopGlow = false;
    private bool _foldWaves = false;
    private bool _foldCelShading = false;

    private static GUIStyle _headerStyle;
    private static GUIStyle _sectionBox;
    private static GUIStyle _bannerStyle;
    private static bool _stylesInit;

    private static readonly Color AccentLava = new Color(1f, 0.45f, 0.1f, 1f);

    private static void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true };
        _sectionBox = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) };
        _bannerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        InitStyles();

        EditorGUILayout.Space(4);
        DrawBanner("TOON LAVA", AccentLava);
        EditorGUILayout.Space(4);

        _foldTextures = DrawSection("Lava Textures", _foldTextures, () =>
        {
            DrawTextureSingleLine(materialEditor, properties, "_MainTex", "Main Lava Texture");
            DrawTextureSingleLine(materialEditor, properties, "_NoiseTex", "Noise / Distort Texture");
        });

        _foldScrolling = DrawSection("Scrolling & Distortion", _foldScrolling, () =>
        {
            DrawProp(materialEditor, properties, "_Scale", "Noise Scale");
            DrawProp(materialEditor, properties, "_MainScale", "Main Texture Scale");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Distortion Speed", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_SpeedDistortX", "X");
            DrawProp(materialEditor, properties, "_SpeedDistortY", "Y");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Main Scroll Speed", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_SpeedMainX", "X");
            DrawProp(materialEditor, properties, "_SpeedMainY", "Y");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
            DrawProp(materialEditor, properties, "_DistortionStrength", "Noise Distortion");
            DrawProp(materialEditor, properties, "_VCDistortionStrength", "Vertex Color Distortion");
        });

        _foldColors = DrawSection("Colors", _foldColors, () =>
        {
            DrawProp(materialEditor, properties, "_TintStart", "Cool Tint");
            DrawProp(materialEditor, properties, "_TintEnd", "Hot Tint");
            DrawProp(materialEditor, properties, "_TintOffset", "Tint Offset");
            DrawProp(materialEditor, properties, "_BrightnessUnder", "Under-Surface Brightness");
        });

        _foldEdgeGlow = DrawSection("Edge Glow (Intersection)", _foldEdgeGlow, () =>
        {
            DrawProp(materialEditor, properties, "_EdgeThickness", "Thickness");
            DrawProp(materialEditor, properties, "_EdgeSmoothness", "Edge Smoothness");
            DrawProp(materialEditor, properties, "_EdgeColor", "Edge Color (HDR)");
            DrawProp(materialEditor, properties, "_EdgeBrightness", "Brightness");
        });

        _foldTopGlow = DrawSection("Top Glow", _foldTopGlow, () =>
        {
            DrawProp(materialEditor, properties, "_CutoffTop", "Cutoff");
            DrawProp(materialEditor, properties, "_TopSmoothness", "Top Smoothness");
            DrawProp(materialEditor, properties, "_TopColor", "Top Color (HDR)");
        });

        _foldWaves = DrawSection("Vertex Waves", _foldWaves, () =>
        {
            DrawProp(materialEditor, properties, "_WaveAmount", "Amount");
            DrawProp(materialEditor, properties, "_WaveSpeed", "Speed");
            DrawProp(materialEditor, properties, "_WaveHeight", "Height");
        });

        _foldCelShading = DrawSection("Cel Shading", _foldCelShading, () =>
        {
            DrawProp(materialEditor, properties, "_ShadowColor", "Shadow Color");
            DrawProp(materialEditor, properties, "_Threshold", "Shadow Threshold");
            DrawProp(materialEditor, properties, "_Smoothness", "Shadow Smoothness");
        });

        EditorGUILayout.Space(6);
        materialEditor.RenderQueueField();
    }

    private static bool DrawSection(string title, bool foldout, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), AccentLava);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
        {
            foldout = !foldout;
            e.Use();
        }

        EditorGUI.LabelField(new Rect(headerRect.x + 16f, headerRect.y + 2f, headerRect.width - 16f, headerRect.height), (foldout ? "▼ " : "► ") + title, _headerStyle);
        if (foldout)
        {
            EditorGUILayout.BeginVertical(_sectionBox);
            drawContent();
            EditorGUILayout.EndVertical();
        }
        return foldout;
    }

    private static void DrawBanner(string text, Color color)
    {
        Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), color);

        Color oldColor = GUI.contentColor;
        GUI.contentColor = color;
        EditorGUI.LabelField(r, text, _bannerStyle);
        GUI.contentColor = oldColor;
    }

    private static void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name, string label)
    {
        MaterialProperty p = FindProperty(name, props, false);
        if (p != null) editor.ShaderProperty(p, label);
    }

    private static void DrawTextureSingleLine(MaterialEditor editor, MaterialProperty[] props, string name, string label)
    {
        MaterialProperty p = FindProperty(name, props, false);
        if (p != null) editor.TexturePropertySingleLine(new GUIContent(label), p);
    }
}