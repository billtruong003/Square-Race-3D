using System.Collections.Generic;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// A health bar floating over every racer, camera-facing. Green to yellow to red by ratio,
    /// and the chunk that was just lost lingers as a white "ghost" for a beat before it drains -
    /// the fighting-game read of how hard a hit landed. Cosmetic: reads Racer.Health only.
    /// </summary>
    public sealed class HealthBarSystem
    {
        private sealed class Bar
        {
            public Racer Racer;
            public Transform Root;
            public Transform Fill;
            public Transform Ghost;
            public MeshRenderer FillRenderer;
            public float Shown;      // ratio the coloured fill shows
            public float GhostRatio; // ratio the white ghost still covers
            public float GhostHold;  // seconds before the ghost starts draining
            public float Shake;
            public float ShowFor;    // seconds the bar stays up after a hit
            public float Alpha;      // current fade
            public MeshRenderer BackRenderer, GhostRenderer;
        }

        private const float ShowSeconds = 2.4f;
        private const float FadeInPerSecond = 8f;
        private const float FadeOutPerSecond = 2.5f;

        private const float Width = 2.4f;
        private const float Height = 0.32f;
        private const float GhostHoldSeconds = 0.4f;
        private const float GhostDrainPerSecond = 1.6f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<Bar> _bars = new List<Bar>();
        private readonly Material _back;
        private readonly Material _ghost;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Transform _camera;

        public HealthBarSystem(Racer[] racers, Transform parent, Camera camera)
        {
            _camera = camera != null ? camera.transform : null;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            _back = Transparent(new Material(unlit));
            _ghost = Transparent(new Material(unlit));

            if (racers == null) return;
            for (int i = 0; i < racers.Length; i++) _bars.Add(Build(racers[i], parent, unlit));
        }

        private Bar Build(Racer racer, Transform parent, Shader unlit)
        {
            var root = new GameObject("HealthBar_" + racer.Id).transform;
            root.SetParent(parent, false);

            Transform back = Quad("Back", root, _back);
            back.localScale = new Vector3(Width + 0.12f, Height + 0.12f, 1f);
            back.localPosition = new Vector3(0f, 0f, 0.01f);
            // The bar's backing stamps the outline stencil: no red rim may paint over the bar.
            Material mask = ArmedOutline.MaskMaterial;
            if (mask != null) back.GetComponent<MeshRenderer>().sharedMaterials = new[] { _back, mask };

            Transform ghost = Quad("Ghost", root, _ghost);
            Transform fill = Quad("Fill", root, Transparent(new Material(unlit)));
            fill.localPosition = new Vector3(0f, 0f, -0.01f);

            var bar = new Bar
            {
                Racer = racer, Root = root, Fill = fill, Ghost = ghost,
                FillRenderer = fill.GetComponent<MeshRenderer>(),
                BackRenderer = back.GetComponent<MeshRenderer>(),
                GhostRenderer = ghost.GetComponent<MeshRenderer>(),
                Shown = 1f, GhostRatio = 1f, Alpha = 0f,
            };
            Layout(bar);
            root.gameObject.SetActive(false);
            return bar;
        }

        /// <summary>Alpha-blended so the bar can fade; colours come per bar through property blocks.</summary>
        private static Material Transparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        private static Transform Quad(string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _bars.Count; i++)
            {
                Bar bar = _bars[i];
                Racer racer = bar.Racer;
                if (racer == null || !racer.Alive || racer.MaxHealth <= 0f)
                {
                    bar.Root.gameObject.SetActive(false);
                    continue;
                }

                float ratio = Mathf.Clamp01(racer.Health / racer.MaxHealth);
                if (ratio < bar.Shown - 0.001f)
                {
                    // Lost health: the fill snaps, the ghost holds then drains.
                    bar.GhostHold = GhostHoldSeconds;
                    bar.Shake = 0.18f;
                    bar.ShowFor = ShowSeconds;   // a hit brings the bar up; it fades once the moment passes
                }
                else if (ratio > bar.Shown + 0.001f)
                {
                    bar.GhostRatio = ratio;   // a heal fills the ghost too
                }
                bar.Shown = ratio;

                if (bar.GhostHold > 0f) bar.GhostHold -= deltaTime;
                else bar.GhostRatio = Mathf.MoveTowards(bar.GhostRatio, ratio, GhostDrainPerSecond * deltaTime);
                if (bar.GhostRatio < ratio) bar.GhostRatio = ratio;
                if (bar.Shake > 0f) bar.Shake -= deltaTime;

                if (bar.ShowFor > 0f) { bar.ShowFor -= deltaTime; bar.Alpha = Mathf.MoveTowards(bar.Alpha, 1f, FadeInPerSecond * deltaTime); }
                else bar.Alpha = Mathf.MoveTowards(bar.Alpha, 0f, FadeOutPerSecond * deltaTime);
                if (bar.Alpha <= 0.001f) { bar.Root.gameObject.SetActive(false); continue; }

                Layout(bar);
            }
        }

        private void Layout(Bar bar)
        {
            Racer racer = bar.Racer;
            bar.Root.gameObject.SetActive(true);

            Vector3 jitter = bar.Shake > 0f
                ? new Vector3(Mathf.Sin(Time.unscaledTime * 90f) * 0.12f * bar.Shake / 0.18f, 0f, 0f)
                : Vector3.zero;
            // "Above" the cube as the top-down viewer sees it: just past its +Z edge, lying flat,
            // at the cube's mid height so it never floats over the face or hides the eyes.
            // Just above the top face: the trail ribbon rides at mid height, and a bar on the
            // same plane z-fought with it every time the cube turned toward the camera's "up".
            bar.Root.position = racer.Position + new Vector3(0f, racer.HalfExtent * 2f + 0.2f, racer.HalfExtent + 0.75f) + jitter;
            bar.Root.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            SetFill(bar.Fill, bar.Shown, -0.01f);
            SetFill(bar.Ghost, bar.GhostRatio, 0f);

            Color color = bar.Shown > 0.5f
                ? Color.Lerp(new Color(1f, 0.85f, 0.15f), new Color(0.2f, 0.95f, 0.3f), (bar.Shown - 0.5f) * 2f)
                : Color.Lerp(new Color(0.95f, 0.15f, 0.15f), new Color(1f, 0.85f, 0.15f), bar.Shown * 2f);
            color.a = bar.Alpha;
            Tint(bar.FillRenderer, color);
            Tint(bar.BackRenderer, new Color(0.05f, 0.05f, 0.07f, bar.Alpha * 0.9f));
            Tint(bar.GhostRenderer, new Color(1f, 1f, 1f, bar.Alpha));
        }

        private void Tint(MeshRenderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        private static void SetFill(Transform quad, float ratio, float z)
        {
            float w = Width * Mathf.Clamp01(ratio);
            quad.localScale = new Vector3(Mathf.Max(0.0001f, w), Height, 1f);
            quad.localPosition = new Vector3(-Width * 0.5f + w * 0.5f, 0f, z);
        }
    }
}
