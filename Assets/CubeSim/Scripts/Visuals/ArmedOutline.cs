using System.Collections.Generic;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The red "this one is dangerous" rim. When a racer picks up a weapon, every renderer of its
    /// body and of the held weapon gets two extra materials appended: a stencil mask that stamps
    /// the full silhouette, then an expanded re-draw that is refused inside that stamp. What is
    /// left on screen is one even contour around the combined shape of racer plus knife. Dropping
    /// the weapon strips both materials again.
    ///
    /// Purely cosmetic: it touches renderer material lists and property blocks only, never the
    /// simulation. Two shared materials, per-renderer bounds fed through a property block so the
    /// rim is the same width in metres on a two-metre cube and on a knife blade.
    /// </summary>
    public static class ArmedOutline
    {
        private const float WidthMetres = 0.2f;

        private static readonly int HullCenterId = Shader.PropertyToID("_HullCenter");
        private static readonly int HullFactorId = Shader.PropertyToID("_HullFactor");

        private static Material _mask;
        private static Material _outline;
        private static readonly Dictionary<Racer, List<Renderer>> _applied = new Dictionary<Racer, List<Renderer>>();
        private static readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private static bool Ready
        {
            get
            {
                if (_mask != null && _outline != null) return true;
                Shader maskShader = Shader.Find("CubeSim/SilhouetteMask");
                Shader outlineShader = Shader.Find("CubeSim/SilhouetteOutline");
                if (maskShader == null || outlineShader == null) return false;
                _mask = new Material(maskShader) { name = "CubeSim_ArmedMask" };
                _outline = new Material(outlineShader) { name = "CubeSim_ArmedOutline" };
                return true;
            }
        }

        private static readonly Dictionary<Color, Material> _tinted = new Dictionary<Color, Material>();

        /// <summary>Same rim, another colour: infection green, shield blue.</summary>
        public static void Apply(Racer racer, Color color)
        {
            if (racer?.Visual == null || !Ready) return;
            if (!_tinted.TryGetValue(color, out Material material))
            {
                material = new Material(_outline) { name = "CubeSim_Outline_" + ColorUtility.ToHtmlStringRGB(color) };
                material.SetColor("_OutlineColor", color);
                _tinted[color] = material;
            }
            Apply(racer, material);
        }

        public static void Apply(Racer racer) { if (racer?.Visual == null || !Ready) return; Apply(racer, _outline); }

        private static void Apply(Racer racer, Material outline)
        {
            Remove(racer);

            var targets = new List<Renderer>();
            if (racer.Visual.Model != null) targets.AddRange(racer.Visual.Model.GetComponentsInChildren<Renderer>());

            Transform held = racer.Visual.Anchor != null ? racer.Visual.Anchor.Held : null;
            if (held != null) targets.AddRange(held.GetComponentsInChildren<Renderer>(true));

            for (int i = 0; i < targets.Count; i++) Attach(targets[i], outline);
            _applied[racer] = targets;
        }

        public static void Remove(Racer racer)
        {
            if (racer == null || !_applied.TryGetValue(racer, out List<Renderer> targets)) return;
            for (int i = 0; i < targets.Count; i++) if (targets[i] != null) Detach(targets[i]);
            _applied.Remove(racer);
        }

        public static void Clear() => _applied.Clear();

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static bool IsOutline(Material m) => m == _outline || (m != null && m.shader == _outline.shader);

        private static void Attach(Renderer renderer, Material outline)
        {
            Mesh mesh = MeshOf(renderer);
            if (mesh == null || renderer is TrailRenderer || renderer is LineRenderer || renderer is ParticleSystemRenderer) return;

            Material[] current = renderer.sharedMaterials;
            for (int i = 0; i < current.Length; i++) if (IsOutline(current[i])) return;

            var next = new Material[current.Length + 2];
            current.CopyTo(next, 0);
            next[current.Length] = _mask;
            next[current.Length + 1] = outline;
            renderer.sharedMaterials = next;

            Bounds b = mesh.bounds;
            Vector3 scale = renderer.transform.lossyScale;
            var factor = new Vector4(
                Factor(b.extents.x, scale.x), Factor(b.extents.y, scale.y), Factor(b.extents.z, scale.z), 0f);

            renderer.GetPropertyBlock(_block);
            _block.SetVector(HullCenterId, b.center);
            _block.SetVector(HullFactorId, factor);
            renderer.SetPropertyBlock(_block);
        }

        private static float Factor(float extent, float scale)
        {
            float world = Mathf.Abs(extent * scale);
            return world < 1e-4f ? 1f : 1f + WidthMetres / world;
        }

        private static void Detach(Renderer renderer)
        {
            Material[] current = renderer.sharedMaterials;
            var kept = new List<Material>(current.Length);
            for (int i = 0; i < current.Length; i++) if (current[i] != _mask && !IsOutline(current[i])) kept.Add(current[i]);
            if (kept.Count != current.Length) renderer.sharedMaterials = kept.ToArray();
        }
    }
}
