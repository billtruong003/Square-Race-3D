using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ToonTerrainGUI : ShaderGUI
{
    private bool _foldLayers = true;
    private bool _foldSplatMap = true;
    private bool _foldHoleMap = false;
    private bool _foldHeightBlend = true;
    private bool _foldTriplanar = false;
    private bool _foldTexScale = false;
    private bool _foldCelShading = true;
    private bool _foldShadowRendering = true;

    private static GUIStyle _headerStyle;
    private static GUIStyle _sectionBox;
    private static GUIStyle _bannerStyle;
    private static GUIStyle _infoBoxStyle;
    private static GUIStyle _channelStyle;
    private static GUIStyle _statusIconStyle;
    private static GUIStyle _statusDetailStyle;
    private static bool _stylesInit;

    private static readonly Color AccentTerrain = new Color(0.45f, 0.75f, 0.35f, 1f);
    private static readonly Color AccentSplat = new Color(0.55f, 0.65f, 0.95f, 1f);
    private static readonly Color AccentHole = new Color(0.95f, 0.55f, 0.45f, 1f);
    private static readonly Color WarnYellow = new Color(1f, 0.85f, 0.3f, 1f);
    private static readonly Color GoodGreen = new Color(0.4f, 0.9f, 0.4f, 1f);

    private static void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, richText = true };
        _sectionBox = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 4) };
        _bannerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        _infoBoxStyle = new GUIStyle(EditorStyles.helpBox) { richText = true, fontSize = 10, normal = { textColor = Color.white } };
        _channelStyle = new GUIStyle(EditorStyles.boldLabel) { fixedWidth = 20, normal = { textColor = Color.white } };
        _statusIconStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fixedWidth = 20, normal = { textColor = Color.white } };
        _statusDetailStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        InitStyles();
        Material mat = materialEditor.target as Material;

        EditorGUILayout.Space(4);
        DrawBanner("TOON TERRAIN", AccentTerrain);
        EditorGUILayout.Space(4);

        _foldLayers = DrawSection("Terrain Layers", _foldLayers, AccentTerrain, () =>
        {
            DrawLayerRow(materialEditor, properties, "_Layer0", "_Layer0Color", "Layer 0 — Low Ground");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer1", "_Layer1Color", "Layer 1 — Mid Ground");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer2", "_Layer2Color", "Layer 2 — High / Snow");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer3", "_Layer3Color", "Layer 3 — Cliff (Triplanar)");
        });

        _foldSplatMap = DrawSection("Splat Map", _foldSplatMap, AccentSplat, () => DrawSplatMapSection(materialEditor, properties, mat));
        _foldHoleMap = DrawSection("Hole Map", _foldHoleMap, AccentHole, () => DrawHoleMapSection(materialEditor, properties, mat));

        _foldHeightBlend = DrawSection("Height Blending", _foldHeightBlend, AccentTerrain, () =>
        {
            DrawProp(materialEditor, properties, "_HeightLow", "Low → Mid Height");
            DrawProp(materialEditor, properties, "_HeightMid", "Mid → High Height");
            DrawProp(materialEditor, properties, "_BlendSharpness", "Blend Sharpness");
            DrawProp(materialEditor, properties, "_HeightOffset", "Height Offset");

            if (mat.IsKeywordEnabled("_USE_SPLATMAP"))
            {
                EditorGUILayout.Space(2);
                DrawInfoBox("Splat Map is active", AccentSplat);
            }
        });

        _foldTriplanar = DrawSection("Triplanar Cliff", _foldTriplanar, AccentTerrain, () =>
        {
            DrawProp(materialEditor, properties, "_TriplanarScale", "Triplanar Scale");
            DrawProp(materialEditor, properties, "_TriplanarSharpness", "Blend Sharpness");
            DrawProp(materialEditor, properties, "_CliffAngle", "Cliff Angle Threshold");

            if (mat.IsKeywordEnabled("_USE_SPLATMAP"))
            {
                EditorGUILayout.Space(2);
                DrawInfoBox("Splat A channel overrides cliffs", AccentSplat);
            }
        });

        _foldTexScale = DrawSection("Texture Scale", _foldTexScale, AccentTerrain, () => DrawProp(materialEditor, properties, "_TexScale", "Global Texture Scale"));

        _foldCelShading = DrawSection("Cel Shading", _foldCelShading, AccentTerrain, () =>
        {
            DrawProp(materialEditor, properties, "_ShadowColor", "Shadow Color");
            DrawProp(materialEditor, properties, "_Threshold", "Shadow Threshold");
            DrawProp(materialEditor, properties, "_Smoothness", "Shadow Smoothness");
        });

        _foldShadowRendering = DrawSection("Shadow & Rendering", _foldShadowRendering, AccentTerrain, () => DrawShadowStatus(materialEditor));

        EditorGUILayout.Space(6);
        materialEditor.RenderQueueField();

        foreach (var obj in materialEditor.targets)
        {
            SyncHoleKeywords((Material)obj);
        }
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        SyncHoleKeywords(material);
    }

    public override void ValidateMaterial(Material material)
    {
        base.ValidateMaterial(material);
        SyncHoleKeywords(material);
    }

    private static void SyncHoleKeywords(Material material)
    {
        bool useHoleMap = material.IsKeywordEnabled("_USE_HOLEMAP");

        if (useHoleMap)
        {
            if (material.HasProperty("_HoleThreshold") && material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", material.GetFloat("_HoleThreshold"));
            }

            material.SetOverrideTag("RenderType", "TransparentCutout");

            if (!Application.isPlaying)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.Geometry - 100;
            }
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry - 100;
        }
    }

    private static void DrawSplatMapSection(MaterialEditor materialEditor, MaterialProperty[] properties, Material mat)
    {
        MaterialProperty useSplat = FindProperty("_UseSplatMap", properties, false);
        if (useSplat != null)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.ShaderProperty(useSplat, "Enable Splat Map");
            if (EditorGUI.EndChangeCheck())
            {
                if (useSplat.floatValue > 0.5f) mat.EnableKeyword("_USE_SPLATMAP");
                else mat.DisableKeyword("_USE_SPLATMAP");
            }
        }

        if (mat.IsKeywordEnabled("_USE_SPLATMAP"))
        {
            EditorGUILayout.Space(4);
            MaterialProperty splatTex = FindProperty("_SplatMap", properties, false);
            if (splatTex != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Splat Texture (RGBA)"), splatTex);
                materialEditor.TextureScaleOffsetProperty(splatTex);
            }

            EditorGUILayout.Space(4);
            DrawProp(materialEditor, properties, "_SplatInfluence", "Splat Influence");
            DrawProp(materialEditor, properties, "_SplatSharpness", "Splat Blend Sharpness");

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(_sectionBox);
            EditorGUILayout.LabelField("Channel Mapping", EditorStyles.miniLabel);
            DrawChannelRow("R", "Layer 0 — Low Ground", new Color(1f, 0.3f, 0.3f));
            DrawChannelRow("G", "Layer 1 — Mid Ground", new Color(0.3f, 1f, 0.3f));
            DrawChannelRow("B", "Layer 2 — High / Snow", new Color(0.3f, 0.5f, 1f));
            DrawChannelRow("A", "Layer 3 — Cliff Override", new Color(0.8f, 0.8f, 0.8f));
            EditorGUILayout.EndVertical();

            if (splatTex != null && splatTex.textureValue == null)
            {
                EditorGUILayout.Space(2);
                DrawInfoBox("No splat texture assigned", WarnYellow);
            }
        }
    }

    private static void DrawHoleMapSection(MaterialEditor materialEditor, MaterialProperty[] properties, Material mat)
    {
        MaterialProperty useHole = FindProperty("_UseHoleMap", properties, false);
        if (useHole != null)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.ShaderProperty(useHole, "Enable Hole Map");
            if (EditorGUI.EndChangeCheck())
            {
                if (useHole.floatValue > 0.5f) mat.EnableKeyword("_USE_HOLEMAP");
                else mat.DisableKeyword("_USE_HOLEMAP");
                SyncHoleKeywords(mat);
            }
        }

        if (mat.IsKeywordEnabled("_USE_HOLEMAP"))
        {
            EditorGUILayout.Space(4);
            MaterialProperty holeTex = FindProperty("_HoleMap", properties, false);
            if (holeTex != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Hole Texture (R channel)"), holeTex);
                materialEditor.TextureScaleOffsetProperty(holeTex);
            }

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            DrawProp(materialEditor, properties, "_HoleThreshold", "Hole Threshold");
            if (EditorGUI.EndChangeCheck()) SyncHoleKeywords(mat);

            DrawProp(materialEditor, properties, "_HoleEdgeSoftness", "Edge Softness");

            EditorGUILayout.Space(2);
            if (mat.IsKeywordEnabled("_ALPHATEST_ON")) DrawInfoBox("Baker alpha test active", GoodGreen);
            else DrawInfoBox("Baker keywords out of sync", WarnYellow);

            if (holeTex != null && holeTex.textureValue == null)
            {
                EditorGUILayout.Space(2);
                DrawInfoBox("No hole texture assigned", WarnYellow);
            }
        }
    }

    private static void DrawShadowStatus(MaterialEditor materialEditor)
    {
        Material mat = materialEditor.target as Material;
        DrawStatusRow("ShadowCaster Pass", mat.FindPass("ShadowCaster") >= 0, "Terrain can cast shadows");
        DrawStatusRow("DepthNormals Pass", mat.FindPass("DepthNormals") >= 0, "Required for SSAO");
        DrawStatusRow("Meta Pass", mat.FindPass("Meta") >= 0, "Required for GI bake");

        if (mat.IsKeywordEnabled("_USE_HOLEMAP"))
        {
            DrawStatusRow("Hole Map in Shadows", true, "Shadows respect hole cutouts");
            bool alphaTestOn = mat.IsKeywordEnabled("_ALPHATEST_ON");
            DrawStatusRow("Baker Alpha Test", alphaTestOn, alphaTestOn ? "Lightmap baker ready" : "AlphaTest missing");
        }

        EditorGUILayout.Space(4);
        var mainLight = FindMainDirectionalLight();
        if (mainLight != null)
        {
            bool lightCastsShadow = mainLight.shadows != LightShadows.None;
            DrawStatusRow("Directional Light", lightCastsShadow, lightCastsShadow ? $"Type: {mainLight.shadows}" : "Disabled");
            if (!lightCastsShadow && GUILayout.Button("Fix: Enable Soft Shadows", EditorStyles.miniButton))
            {
                Undo.RecordObject(mainLight, "Enable Shadows");
                mainLight.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(mainLight);
            }
        }

        var urpAsset = GraphicsSettings.currentRenderPipeline;
        if (urpAsset != null)
        {
            var so = new SerializedObject(urpAsset);
            var shadowProp = so.FindProperty("m_MainLightShadowsSupported");
            if (shadowProp != null)
            {
                bool urpShadows = shadowProp.boolValue;
                DrawStatusRow("URP Shadow Support", urpShadows, urpShadows ? "Active" : "Disabled");
                if (!urpShadows && GUILayout.Button("Fix: Enable URP Shadows", EditorStyles.miniButton))
                {
                    shadowProp.boolValue = true;
                    so.ApplyModifiedProperties();
                }
            }
            so.Dispose();
        }

        EditorGUILayout.Space(4);
        int rendererCount = 0;
        int castShadowCount = 0;
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == mat)
                {
                    rendererCount++;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) castShadowCount++;
                    break;
                }
            }
        }

        DrawStatusRow("Renderers using material", rendererCount > 0, $"{rendererCount} found");
        DrawStatusRow("Cast Shadows enabled", castShadowCount > 0, $"{castShadowCount}/{rendererCount} valid");

        if (castShadowCount < rendererCount && rendererCount > 0 && GUILayout.Button("Fix: Enable Cast Shadows", EditorStyles.miniButton))
        {
            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == mat)
                    {
                        Undo.RecordObject(r, "Enable Cast Shadows");
                        r.shadowCastingMode = ShadowCastingMode.On;
                        EditorUtility.SetDirty(r);
                        break;
                    }
                }
            }
        }
    }

    private static void DrawLayerRow(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName, string label)
    {
        MaterialProperty tex = FindProperty(texName, props, false);
        MaterialProperty col = FindProperty(colorName, props, false);
        if (tex != null && col != null)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            editor.TexturePropertySingleLine(new GUIContent("Texture"), tex, col);
        }
    }

    private static bool DrawSection(string title, bool foldout, Color accentColor, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), accentColor);

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

    private static void DrawInfoBox(string msg, Color color)
    {
        Color oldColor = GUI.contentColor;
        GUI.contentColor = color;
        EditorGUILayout.LabelField(msg, _infoBoxStyle);
        GUI.contentColor = oldColor;
    }

    private static void DrawChannelRow(string channel, string layerName, Color channelColor)
    {
        EditorGUILayout.BeginHorizontal();
        Color oldColor = GUI.contentColor;
        GUI.contentColor = channelColor;
        EditorGUILayout.LabelField(channel, _channelStyle, GUILayout.Width(20));
        GUI.contentColor = oldColor;
        EditorGUILayout.LabelField("→ " + layerName, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawStatusRow(string label, bool ok, string detail)
    {
        EditorGUILayout.BeginHorizontal();
        Color oldColor = GUI.contentColor;
        GUI.contentColor = ok ? GoodGreen : WarnYellow;
        EditorGUILayout.LabelField(ok ? "✓" : "⚠", _statusIconStyle, GUILayout.Width(20));
        GUI.contentColor = oldColor;
        EditorGUILayout.LabelField(label, GUILayout.Width(180));
        GUI.contentColor = ok ? Color.gray : WarnYellow;
        EditorGUILayout.LabelField(detail, _statusDetailStyle);
        GUI.contentColor = oldColor;
        EditorGUILayout.EndHorizontal();
    }

    private static Light FindMainDirectionalLight()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional) return light;
        }
        return null;
    }
}