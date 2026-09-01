using UnityEditor;
using UnityEngine;

public class StylizedWaterVRGUI : ShaderGUI
{
    private bool _foldColor = true;
    private bool _foldNormals = false;
    private bool _foldRefraction = false;
    private bool _foldSurfaceFoam = true;
    private bool _foldIntersection = true;
    private bool _foldBling = false;
    private bool _foldWaves = false;

    private static GUIStyle _headerStyle;
    private static GUIStyle _sectionBox;
    private static GUIStyle _bannerStyle;
    private static bool _stylesInit;

    private static readonly Color AccentWater = new Color(0.3f, 0.7f, 1f, 1f);

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
        DrawBanner("STYLIZED WATER", AccentWater);
        EditorGUILayout.Space(4);

        _foldColor = DrawSection("Color & Depth", _foldColor, () =>
        {
            DrawProp(materialEditor, properties, "_ShallowColor", "Shallow Color");
            DrawProp(materialEditor, properties, "_DeepColor", "Deep Color (HDR)");
            DrawProp(materialEditor, properties, "_DepthMaxDistance", "Depth Max Distance");
        });

        _foldNormals = DrawSection("Normal Map", _foldNormals, () =>
        {
            DrawTextureSingleLine(materialEditor, properties, "_NormalMap", "Normal Map");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Layer A", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_NormalTilingA", "Tiling");
            DrawProp(materialEditor, properties, "_NormalScrollA", "Scroll Speed");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Layer B", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_NormalTilingB", "Tiling");
            DrawProp(materialEditor, properties, "_NormalScrollB", "Scroll Speed");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
            DrawProp(materialEditor, properties, "_NormalStrength", "Strength");
        });

        _foldRefraction = DrawSection("Refraction", _foldRefraction, () => DrawProp(materialEditor, properties, "_RefractionStrength", "Strength"));

        _foldSurfaceFoam = DrawSection("Surface Foam", _foldSurfaceFoam, () =>
        {
            DrawTextureSingleLine(materialEditor, properties, "_SurfaceFoamTexture", "Foam Texture");
            DrawProp(materialEditor, properties, "_SurfaceFoamTiling", "Tiling");
            DrawProp(materialEditor, properties, "_SurfaceFoamScroll", "Scroll Speed");
            DrawProp(materialEditor, properties, "_SurfaceFoamCutoff", "Cutoff");
            DrawProp(materialEditor, properties, "_FoamDistortion", "Normal Distortion");
        });

        _foldIntersection = DrawSection("Intersection Foam", _foldIntersection, () =>
        {
            DrawProp(materialEditor, properties, "_FoamColor", "Foam Color");
            DrawProp(materialEditor, properties, "_FoamIntersectionDepth", "Intersection Depth");
            DrawProp(materialEditor, properties, "_FoamEdgeSmoothness", "Edge Smoothness");
        });

        _foldBling = DrawSection("Specular Bling", _foldBling, () =>
        {
            DrawProp(materialEditor, properties, "_BlingColor", "Bling Color (HDR)");
            DrawProp(materialEditor, properties, "_BlingGloss", "Gloss");
            DrawProp(materialEditor, properties, "_BlingThreshold", "Threshold");
            DrawProp(materialEditor, properties, "_BlingIntensity", "Intensity");
        });

        _foldWaves = DrawSection("Vertex Waves", _foldWaves, () =>
        {
            DrawProp(materialEditor, properties, "_WaveAmplitude", "Amplitude");
            DrawProp(materialEditor, properties, "_WaveFrequency", "Frequency");
            DrawProp(materialEditor, properties, "_WaveSpeed", "Speed");
        });

        EditorGUILayout.Space(6);

        MaterialProperty cullProp = FindProperty("_Cull", properties, false);
        if (cullProp != null)
        {
            EditorGUI.BeginChangeCheck();
            var cullMode = (UnityEngine.Rendering.CullMode)cullProp.floatValue;
            cullMode = (UnityEngine.Rendering.CullMode)EditorGUILayout.EnumPopup("Cull Mode", cullMode);
            if (EditorGUI.EndChangeCheck()) cullProp.floatValue = (float)cullMode;
        }

        materialEditor.RenderQueueField();
    }

    private static bool DrawSection(string title, bool foldout, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), AccentWater);

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