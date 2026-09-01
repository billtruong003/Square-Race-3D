using UnityEditor;
using UnityEngine;

public class PBRHorrorMetalGUI : ShaderGUI
{
    private bool _foldSurface = true, _foldPBR = true, _foldRust = true, _foldCracks = true;
    private bool _foldFlicker = false, _foldEmission = false, _foldOptions = false;

    private static GUIStyle _hdr, _box, _ban;
    private static bool _si;
    private static readonly Color ARust = new Color(0.7f, 0.35f, 0.1f);
    private static readonly Color ACrack = new Color(1f, 0.5f, 0.15f);
    private static readonly Color AMetal = new Color(0.5f, 0.48f, 0.45f);

    static void Init()
    {
        if (_si) return; _si = true;
        _hdr = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true };
        _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) };
        _ban = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
    }

    public override void OnGUI(MaterialEditor me, MaterialProperty[] p)
    {
        Init(); Material mat = me.target as Material;
        EditorGUILayout.Space(4); Banner("\u2699  PBR HORROR METAL", ARust); EditorGUILayout.Space(4);

        _foldSurface = Sec("Surface", _foldSurface, AMetal, () =>
        {
            TexCol(me, p, "_BaseMap", "_BaseColor", "Base Map");
            Tex(me, p, "_BumpMap", "Normal Map"); P(me, p, "_BumpScale", "Normal Scale");
            Tex(me, p, "_MetallicGlossMap", "Metallic(R) Smooth(A)");
        });
        _foldPBR = Sec("PBR Base", _foldPBR, AMetal, () =>
        {
            P(me, p, "_Metallic", "Metallic"); P(me, p, "_SmoothnessBase", "Smoothness");
            Tex(me, p, "_OcclusionMap", "Occlusion (R)"); P(me, p, "_OcclusionStrength", "Occlusion Str");
        });
        _foldRust = Sec("Rust & Corrosion", _foldRust, ARust, () =>
        {
            Tog(me, p, mat, "_EnableRust", "_RUST_ON", "Enable Rust");
            if (mat.IsKeywordEnabled("_RUST_ON"))
            {
                Tex(me, p, "_NoiseTex", "Noise Texture");
                P(me, p, "_RustColor", "Rust Color"); P(me, p, "_RustColor2", "Deep Rust");
                P(me, p, "_RustThreshold", "Amount"); P(me, p, "_RustScale", "Scale");
                P(me, p, "_RustSmoothness", "Rust Smoothness"); P(me, p, "_RustMetallic", "Rust Metallic");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Edge Wear", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                P(me, p, "_EdgeWear", "Corrosion"); P(me, p, "_EdgeSharpness", "Sharpness");
                EditorGUI.indentLevel--;
            }
        });
        _foldCracks = Sec("Glowing Cracks", _foldCracks, ACrack, () =>
        {
            Tog(me, p, mat, "_EnableCracks", "_CRACKS_ON", "Enable Cracks");
            if (mat.IsKeywordEnabled("_CRACKS_ON"))
            {
                P(me, p, "_CrackScale", "Scale"); P(me, p, "_CrackSharpness", "Width");
                P(me, p, "_CrackGlow", "Glow (HDR)"); P(me, p, "_CrackPulseRate", "Pulse Rate");
            }
        });
        _foldFlicker = Sec("Warning Flicker", _foldFlicker, ACrack, () =>
        {
            Tog(me, p, mat, "_EnableFlicker", "_FLICKER_ON", "Enable Flicker");
            if (mat.IsKeywordEnabled("_FLICKER_ON"))
            {
                P(me, p, "_FlickerColor", "Color (HDR)"); P(me, p, "_FlickerSpeed", "Speed");
                Tex(me, p, "_FlickerMask", "Mask (R)");
            }
        });
        _foldEmission = Sec("Emission", _foldEmission, ACrack, () =>
        {
            Tog(me, p, mat, "_EnableEmission", "_EMISSION", "Enable Emission");
            if (mat.IsKeywordEnabled("_EMISSION")) { P(me, p, "_EmissionColor", "Color (HDR)"); Tex(me, p, "_EmissionMap", "Map"); }
        });
        _foldOptions = Sec("Options", _foldOptions, new Color(0.5f, 0.5f, 0.5f), () =>
        {
            P(me, p, "_Cull", "Cull"); Tog(me, p, mat, "_AlphaClip", "_ALPHATEST_ON", "Alpha Clip");
            if (mat.IsKeywordEnabled("_ALPHATEST_ON")) P(me, p, "_Cutoff", "Cutoff");
        });
        EditorGUILayout.Space(6); me.RenderQueueField();
    }

    static void Tog(MaterialEditor me, MaterialProperty[] p, Material m, string pn, string kw, string l) { var t = FindProperty(pn, p, false); if (t == null) return; EditorGUI.BeginChangeCheck(); me.ShaderProperty(t, l); if (EditorGUI.EndChangeCheck()) { if (t.floatValue > 0.5f) m.EnableKeyword(kw); else m.DisableKeyword(kw); } }
    static bool Sec(string t, bool f, Color a, System.Action d) { EditorGUILayout.Space(2); Rect h = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(h, f ? new Color(.25f, .25f, .25f, .6f) : new Color(.2f, .2f, .2f, .3f)); EditorGUI.DrawRect(new Rect(h.x, h.y, 3f, h.height), a); Event e = Event.current; if (e.type == EventType.MouseDown && h.Contains(e.mousePosition)) { f = !f; e.Use(); } EditorGUI.LabelField(new Rect(h.x + 16f, h.y + 2f, h.width - 16f, h.height), (f ? "\u25BC " : "\u25BA ") + t, _hdr); if (f) { EditorGUILayout.BeginVertical(_box); d(); EditorGUILayout.EndVertical(); } return f; }
    static void Banner(string t, Color c) { Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(r, new Color(c.r * .3f, c.g * .3f, c.b * .3f, .8f)); EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), c); Color o = GUI.contentColor; GUI.contentColor = c; EditorGUI.LabelField(r, t, _ban); GUI.contentColor = o; }
    static void P(MaterialEditor me, MaterialProperty[] p, string n, string l) { var x = FindProperty(n, p, false); if (x != null) me.ShaderProperty(x, l); }
    static void Tex(MaterialEditor me, MaterialProperty[] p, string n, string l) { var x = FindProperty(n, p, false); if (x != null) me.TexturePropertySingleLine(new GUIContent(l), x); }
    static void TexCol(MaterialEditor me, MaterialProperty[] p, string tn, string cn, string l) { var t = FindProperty(tn, p, false); var c = FindProperty(cn, p, false); if (t != null && c != null) me.TexturePropertySingleLine(new GUIContent(l), t, c); }
}
