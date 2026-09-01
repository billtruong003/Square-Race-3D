using UnityEditor;
using UnityEngine;

public class PBRFleshGUI : ShaderGUI
{
    private bool _foldSurface = true, _foldPBR = true, _foldNoise = false, _foldVeins = true;
    private bool _foldSSS = true, _foldPulse = true, _foldEmission = false, _foldOptions = false;

    private static GUIStyle _hdr, _box, _ban;
    private static bool _si;
    private static readonly Color AF = new Color(0.75f, 0.3f, 0.25f);
    private static readonly Color AV = new Color(0.5f, 0.1f, 0.2f);
    private static readonly Color AS = new Color(0.9f, 0.3f, 0.15f);
    private static readonly Color AP = new Color(0.7f, 0.15f, 0.1f);

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
        EditorGUILayout.Space(4); Banner("\u2764  PBR FLESH", AF); EditorGUILayout.Space(4);

        _foldSurface = Sec("Surface", _foldSurface, AF, () =>
        {
            TexCol(me, p, "_BaseMap", "_BaseColor", "Base Map");
            Tex(me, p, "_BumpMap", "Normal Map"); P(me, p, "_BumpScale", "Normal Scale");
        });
        _foldPBR = Sec("PBR", _foldPBR, new Color(0.5f, 0.5f, 0.5f), () =>
        {
            P(me, p, "_Metallic", "Metallic"); P(me, p, "_SmoothnessBase", "Smoothness");
            Tex(me, p, "_OcclusionMap", "Occlusion (R)"); P(me, p, "_OcclusionStrength", "Occlusion Str");
        });
        _foldNoise = Sec("Noise", _foldNoise, AF, () =>
        {
            Tex(me, p, "_NoiseTex", "Noise Texture"); P(me, p, "_NoiseScale", "Noise Tiling");
        });
        _foldVeins = Sec("Veins", _foldVeins, AV, () =>
        {
            Tog(me, p, mat, "_EnableVeins", "_VEINS_ON", "Enable Veins");
            if (mat.IsKeywordEnabled("_VEINS_ON"))
            {
                P(me, p, "_VeinColor", "Color"); P(me, p, "_VeinScale", "Scale"); P(me, p, "_VeinThickness", "Thickness");
            }
        });
        _foldSSS = Sec("Subsurface Scattering", _foldSSS, AS, () =>
        {
            Tog(me, p, mat, "_EnableSSS", "_SSS_ON", "Enable Fake SSS");
            if (mat.IsKeywordEnabled("_SSS_ON"))
            {
                P(me, p, "_SSSColor", "SSS Color"); P(me, p, "_SSSStrength", "Strength"); P(me, p, "_SSSDistortion", "Distortion");
            }
        });
        _foldPulse = Sec("Pulse Animation", _foldPulse, AP, () =>
        {
            Tog(me, p, mat, "_EnablePulse", "_PULSE_ON", "Enable Pulsation");
            if (mat.IsKeywordEnabled("_PULSE_ON"))
            {
                P(me, p, "_PulseSpeed", "Speed"); P(me, p, "_PulseScale", "Noise Scale"); P(me, p, "_PulseAmplitude", "Displacement");
                EditorGUILayout.Space(2);
                P(me, p, "_PulseEmission", "Emission (HDR)"); P(me, p, "_PulseEmissionStrength", "Emission Str");
            }
        });
        _foldEmission = Sec("Emission", _foldEmission, AS, () =>
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
