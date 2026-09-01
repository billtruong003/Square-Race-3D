using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BillDev.SSOutline
{
    [Serializable, VolumeComponentMenu("BillDev/Outline")]
    public sealed class OutlineVolume : VolumeComponent, IPostProcessComponent
    {
        public enum OutlineMode { FullScreen, SelectionOnly, Mixed }
        public enum DebugView { None, Depth, Normals, Scene, EdgeMask, SelectionMask, OcclusionMask }

        public BoolParameter isActive = new BoolParameter(false);

        public EnumParameter<OutlineMode> mode = new EnumParameter<OutlineMode>(OutlineMode.FullScreen);

        public LayerMaskParameter selectionLayer = new LayerMaskParameter(0);
        public LayerMaskParameter occlusionLayer = new LayerMaskParameter(0);

        public ColorParameter outlineColor = new ColorParameter(new Color(0f, 0f, 0f, 1f), hdr: false, showAlpha: true, showEyeDropper: true);
        public ClampedIntParameter thickness = new ClampedIntParameter(2, 1, 6);
        public ClampedFloatParameter outlineIntensity = new ClampedFloatParameter(1f, 0f, 1f);

        public BoolParameter useDepth = new BoolParameter(true);
        public BoolParameter useNormals = new BoolParameter(false);

        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(1.5f, 0f, 10f);
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.35f, 0f, 1f);

        public ClampedFloatParameter depthViewBias = new ClampedFloatParameter(0.85f, 0f, 1f);
        public ClampedFloatParameter normalViewBias = new ClampedFloatParameter(0.5f, 0f, 1f);

        public FloatParameter fadeDistanceStart = new FloatParameter(80f);
        public FloatParameter fadeDistanceEnd = new FloatParameter(150f);

        public ClampedFloatParameter vrPeripheryFade = new ClampedFloatParameter(0f, 0f, 3f);

        public EnumParameter<DebugView> debugMode = new EnumParameter<DebugView>(DebugView.None);

        public bool IsActive() => isActive.value;
        public bool IsTileCompatible() => false;
    }
}
