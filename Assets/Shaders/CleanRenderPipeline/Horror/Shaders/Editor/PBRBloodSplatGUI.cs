using UnityEditor;
using UnityEngine;

public class PBRBloodSplatGUI : ShaderGUI
{
    private bool _foldSurface = true, _foldPBR = true, _foldBlood = true;
    private bool _foldDrip = true, _foldPulse = false, _foldEmission = false, _foldOptions = false;

    private static GUIStyle _hdr, _box, _ban;
    private static bool _si;
    private static readonly Color A1 = new Color(0.8f, 0.1f, 0.1f);
    private static readonly Color A2 = new Color(0.6f, 0.05f, 0.05f);

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
        EditorGUILayout.Space(4); Banner("\u2620  PBR BLOOD SPLAT", A1); EditorGUILayout.Space(4);

        _foldSurface = Sec("Surface", _foldSurface, A1, () => { TexCol(me, p, "_BaseMap", "_BaseColor", "Base Map"); Tex(me, p, "_BumpMap", "Normal Map"); P(me, p, "_BumpScale", "Normal Scale"); });
        _foldPBR = Sec("PBR", _foldPBR, new Color(0.5f, 0.5f, 0.5f), () => { P(me, p, "_Metallic", "Metallic"); P(me, p, "_SmoothnessBase", "Smoothness"); Tex(me, p, "_OcclusionMap", "Occlusion (R)"); P(me, p, "_OcclusionStrength", "Occlusion Strength"); });
        _foldBlood = Sec("Blood", _foldBlood, A1, () =>
        {
            P(me, p, "_BloodColor", "Fresh Blood"); P(me, p, "_DriedBloodColor", "Dried Blood");
            Tex(me, p, "_BloodMask", "Blood Mask (R)"); Tex(me, p, "_NoiseTex", "Noise Texture");
            P(me, p, "_NoiseScale", "Noise Tiling"); P(me, p, "_BloodNormalStrength", "Blood Normal Str");
            EditorGUILayout.Space(2);
            P(me, p, "_BloodWetness", "Wetness"); P(me, p, "_BloodSmoothness", "Blood Smoothness");
            P(me, p, "_BloodEdgeNoise", "Edge Distortion"); P(me, p, "_BloodMetallic", "Blood Metallic");
        });
        _foldDrip = Sec("Drip", _foldDrip, A2, () =>
        {
            Tog(me, p, mat, "_EnableDrip", "_DRIP_ON", "Enable Drips");
            if (mat.IsKeywordEnabled("_DRIP_ON")) { P(me, p, "_DripSpeed", "Speed"); P(me, p, "_DripScale", "Scale"); }
        });
        _foldPulse = Sec("Pulse", _foldPulse, A1, () =>
        {
            Tog(me, p, mat, "_EnablePulse", "_PULSE_ON", "Enable Pulse");
            if (mat.IsKeywordEnabled("_PULSE_ON")) { P(me, p, "_PulseRate", "Rate"); P(me, p, "_PulseStrength", "Strength"); P(me, p, "_PulseEmission", "Emission (HDR)"); }
        });
        _foldEmission = Sec("Emission", _foldEmission, A1, () =>
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
