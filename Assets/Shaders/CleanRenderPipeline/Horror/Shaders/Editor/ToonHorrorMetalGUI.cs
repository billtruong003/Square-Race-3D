using UnityEditor;
using UnityEngine;
public class ToonHorrorMetalGUI : ShaderGUI
{
    bool _fB = true, _fN = true, _fR = true, _fC = true, _fF = false, _fM = false, _fCl = false, _fO = false;
    static GUIStyle _h, _b, _bn; static bool _si;
    static readonly Color AR = new Color(.7f, .35f, .1f), AC = new Color(1f, .5f, .15f), AM = new Color(.5f, .48f, .45f);
    static void I() { if (_si) return; _si = true; _h = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true }; _b = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) }; _bn = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }; }
    public override void OnGUI(MaterialEditor me, MaterialProperty[] p)
    {
        I(); Material m = me.target as Material;
        EditorGUILayout.Space(4); Ban("\u2699  TOON HORROR METAL", AR); EditorGUILayout.Space(4);
        _fB = S("Base", _fB, AM, () => { TC(me, p, "_BaseMap", "_BaseColor", "Base"); P(me, p, "_BaseScale", "Scale"); P(me, p, "_TriSharpness", "Sharpness"); });
        _fN = S("Noise & Normal", _fN, AM, () => { T(me, p, "_NoiseTex", "Noise"); P(me, p, "_NormalStrength", "Strength"); });
        _fR = S("Rust", _fR, AR, () => { TK(me, p, m, "_EnableRust", "_RUST_ON", "Enable"); if (m.IsKeywordEnabled("_RUST_ON")) { P(me, p, "_RustColor", "Color"); P(me, p, "_RustColor2", "Deep"); P(me, p, "_RustThreshold", "Amount"); P(me, p, "_RustScale", "Scale"); P(me, p, "_EdgeWear", "Edge Wear"); P(me, p, "_EdgeSharpness", "Sharpness"); } });
        _fC = S("Cracks", _fC, AC, () => { TK(me, p, m, "_EnableCracks", "_CRACKS_ON", "Enable"); if (m.IsKeywordEnabled("_CRACKS_ON")) { P(me, p, "_CrackScale", "Scale"); P(me, p, "_CrackSharpness", "Width"); P(me, p, "_CrackGlow", "Glow(HDR)"); P(me, p, "_CrackPulseRate", "Pulse"); } });
        _fF = S("Flicker", _fF, AC, () => { TK(me, p, m, "_EnableFlicker", "_FLICKER_ON", "Enable"); if (m.IsKeywordEnabled("_FLICKER_ON")) { P(me, p, "_FlickerColor", "Color"); P(me, p, "_FlickerSpeed", "Speed"); T(me, p, "_FlickerMask", "Mask"); } });
        _fM = S("Metal", _fM, AM, () => { P(me, p, "_MetalColor", "Fresnel"); P(me, p, "_MetalCutoff", "Cutoff"); P(me, p, "_MetalSmoothness", "Smooth"); P(me, p, "_SpecColor", "Spec"); P(me, p, "_SpecCutoff", "SpecCut"); P(me, p, "_SpecSmoothness", "SpecSmooth"); });
        _fCl = S("Cel", _fCl, AM, () => { P(me, p, "_ShadowColor", "Shadow"); P(me, p, "_Threshold", "Thresh"); P(me, p, "_Smoothness", "Smooth"); P(me, p, "_RimColor", "Rim"); P(me, p, "_RimPower", "Power"); });
        _fO = S("Options", _fO, new Color(.5f, .5f, .5f), () => { P(me, p, "_Cull", "Cull"); });
        EditorGUILayout.Space(6); me.RenderQueueField();
    }
    static void TK(MaterialEditor me, MaterialProperty[] p, Material m, string pn, string kw, string l) { var t = FindProperty(pn, p, false); if (t == null) return; EditorGUI.BeginChangeCheck(); me.ShaderProperty(t, l); if (EditorGUI.EndChangeCheck()) { if (t.floatValue > .5f) m.EnableKeyword(kw); else m.DisableKeyword(kw); } }
    static bool S(string t, bool f, Color a, System.Action d) { EditorGUILayout.Space(2); Rect h = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(h, f ? new Color(.25f, .25f, .25f, .6f) : new Color(.2f, .2f, .2f, .3f)); EditorGUI.DrawRect(new Rect(h.x, h.y, 3f, h.height), a); Event e = Event.current; if (e.type == EventType.MouseDown && h.Contains(e.mousePosition)) { f = !f; e.Use(); } EditorGUI.LabelField(new Rect(h.x + 16f, h.y + 2f, h.width - 16f, h.height), (f ? "\u25BC " : "\u25BA ") + t, _h); if (f) { EditorGUILayout.BeginVertical(_b); d(); EditorGUILayout.EndVertical(); } return f; }
    static void Ban(string t, Color c) { Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(r, new Color(c.r * .3f, c.g * .3f, c.b * .3f, .8f)); EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), c); Color o = GUI.contentColor; GUI.contentColor = c; EditorGUI.LabelField(r, t, _bn); GUI.contentColor = o; }
    static void P(MaterialEditor me, MaterialProperty[] p, string n, string l) { var x = FindProperty(n, p, false); if (x != null) me.ShaderProperty(x, l); }
    static void T(MaterialEditor me, MaterialProperty[] p, string n, string l) { var x = FindProperty(n, p, false); if (x != null) me.TexturePropertySingleLine(new GUIContent(l), x); }
    static void TC(MaterialEditor me, MaterialProperty[] p, string tn, string cn, string l) { var t = FindProperty(tn, p, false); var c = FindProperty(cn, p, false); if (t != null && c != null) me.TexturePropertySingleLine(new GUIContent(l), t, c); }
}
