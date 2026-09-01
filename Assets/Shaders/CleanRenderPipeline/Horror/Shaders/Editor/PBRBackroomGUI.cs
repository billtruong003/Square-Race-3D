using UnityEditor;
using UnityEngine;
public class PBRBackroomGUI : ShaderGUI
{
    bool _fB = true, _fP = true, _fN = true, _fD = true, _fW = true, _fBl = false, _fFl = false, _fEm = false, _fO = false;
    static GUIStyle _h, _b, _bn; static bool _si;
    static readonly Color AW = new Color(.72f, .68f, .5f), AD = new Color(.45f, .35f, .2f), AS = new Color(.5f, .45f, .35f), AB = new Color(.7f, .1f, .1f), AF = new Color(.9f, .8f, .5f);
    static void I() { if (_si) return; _si = true; _h = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true }; _b = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) }; _bn = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }; }
    public override void OnGUI(MaterialEditor me, MaterialProperty[] p)
    {
        I(); Material m = me.target as Material;
        EditorGUILayout.Space(4); Ban("\u26A0  PBR BACKROOM", AW); EditorGUILayout.Space(4);
        _fB = S("Surface", _fB, AW, () => { TC(me, p, "_BaseMap", "_BaseColor", "Texture"); T(me, p, "_BumpMap", "Normal"); P(me, p, "_BumpScale", "Nor Scale"); P(me, p, "_BaseScale", "Tri Scale"); P(me, p, "_TriSharpness", "Sharpness"); });
        _fP = S("PBR", _fP, new Color(.5f, .5f, .5f), () => { P(me, p, "_Metallic", "Metal"); P(me, p, "_SmoothnessBase", "Smooth"); T(me, p, "_OcclusionMap", "Occ"); P(me, p, "_OcclusionStrength", "Occ Str"); });
        _fN = S("Noise", _fN, AD, () => { T(me, p, "_NoiseTex", "Noise"); });
        _fD = S("Dirt", _fD, AD, () => { TK(me, p, m, "_EnableDirt", "_DIRT_ON", "Enable"); if (m.IsKeywordEnabled("_DIRT_ON")) { P(me, p, "_DirtColor", "Color"); P(me, p, "_DirtScale", "Scale"); P(me, p, "_DirtAmount", "Amount"); P(me, p, "_DirtRoughness", "Roughness"); } });
        _fW = S("Water", _fW, AS, () =>
        {
            TK(me, p, m, "_EnableWaterStain", "_WATER_STAIN_ON", "Wall Stains"); if (m.IsKeywordEnabled("_WATER_STAIN_ON")) { P(me, p, "_StainColor", "Color"); P(me, p, "_StainHeight", "Height"); P(me, p, "_StainSoftness", "Soft"); P(me, p, "_StainNoiseScale", "Scale"); P(me, p, "_StainSmoothness", "Smooth"); }
            TK(me, p, m, "_EnableCeilingStain", "_CEILING_STAIN_ON", "Ceiling"); if (m.IsKeywordEnabled("_CEILING_STAIN_ON")) P(me, p, "_CeilingStainThreshold", "Thresh");
        });
        _fBl = S("Blood", _fBl, AB, () => { TK(me, p, m, "_EnableBlood", "_BLOOD_ON", "Enable"); if (m.IsKeywordEnabled("_BLOOD_ON")) { P(me, p, "_BloodColor", "Color"); P(me, p, "_BloodScale", "Scale"); P(me, p, "_BloodThreshold", "Thresh"); P(me, p, "_BloodEdge", "Edge"); P(me, p, "_BloodSmoothness", "Smooth"); } });
        _fFl = S("Flicker", _fFl, AF, () => { TK(me, p, m, "_EnableFlicker", "_FLICKER_ON", "Enable"); if (m.IsKeywordEnabled("_FLICKER_ON")) { P(me, p, "_FlickerColor", "Color"); P(me, p, "_FlickerSpeed", "Speed"); T(me, p, "_FlickerMask", "Mask"); } });
        _fEm = S("Emission", _fEm, AF, () => { TK(me, p, m, "_EnableEmission", "_EMISSION", "Enable"); if (m.IsKeywordEnabled("_EMISSION")) P(me, p, "_EmissionColor", "Color"); });
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
