using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Combat;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Builds the handful of materials an episode needs, once, from a <see cref="VisualTheme"/>.
    /// Racers share a single material and vary their colour through a MaterialPropertyBlock, so a
    /// hundred racers still cost one material.
    /// </summary>
    public sealed class MaterialLibrary
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int EmissionToggleId = Shader.PropertyToID("_Emission");

        private readonly VisualTheme _theme;
        private readonly Shader _shader;
        private readonly List<Material> _owned = new List<Material>(8);
        private readonly Dictionary<string, Material> _zoneMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Material> _weaponMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Material> _tintedMaterials = new Dictionary<string, Material>();

        public Material Floor { get; }
        public Material Wall { get; }
        public Material Pressure { get; }
        public Material Racer { get; }

        /// <summary>
        /// Opaque, so it writes Depth and DepthNormals. That is what lets the project's screen-space
        /// outline see the trail at all - a transparent trail writes neither and gets no outline.
        /// </summary>
        public Material Trail { get; }

        public MaterialLibrary(VisualTheme theme)
        {
            _theme = theme;
            _shader = ResolveShader(theme.shaderName);

            Floor = Create("CubeSim_Floor", theme.floorColor, Color.black, 0f);
            Wall = Create("CubeSim_Wall", theme.wallColor, Color.black, 0f);
            Pressure = Create("CubeSim_Pressure", theme.pressureColor, theme.pressureColor, theme.pressureEmission);
            Racer = Create("CubeSim_Racer", Color.white, Color.white, theme.racerEmission);
            // The trail's own shader: it lifts its depth toward the camera so the full-screen
            // outline inks the ribbon's edge, which a 6cm step above the floor never triggers.
            Shader trailShader = Shader.Find("CubeSim/TrailLit");
            if (trailShader != null)
            {
                var trail = new Material(trailShader) { name = "CubeSim_Trail" };
                trail.SetColor("_BaseColor", Color.white);
                trail.SetColor("_EmissionColor", Color.white * 0.35f);
                _owned.Add(trail);
                Trail = trail;
            }
            else
            {
                Trail = Create("CubeSim_Trail", Color.white, Color.white, 0.35f);
            }
        }

        private static Shader ResolveShader(string preferred)
        {
            Shader shader = string.IsNullOrEmpty(preferred) ? null : Shader.Find(preferred);
            if (shader != null) return shader;

            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            Debug.LogWarning("[CubeSim] No suitable shader found; falling back to the error shader.");
            return Shader.Find("Hidden/InternalErrorShader");
        }

        private Material Create(string name, Color baseColor, Color emissionColor, float emissionIntensity)
        {
            var material = new Material(_shader) { name = name };
            ApplyColors(material, baseColor, emissionColor, emissionIntensity);
            material.SetColor(ShadowColorId, _theme.shadowColor);
            _owned.Add(material);
            return material;
        }

        private void ApplyColors(Material material, Color baseColor, Color emissionColor, float emissionIntensity)
        {
            material.SetColor(BaseColorId, baseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);

            if (emissionIntensity > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty(EmissionToggleId)) material.SetFloat(EmissionToggleId, 1f);
                material.SetColor(EmissionColorId, emissionColor * emissionIntensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.SetColor(EmissionColorId, Color.black);
            }
        }

        public Material GetZoneMaterial(ZoneDefinition zone)
        {
            if (_zoneMaterials.TryGetValue(zone.id, out Material existing)) return existing;

            Material material = Create("CubeSim_Zone_" + zone.id, zone.color, zone.color, zone.emission);
            _zoneMaterials.Add(zone.id, material);
            return material;
        }

        /// <summary>Weapons glow a little so they read as pickups from a top-down camera.</summary>
        public Material GetWeaponMaterial(WeaponDefinition weapon)
        {
            if (_weaponMaterials.TryGetValue(weapon.id, out Material existing)) return existing;

            Material material = Create("CubeSim_Weapon_" + weapon.id, weapon.color, weapon.color, 0.45f);
            _weaponMaterials.Add(weapon.id, material);
            return material;
        }

        /// <summary>Materials for goal dressing and breakable-wall accents, cached by key.</summary>
        public Material GetGoalMaterial(string key, Color color, float emission)
            => GetTinted("CubeSim_Goal_" + key, color, emission);

        public Material GetTinted(string key, Color color, float emission)
        {
            if (_tintedMaterials.TryGetValue(key, out Material existing)) return existing;

            Material material = Create(key, color, color, emission);
            _tintedMaterials.Add(key, material);
            return material;
        }

        /// <summary>Per-racer colour without a per-racer material.</summary>
        public void ApplyRacerColor(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, color * _theme.racerEmission);
            renderer.SetPropertyBlock(block);
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_owned[i]);
                else Object.DestroyImmediate(_owned[i]);
            }

            _owned.Clear();
            _zoneMaterials.Clear();
            _weaponMaterials.Clear();
            _tintedMaterials.Clear();
        }
    }
}
