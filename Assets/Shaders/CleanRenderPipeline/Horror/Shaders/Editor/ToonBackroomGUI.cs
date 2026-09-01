using UnityEditor;
using UnityEngine;

public class ToonBackroomGUI : ShaderGUI
{
    bool _fBase=true,_fNoise=true,_fDirt=true,_fWater=true,_fBlood=false,_fFlicker=false,_fCel=false,_fEmission=false,_fOpt=false;
    static GUIStyle _h,_b,_bn; static bool _si;
    static readonly Color AW=new Color(.72f,.68f,.5f),AD=new Color(.45f,.35f,.2f),AS=new Color(.5f,.45f,.35f),AB=new Color(.7f,.1f,.1f),AF=new Color(.9f,.8f,.5f);
    static void I(){if(_si)return;_si=true;_h=new GUIStyle(EditorStyles.boldLabel){fontSize=12,richText=true};_b=new GUIStyle(GUI.skin.box){padding=new RectOffset(10,10,6,6),margin=new RectOffset(0,0,2,4)};_bn=new GUIStyle(EditorStyles.boldLabel){fontSize=14,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};}

    public override void OnGUI(MaterialEditor me, MaterialProperty[] p)
    {
        I(); Material m=me.target as Material;
        EditorGUILayout.Space(4);Ban("\u26A0  TOON BACKROOM",AW);EditorGUILayout.Space(4);

        _fBase=S("Base — Triplanar",_fBase,AW,()=>{
            TC(me,p,"_BaseMap","_BaseColor","Wall Texture");
            P(me,p,"_BaseScale","Triplanar Scale");P(me,p,"_TriSharpness","Sharpness");
        });
        _fNoise=S("Noise Texture",_fNoise,AD,()=>{T(me,p,"_NoiseTex","Noise (drives all overlays)");});
        _fDirt=S("Dirt & Grime",_fDirt,AD,()=>{
            TK(me,p,m,"_EnableDirt","_DIRT_ON","Enable Dirt");
            if(m.IsKeywordEnabled("_DIRT_ON")){
                P(me,p,"_DirtColor","Dirt Color");P(me,p,"_DirtScale","Scale");P(me,p,"_DirtAmount","Amount");
                P(me,p,"_NormalStrength","Normal Strength");
            }
        });
        _fWater=S("Water Damage",_fWater,AS,()=>{
            TK(me,p,m,"_EnableWaterStain","_WATER_STAIN_ON","Enable Wall Stains");
            if(m.IsKeywordEnabled("_WATER_STAIN_ON")){
                P(me,p,"_StainColor","Stain Color");P(me,p,"_StainHeight","Height");
                P(me,p,"_StainSoftness","Softness");P(me,p,"_StainNoiseScale","Noise Scale");
            }
            EditorGUILayout.Space(2);
            TK(me,p,m,"_EnableCeilingStain","_CEILING_STAIN_ON","Enable Ceiling Stains");
            if(m.IsKeywordEnabled("_CEILING_STAIN_ON"))P(me,p,"_CeilingStainThreshold","Threshold");
        });
        _fBlood=S("Blood Overlay",_fBlood,AB,()=>{
            TK(me,p,m,"_EnableBlood","_BLOOD_ON","Enable Blood");
            if(m.IsKeywordEnabled("_BLOOD_ON")){
                P(me,p,"_BloodColor","Color");P(me,p,"_BloodScale","Scale");
                P(me,p,"_BloodThreshold","Threshold");P(me,p,"_BloodEdge","Edge");
            }
        });
        _fFlicker=S("Fluorescent Flicker",_fFlicker,AF,()=>{
            TK(me,p,m,"_EnableFlicker","_FLICKER_ON","Enable Flicker");
            if(m.IsKeywordEnabled("_FLICKER_ON")){
                P(me,p,"_FlickerColor","Color (HDR)");P(me,p,"_FlickerSpeed","Speed");
                T(me,p,"_FlickerMask","Mask (R)");
            }
        });
        _fCel=S("Cel Shading",_fCel,AW,()=>{
            P(me,p,"_ShadowColor","Shadow Color");P(me,p,"_Threshold","Threshold");P(me,p,"_Smoothness","Smoothness");
            EditorGUILayout.Space(2);P(me,p,"_RimColor","Rim (RGB+A)");P(me,p,"_RimPower","Rim Power");
        });
        _fEmission=S("Emission",_fEmission,AF,()=>{
            TK(me,p,m,"_Emission","_EMISSION","Enable");if(m.IsKeywordEnabled("_EMISSION"))P(me,p,"_EmissionColor","Color (HDR)");
        });
        _fOpt=S("Options",_fOpt,new Color(.5f,.5f,.5f),()=>{P(me,p,"_Cull","Cull");});
        EditorGUILayout.Space(6);me.RenderQueueField();
    }

    static void TK(MaterialEditor me,MaterialProperty[]p,Material m,string pn,string kw,string l){var t=FindProperty(pn,p,false);if(t==null)return;EditorGUI.BeginChangeCheck();me.ShaderProperty(t,l);if(EditorGUI.EndChangeCheck()){if(t.floatValue>.5f)m.EnableKeyword(kw);else m.DisableKeyword(kw);}}
    static bool S(string t,bool f,Color a,System.Action d){EditorGUILayout.Space(2);Rect h=GUILayoutUtility.GetRect(1f,22f,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(h,f?new Color(.25f,.25f,.25f,.6f):new Color(.2f,.2f,.2f,.3f));EditorGUI.DrawRect(new Rect(h.x,h.y,3f,h.height),a);Event e=Event.current;if(e.type==EventType.MouseDown&&h.Contains(e.mousePosition)){f=!f;e.Use();}EditorGUI.LabelField(new Rect(h.x+16f,h.y+2f,h.width-16f,h.height),(f?"\u25BC ":"\u25BA ")+t,_h);if(f){EditorGUILayout.BeginVertical(_b);d();EditorGUILayout.EndVertical();}return f;}
    static void Ban(string t,Color c){Rect r=GUILayoutUtility.GetRect(1f,28f,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(r,new Color(c.r*.3f,c.g*.3f,c.b*.3f,.8f));EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,2f),c);Color o=GUI.contentColor;GUI.contentColor=c;EditorGUI.LabelField(r,t,_bn);GUI.contentColor=o;}
    static void P(MaterialEditor me,MaterialProperty[]p,string n,string l){var x=FindProperty(n,p,false);if(x!=null)me.ShaderProperty(x,l);}
    static void T(MaterialEditor me,MaterialProperty[]p,string n,string l){var x=FindProperty(n,p,false);if(x!=null)me.TexturePropertySingleLine(new GUIContent(l),x);}
    static void TC(MaterialEditor me,MaterialProperty[]p,string tn,string cn,string l){var t=FindProperty(tn,p,false);var c=FindProperty(cn,p,false);if(t!=null&&c!=null)me.TexturePropertySingleLine(new GUIContent(l),t,c);}
}
