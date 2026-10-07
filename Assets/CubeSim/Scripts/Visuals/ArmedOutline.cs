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
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

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
            if (!Enabled || racer?.Visual == null || !Ready) return;
            if (!_tinted.TryGetValue(color, out Material material))
            {
                material = new Material(_outline) { name = "CubeSim_Outline_" + ColorUtility.ToHtmlStringRGB(color) };
                material.SetColor("_OutlineColor", color);
                _tinted[color] = material;
            }
            Apply(racer, material);
        }

        /// <summary>Master switch for the red armed rim. Off for review: the user finds it busy.</summary>
        public static bool Enabled = true;

        /// <summary>The stencil stamp, for cosmetics that must never get a rim drawn over them.</summary>
        public static Material MaskMaterial => Ready ? _mask : null;

        public static void Apply(Racer racer) { if (!Enabled || racer?.Visual == null || !Ready) return; Apply(racer, _outline); }

        private static void Apply(Racer racer, Material outline)
        {
            Remove(racer);

            var targets = new List<Renderer>();
            if (racer.Visual.Model != null) targets.AddRange(racer.Visual.Model.GetComponentsInChildren<Renderer>());
            for (int i = 0; i < targets.Count; i++) Attach(targets[i], outline, false);

            // The weapon itself gets no rim (it read as clutter); it only masks so the body's
            // rim does not paint across it.
            Transform held = racer.Visual.Anchor != null ? racer.Visual.Anchor.Held : null;
            if (held != null)
            {
                var parts = held.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < parts.Length; i++) { AttachMask(parts[i]); targets.Add(parts[i]); }
            }

            // The trail streams out of the cube's body: stamp it into the stencil too, so the rim
            // stops where the ribbon begins instead of being painted over it.
            if (racer.Visual.Trail != null)
            {
                if (racer.Visual.Trail.Renderer != null) { AttachMask(racer.Visual.Trail.Renderer); targets.Add(racer.Visual.Trail.Renderer); }
                if (racer.Visual.Trail.CapRenderer != null) { AttachMask(racer.Visual.Trail.CapRenderer); targets.Add(racer.Visual.Trail.CapRenderer); }
            }
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

        /// <summary>Mask only: the renderer blocks the rim but draws no rim of its own.</summary>
        private static void AttachMask(Renderer renderer)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) return;
            Material[] current = renderer.sharedMaterials;
            for (int i = 0; i < current.Length; i++) if (current[i] == _mask) return;
            var next = new Material[current.Length + 1];
            current.CopyTo(next, 0);
            next[current.Length] = _mask;
            renderer.sharedMaterials = next;
        }

        private static void Attach(Renderer renderer, Material outline, bool weapon)
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
            if (weapon)
            {
                // A gun is dozens of parts; scaling each about its own centre smeared the rim off
                // the barrel. Push along the (smoothed) normals instead: one even rim, no offset.
                renderer.GetPropertyBlock(_block);
                _block.SetVector(HullCenterId, b.center);
                _block.SetVector(HullFactorId, new Vector4(1f, 1f, 1f, 0f));
                _block.SetFloat(OutlineWidthId, WidthMetres * 0.9f);
                renderer.SetPropertyBlock(_block);
                return;
            }
            var factor = new Vector4(
                Factor(b.extents.x, scale.x), Factor(b.extents.y, scale.y), Factor(b.extents.z, scale.z), 0f);

            renderer.GetPropertyBlock(_block);
            _block.SetVector(HullCenterId, b.center);
            _block.SetVector(HullFactorId, factor);
            _block.SetFloat(OutlineWidthId, 0f);
            renderer.SetPropertyBlock(_block);
        }

        private static float Factor(float extent, float scale)
        {
            float world = Mathf.Abs(extent * scale);
            // A thin barrel must not grow into a slab: cap the growth so the outline hugs the shape.
            return world < 1e-4f ? 1f : Mathf.Min(1f + WidthMetres / world, 1.6f);
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
