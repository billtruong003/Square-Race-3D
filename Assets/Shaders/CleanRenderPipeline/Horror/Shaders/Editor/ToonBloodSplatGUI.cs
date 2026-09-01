using UnityEditor;
using UnityEngine;

public class ToonBloodSplatGUI : ShaderGUI
{
    private bool _foldSurface = true;
    private bool _foldBlood = true;
    private bool _foldDrip = true;
    private bool _foldPulse = false;
    private bool _foldCel = false;
    private bool _foldEmission = false;
    private bool _foldOptions = false;

    private static GUIStyle _headerStyle;
    private static GUIStyle _sectionBox;
    private static GUIStyle _bannerStyle;
    private static bool _stylesInit;

    private static readonly Color AccentBlood = new Color(0.8f, 0.1f, 0.1f, 1f);
    private static readonly Color AccentDrip = new Color(0.6f, 0.05f, 0.05f, 1f);
    private static readonly Color AccentPulse = new Color(1f, 0.2f, 0.2f, 1f);

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
        Material mat = materialEditor.target as Material;

        EditorGUILayout.Space(4);
        DrawBanner("\u2620  TOON BLOOD SPLAT", AccentBlood);
        EditorGUILayout.Space(4);

        _foldSurface = DrawSection("Surface", _foldSurface, AccentBlood, () =>
        {
            DrawTextureProp(materialEditor, properties, "_BaseMap", "_BaseColor", "Base Map");
        });

        _foldBlood = DrawSection("Blood", _foldBlood, AccentBlood, () =>
        {
            DrawProp(materialEditor, properties, "_BloodColor", "Fresh Blood Color");
            DrawProp(materialEditor, properties, "_DriedBloodColor", "Dried Blood Color");
            DrawTextureSingleLine(materialEditor, properties, "_BloodMask", "Blood Mask (R=splat)");
            EditorGUILayout.Space(4);
            DrawTextureSingleLine(materialEditor, properties, "_NoiseTex", "Noise Texture (Normal)");
            DrawProp(materialEditor, properties, "_NoiseScale", "Noise Tiling");
            DrawProp(materialEditor, properties, "_NormalStrength", "Normal Strength");
            EditorGUILayout.Space(2);
            DrawProp(materialEditor, properties, "_BloodWetness", "Wetness");
            DrawProp(materialEditor, properties, "_BloodEdgeNoise", "Edge Distortion");
        });

        _foldDrip = DrawSection("Drip Animation", _foldDrip, AccentDrip, () =>
        {
            DrawToggleKeyword(materialEditor, properties, mat, "_EnableDrip", "_DRIP_ON", "Enable Drips");
            if (mat.IsKeywordEnabled("_DRIP_ON"))
            {
                DrawProp(materialEditor, properties, "_DripSpeed", "Drip Speed");
                DrawProp(materialEditor, properties, "_DripScale", "Drip Scale");
                DrawProp(materialEditor, properties, "_DripColor", "Drip Color");
            }
        });

        _foldPulse = DrawSection("Heartbeat Pulse", _foldPulse, AccentPulse, () =>
        {
            DrawToggleKeyword(materialEditor, properties, mat, "_EnablePulse", "_PULSE_ON", "Enable Pulse");
            if (mat.IsKeywordEnabled("_PULSE_ON"))
            {
                DrawProp(materialEditor, properties, "_PulseRate", "Pulse Rate");
                DrawProp(materialEditor, properties, "_PulseStrength", "Pulse Strength");
                DrawProp(materialEditor, properties, "_PulseEmission", "Pulse Emission (HDR)");
            }
        });

        _foldCel = DrawSection("Cel Shading", _foldCel, AccentBlood, () =>
        {
            DrawProp(materialEditor, properties, "_ShadowColor", "Shadow Color");
            DrawProp(materialEditor, properties, "_Threshold", "Shadow Threshold");
            DrawProp(materialEditor, properties, "_Smoothness", "Shadow Smoothness");
            EditorGUILayout.Space(2);
            DrawProp(materialEditor, properties, "_RimColor", "Rim Color (RGB + A)");
            DrawProp(materialEditor, properties, "_RimPower", "Rim Power");
        });

        _foldEmission = DrawSection("Emission", _foldEmission, AccentPulse, () =>
        {
            DrawToggleKeyword(materialEditor, properties, mat, "_Emission", "_EMISSION", "Enable Emission");
            if (mat.IsKeywordEnabled("_EMISSION"))
            {
                DrawProp(materialEditor, properties, "_EmissionColor", "Emission Color (HDR)");
                DrawTextureSingleLine(materialEditor, properties, "_EmissionMap", "Emission Map");
            }
        });

        _foldOptions = DrawSection("Options", _foldOptions, new Color(0.5f, 0.5f, 0.5f), () =>
        {
            DrawProp(materialEditor, properties, "_Cull", "Cull Mode");
            DrawToggleKeyword(materialEditor, properties, mat, "_AlphaClip", "_ALPHATEST_ON", "Alpha Clip");
            if (mat.IsKeywordEnabled("_ALPHATEST_ON"))
                DrawProp(materialEditor, properties, "_Cutoff", "Alpha Cutoff");
        });

        EditorGUILayout.Space(6);
        materialEditor.RenderQueueField();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DrawToggleKeyword(MaterialEditor editor, MaterialProperty[] props,
        Material mat, string propName, string keyword, string label)
    {
        MaterialProperty toggle = FindProperty(propName, props, false);
        if (toggle == null) return;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(toggle, label);
        if (EditorGUI.EndChangeCheck())
        {
            if (toggle.floatValue > 0.5f) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }
    }

    private static bool DrawSection(string title, bool foldout, Color accent, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), accent);
        Event e = Event.current;
        if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition)) { foldout = !foldout; e.Use(); }
        EditorGUI.LabelField(new Rect(headerRect.x + 16f, headerRect.y + 2f, headerRect.width - 16f, headerRect.height),
            (foldout ? "\u25BC " : "\u25BA ") + title, _headerStyle);
        if (foldout) { EditorGUILayout.BeginVertical(_sectionBox); drawContent(); EditorGUILayout.EndVertical(); }
        return foldout;
    }

    private static void DrawBanner(string text, Color color)
    {
        Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), color);
        Color old = GUI.contentColor; GUI.contentColor = color;
        EditorGUI.LabelField(r, text, _bannerStyle);
        GUI.contentColor = old;
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

    private static void DrawTextureProp(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName, string label)
    {
        MaterialProperty tex = FindProperty(texName, props, false);
        MaterialProperty col = FindProperty(colorName, props, false);
        if (tex != null && col != null) editor.TexturePropertySingleLine(new GUIContent(label), tex, col);
    }
}
