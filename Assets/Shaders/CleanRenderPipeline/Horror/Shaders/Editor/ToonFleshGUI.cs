using UnityEditor;
using UnityEngine;
public class ToonFleshGUI : ShaderGUI
{
    bool _fB = true, _fN = true, _fV = true, _fS = true, _fP = true, _fCl = false, _fO = false;
    static GUIStyle _h, _b, _bn; static bool _si;
    static readonly Color AF = new Color(.75f, .3f, .25f), AV = new Color(.5f, .1f, .2f), AS = new Color(.9f, .3f, .15f), AP = new Color(.7f, .15f, .1f);
    static void I() { if (_si) return; _si = true; _h = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true }; _b = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) }; _bn = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }; }
    public override void OnGUI(MaterialEditor me, MaterialProperty[] p)
    {
        I(); Material m = me.target as Material;
        EditorGUILayout.Space(4); Ban("\u2764  TOON FLESH", AF); EditorGUILayout.Space(4);
        _fB = S("Base", _fB, AF, () => { TC(me, p, "_BaseMap", "_BaseColor", "Base"); P(me, p, "_BaseScale", "Scale"); P(me, p, "_TriSharpness", "Sharpness"); });
        _fN = S("Noise", _fN, AF, () => { T(me, p, "_NoiseTex", "Noise"); P(me, p, "_NormalStrength", "Normal Str"); });
        _fV = S("Veins", _fV, AV, () => { TK(me, p, m, "_EnableVeins", "_VEINS_ON", "Enable"); if (m.IsKeywordEnabled("_VEINS_ON")) { P(me, p, "_VeinColor", "Color"); P(me, p, "_VeinScale", "Scale"); P(me, p, "_VeinThickness", "Thickness"); } });
        _fS = S("SSS", _fS, AS, () => { TK(me, p, m, "_EnableSSS", "_SSS_ON", "Enable"); if (m.IsKeywordEnabled("_SSS_ON")) { P(me, p, "_SSSColor", "Color"); P(me, p, "_SSSStrength", "Strength"); P(me, p, "_SSSDistortion", "Distortion"); } });
        _fP = S("Pulse", _fP, AP, () => { TK(me, p, m, "_EnablePulse", "_PULSE_ON", "Enable"); if (m.IsKeywordEnabled("_PULSE_ON")) { P(me, p, "_PulseSpeed", "Speed"); P(me, p, "_PulseScale", "Scale"); P(me, p, "_PulseAmplitude", "Displacement"); P(me, p, "_PulseEmission", "Em(HDR)"); P(me, p, "_PulseEmissionStrength", "Em Str"); } });
        _fCl = S("Cel", _fCl, AF, () => { P(me, p, "_ShadowColor", "Shadow"); P(me, p, "_Threshold", "Thresh"); P(me, p, "_Smoothness", "Smooth"); P(me, p, "_RimColor", "Rim"); P(me, p, "_RimPower", "Power"); });
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
