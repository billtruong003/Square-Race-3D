using UnityEditor;
using UnityEngine;

public class ToonCrystalGUI : ShaderGUI
{
    private bool _foldSurface = true;
    private bool _foldInterior = true;
    private bool _foldMatcap = false;
    private bool _foldFresnel = true;
    private bool _foldSpecular = false;
    private bool _foldCel = true;
    private bool _foldRim = false;
    private bool _foldEmission = false;

    private static GUIStyle _headerStyle;
    private static GUIStyle _sectionBox;
    private static GUIStyle _bannerStyle;
    private static bool _stylesInit;

    private static readonly Color AccentCrystal = new Color(0.5f, 0.7f, 0.95f, 1f);
    private static readonly Color AccentInterior = new Color(0.4f, 0.5f, 0.85f, 1f);
    private static readonly Color AccentGlow = new Color(0.85f, 0.7f, 1f, 1f);

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
        DrawBanner("TOON CRYSTAL", AccentCrystal);
        EditorGUILayout.Space(4);

        _foldSurface = DrawSection("Surface", _foldSurface, AccentCrystal, () => DrawTextureProp(materialEditor, properties, "_BaseMap", "_BaseColor", "Surface Texture"));

        _foldInterior = DrawSection("Fake Interior", _foldInterior, AccentInterior, () =>
        {
            DrawTextureProp(materialEditor, properties, "_InteriorMap", "_InteriorColor", "Interior Texture");
            EditorGUILayout.Space(2);
            DrawProp(materialEditor, properties, "_InteriorDepth", "Parallax Depth");
            DrawProp(materialEditor, properties, "_InteriorBlend", "Interior Blend");
        });

        _foldMatcap = DrawSection("Matcap (Fake Reflection)", _foldMatcap, AccentCrystal, () =>
        {
            MaterialProperty toggle = FindProperty("_UseMatcap", properties, false);
            if (toggle != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(toggle, "Enable Matcap");
                if (EditorGUI.EndChangeCheck())
                {
                    if (toggle.floatValue > 0.5f) mat.EnableKeyword("_USE_MATCAP");
                    else mat.DisableKeyword("_USE_MATCAP");
                }
            }

            if (mat.IsKeywordEnabled("_USE_MATCAP"))
            {
                MaterialProperty matcapTex = FindProperty("_Matcap", properties, false);
                if (matcapTex != null) materialEditor.TexturePropertySingleLine(new GUIContent("Matcap Texture"), matcapTex);
                DrawProp(materialEditor, properties, "_MatcapStrength", "Matcap Strength");
            }
        });

        _foldFresnel = DrawSection("Fresnel Edge Glow", _foldFresnel, AccentGlow, () =>
        {
            DrawProp(materialEditor, properties, "_FresnelColor", "Fresnel Color");
            DrawProp(materialEditor, properties, "_FresnelPower", "Fresnel Power");
        });

        _foldSpecular = DrawSection("Specular Facets", _foldSpecular, AccentCrystal, () =>
        {
            DrawProp(materialEditor, properties, "_SpecColor", "Specular Color");
            DrawProp(materialEditor, properties, "_SpecCutoff", "Specular Cutoff");
            DrawProp(materialEditor, properties, "_SpecSmoothness", "Specular Smoothness");
        });

        _foldCel = DrawSection("Cel Shading", _foldCel, AccentCrystal, () =>
        {
            DrawProp(materialEditor, properties, "_ShadowColor", "Shadow Color");
            DrawProp(materialEditor, properties, "_Threshold", "Shadow Threshold");
            DrawProp(materialEditor, properties, "_Smoothness", "Shadow Smoothness");
        });

        _foldRim = DrawSection("Rim Light", _foldRim, AccentCrystal, () =>
        {
            DrawProp(materialEditor, properties, "_RimColor", "Rim Color (RGB + A)");
            DrawProp(materialEditor, properties, "_RimPower", "Rim Power");
        });

        _foldEmission = DrawSection("Emission", _foldEmission, AccentGlow, () =>
        {
            MaterialProperty toggle = FindProperty("_UseEmission", properties, false);
            if (toggle != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(toggle, "Enable Emission");
                if (EditorGUI.EndChangeCheck())
                {
                    if (toggle.floatValue > 0.5f) mat.EnableKeyword("_EMISSION");
                    else mat.DisableKeyword("_EMISSION");
                }
            }

            if (mat.IsKeywordEnabled("_EMISSION"))
            {
                DrawProp(materialEditor, properties, "_EmissionColor", "Emission Color (HDR)");
            }
        });

        EditorGUILayout.Space(6);
        materialEditor.RenderQueueField();
    }

    private static bool DrawSection(string title, bool foldout, Color accent, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), accent);

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

    private static void DrawTextureProp(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName, string label)
    {
        MaterialProperty tex = FindProperty(texName, props, false);
        MaterialProperty col = FindProperty(colorName, props, false);
        if (tex != null && col != null) editor.TexturePropertySingleLine(new GUIContent(label), tex, col);
    }
}