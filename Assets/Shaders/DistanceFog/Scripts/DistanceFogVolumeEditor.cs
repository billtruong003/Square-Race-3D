#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BillDev.DistanceFog
{
    [CustomEditor(typeof(DistanceFogVolume))]
    sealed class DistanceFogVolumeEditor : VolumeComponentEditor
    {
        SerializedDataParameter _isActive;
        SerializedDataParameter _fogColor;
        SerializedDataParameter _density;
        SerializedDataParameter _fogStart;
        SerializedDataParameter _fogEnd;
        SerializedDataParameter _skyFogAmount;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<DistanceFogVolume>(serializedObject);
            _isActive = Unpack(o.Find(v => v.isActive));
            _fogColor = Unpack(o.Find(v => v.fogColor));
            _density = Unpack(o.Find(v => v.density));
            _fogStart = Unpack(o.Find(v => v.fogStart));
            _fogEnd = Unpack(o.Find(v => v.fogEnd));
            _skyFogAmount = Unpack(o.Find(v => v.skyFogAmount));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(4);
            var accentRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(accentRect, new Color(0.4f, 0.6f, 0.9f));

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("BillDev Distance Fog", EditorStyles.boldLabel);
                PropertyField(_isActive, new GUIContent("Active"));
            }

            EditorGUILayout.Space(6);
            PropertyField(_fogColor);
            PropertyField(_density);

            EditorGUILayout.Space(4);
            PropertyField(_fogStart, new GUIContent("Near Distance"));
            PropertyField(_fogEnd, new GUIContent("Far Distance"));

            EditorGUILayout.Space(4);
            PropertyField(_skyFogAmount, new GUIContent("Sky Fog Amount"));

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Sky Fog Amount = 0 means skybox is untouched (recommended for stylized skybox). " +
                "Density controls how thick the fog is on geometry. " +
                "Near/Far controls fog range.",
                MessageType.Info);
        }
    }
}
#endif
