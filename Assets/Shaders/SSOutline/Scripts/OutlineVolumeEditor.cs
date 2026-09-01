#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BillDev.SSOutline
{
    [CustomEditor(typeof(OutlineVolume))]
    sealed class OutlineVolumeEditor : VolumeComponentEditor
    {
        SerializedDataParameter _isActive;
        SerializedDataParameter _mode;
        SerializedDataParameter _selectionLayer;
        SerializedDataParameter _occlusionLayer;
        SerializedDataParameter _outlineColor;
        SerializedDataParameter _thickness;
        SerializedDataParameter _outlineIntensity;
        SerializedDataParameter _useDepth;
        SerializedDataParameter _useNormals;
        SerializedDataParameter _depthThreshold;
        SerializedDataParameter _normalThreshold;
        SerializedDataParameter _depthViewBias;
        SerializedDataParameter _normalViewBias;
        SerializedDataParameter _fadeDistStart;
        SerializedDataParameter _fadeDistEnd;
        SerializedDataParameter _vrPeripheryFade;
        SerializedDataParameter _debugMode;

        bool _showMasking = true;
        bool _showAppearance = true;
        bool _showEdge = true;
        bool _showFading = false;
        bool _showVR = false;
        bool _showDebug = false;

        static readonly Color AccentColor = new Color(0.35f, 0.75f, 0.45f);
        static readonly Color VRColor = new Color(0.45f, 0.65f, 1.0f);
        static readonly Color DebugColor = new Color(0.95f, 0.75f, 0.25f);
        static readonly Color WarnColor = new Color(1f, 0.6f, 0.2f);

        public override void OnEnable()
        {
            var o = new PropertyFetcher<OutlineVolume>(serializedObject);
            _isActive = Unpack(o.Find(v => v.isActive));
            _mode = Unpack(o.Find(v => v.mode));
            _selectionLayer = Unpack(o.Find(v => v.selectionLayer));
            _occlusionLayer = Unpack(o.Find(v => v.occlusionLayer));
            _outlineColor = Unpack(o.Find(v => v.outlineColor));
            _thickness = Unpack(o.Find(v => v.thickness));
            _outlineIntensity = Unpack(o.Find(v => v.outlineIntensity));
            _useDepth = Unpack(o.Find(v => v.useDepth));
            _useNormals = Unpack(o.Find(v => v.useNormals));
            _depthThreshold = Unpack(o.Find(v => v.depthThreshold));
            _normalThreshold = Unpack(o.Find(v => v.normalThreshold));
            _depthViewBias = Unpack(o.Find(v => v.depthViewBias));
            _normalViewBias = Unpack(o.Find(v => v.normalViewBias));
            _fadeDistStart = Unpack(o.Find(v => v.fadeDistanceStart));
            _fadeDistEnd = Unpack(o.Find(v => v.fadeDistanceEnd));
            _vrPeripheryFade = Unpack(o.Find(v => v.vrPeripheryFade));
            _debugMode = Unpack(o.Find(v => v.debugMode));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(4);
            var accentRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(accentRect, AccentColor);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("BillDev SSOutline VR", EditorStyles.boldLabel);
                PropertyField(_isActive, new GUIContent("Active"));
            }
            EditorGUILayout.Space(6);

            DrawSection(ref _showMasking, "Mode & Masking", AccentColor, () =>
            {
                PropertyField(_mode);
                var mode = (OutlineVolume.OutlineMode)_mode.value.enumValueIndex;
                if (mode == OutlineVolume.OutlineMode.SelectionOnly || mode == OutlineVolume.OutlineMode.Mixed)
                {
                    EditorGUI.indentLevel++;
                    PropertyField(_selectionLayer);
                    EditorGUI.indentLevel--;

                    if (_selectionLayer.value.intValue != 0)
                        EditorGUILayout.HelpBox(
                            "Selection mask re-renders objects on this layer. Keep layer count minimal for VR.",
                            MessageType.Warning);
                }
                PropertyField(_occlusionLayer);
            });

            DrawSection(ref _showAppearance, "Appearance", AccentColor, () =>
            {
                PropertyField(_outlineColor);
                PropertyField(_thickness);
                PropertyField(_outlineIntensity);
            });

            DrawSection(ref _showEdge, "Edge Detection", AccentColor, () =>
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PropertyField(_useDepth);
                    PropertyField(_useNormals);
                }

                if (_useNormals.value.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Normals ON forces a DepthNormals prepass that re-renders ALL opaque geometry. " +
                        "For VR: keep OFF unless you need normal-based edges. Depth-only is much cheaper.",
                        MessageType.Warning);
                }

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledGroupScope(!_useDepth.value.boolValue))
                {
                    PropertyField(_depthThreshold);
                    PropertyField(_depthViewBias);
                }
                EditorGUILayout.Space(2);
                using (new EditorGUI.DisabledGroupScope(!_useNormals.value.boolValue))
                {
                    PropertyField(_normalThreshold);
                    PropertyField(_normalViewBias);
                }
            });

            DrawSection(ref _showFading, "Distance Fade", AccentColor, () =>
            {
                PropertyField(_fadeDistStart);
                PropertyField(_fadeDistEnd);
            });

            DrawSection(ref _showVR, "VR / XR", VRColor, () =>
            {
                PropertyField(_vrPeripheryFade);
                EditorGUILayout.HelpBox(
                    "Best VR setup: FullScreen mode, Depth only (normals OFF), Thickness 1-2, " +
                    "FadeEnd < 150. Half-res masks enabled in Feature inspector.",
                    MessageType.Info);
            });

            DrawSection(ref _showDebug, "Debug", DebugColor, () =>
            {
                PropertyField(_debugMode);
            });

            EditorGUILayout.Space(4);
        }

        static void DrawSection(ref bool foldout, string label, Color accent, System.Action draw)
        {
            var headerRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(
                new Rect(headerRect.x, headerRect.y + headerRect.height - 2, headerRect.width, 2),
                accent * 0.6f);
            foldout = EditorGUI.Foldout(headerRect, foldout, label, true, EditorStyles.foldoutHeader);

            if (foldout)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope()) draw();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }
        }
    }
}
#endif
