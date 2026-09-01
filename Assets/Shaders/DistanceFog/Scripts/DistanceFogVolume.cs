using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BillDev.DistanceFog
{
    [Serializable, VolumeComponentMenu("BillDev/Distance Fog")]
    public sealed class DistanceFogVolume : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter isActive = new BoolParameter(false);

        public ColorParameter fogColor = new ColorParameter(new Color(0.6f, 0.65f, 0.7f, 1f), hdr: false, showAlpha: false, showEyeDropper: true);
        public ClampedFloatParameter density = new ClampedFloatParameter(1.0f, 0f, 5f);
        public FloatParameter fogStart = new FloatParameter(50f);
        public FloatParameter fogEnd = new FloatParameter(500f);
        public ClampedFloatParameter skyFogAmount = new ClampedFloatParameter(0f, 0f, 1f);

        public bool IsActive() => isActive.value && density.value > 0.001f;
        public bool IsTileCompatible() => false;
    }
}
