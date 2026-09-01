using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Arena
{
    /// <summary>
    /// Counts impacts on breakable walls and opens them when their rule is satisfied.
    ///
    /// "Impact" means an actual cast hit reported by the mover, not proximity and not per-frame
    /// interpenetration. Each racer is then debounced per wall, so a racer skimming along a face
    /// registers one hit rather than one per step - which is what makes "100 hits" mean something.
    /// </summary>
    public sealed class BreakableWallSystem : PlanarMover.IContactListener
    {
        private sealed class Entry
        {
            public BreakableWall Wall;
            public Collider Collider;
            public Transform Transform;
            public MeshRenderer Renderer;
            public Rect Footprint;
            public Vector3 BaseScale;
            public Vector3 BasePosition;
            public Material Material;

            public int Target;
            public int Count;
            public TextMesh Counter;
            public bool Broken;
            public float BreakTimer;
            public float FlashTimer;

            public readonly HashSet<int> UniqueRacers = new HashSet<int>();
            public readonly Dictionary<int, float> LastContact = new Dictionary<int, float>();
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Entry> _entries = new List<Entry>(8);
        private readonly Dictionary<Collider, Entry> _byCollider = new Dictionary<Collider, Entry>(8);
        private readonly ArenaRuntime _arena;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Color _wallColor;

        private float _time;

        public int WallCount => _entries.Count;
        public int BrokenCount { get; private set; }
        public int RegisteredHits { get; private set; }

        /// <summary>(wall id, racer that landed the final hit)</summary>
        public event Action<string, Racer> OnWallBroken;

        /// <summary>(wall id, racer, hits remaining) - every impact that actually counted.</summary>
        public event Action<string, Racer, int> OnWallHit;

        public BreakableWallSystem(ArenaRuntime arena, MaterialLibrary materials, VisualTheme theme)
        {
            _arena = arena;
            _wallColor = theme.wallColor;

            if (arena.Authored == null) return;

            List<ArenaWall> walls = arena.Authored.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                ArenaWall wall = walls[i];
                if (wall == null) continue;

                var breakable = wall.GetComponent<BreakableWall>();
                if (breakable == null) continue;

                var collider = wall.GetComponent<Collider>();
                if (collider == null) continue;

                var entry = new Entry
                {
                    Wall = breakable,
                    Collider = collider,
                    Transform = wall.transform,
                    Renderer = wall.GetComponent<MeshRenderer>(),
                    Footprint = wall.ResolvedFootprint,
                    BaseScale = wall.transform.localScale,
                    BasePosition = wall.transform.localPosition,
                    Target = breakable.ResolveTarget()
                };

                // A colour-gated wall gets its own tinted material so the gate reads as one at a
                // glance; an accent override (rainbow gate layers) wins over both defaults.
                Color accent = breakable.AccentOverride.a > 0f
                    ? breakable.AccentOverride
                    : breakable.ShowAccentColor && breakable.IsColorGated
                        ? breakable.RequiredColor
                        : new Color(1f, 0.75f, 0.25f);

                float strength = breakable.AccentOverride.a > 0f ? 0.8f
                    : breakable.ShowAccentColor && breakable.IsColorGated ? 0.55f : 0.22f;

                entry.Material = materials.GetTinted("wall_break_" + breakable.Id,
                    Color.Lerp(_wallColor, accent, strength),
                    breakable.AccentOverride.a > 0f ? 0.5f : 0.15f);

                if (entry.Renderer != null) entry.Renderer.sharedMaterial = entry.Material;

                // The big countdown on the block face is the format: the number IS the tension.
                // Only multi-hit walls get one; a single-touch gate has nothing to count down.
                if (entry.Target > 1) entry.Counter = BuildCounter(entry, arena.Root);

                _entries.Add(entry);
                _byCollider[collider] = entry;
                ApplyFeedback(entry);
                UpdateCounter(entry);
            }
        }

        /// <summary>
        /// Called by the mover for every wall impact. Returns quickly for ordinary walls, which are
        /// the overwhelming majority of hits.
        /// </summary>
        public void ReportContact(Racer racer, Collider collider)
        {
            if (collider == null || racer == null) return;
            if (!_byCollider.TryGetValue(collider, out Entry entry)) return;
            if (entry.Broken) return;

            // Debounce: the same racer cannot re-count until it has been away for the cooldown.
            if (entry.LastContact.TryGetValue(racer.Index, out float last) &&
                _time - last < entry.Wall.ContactCooldownPerRacer)
            {
                entry.LastContact[racer.Index] = _time;
                return;
            }

            entry.LastContact[racer.Index] = _time;

            if (!Counts(entry, racer)) return;

            entry.Count++;
            RegisteredHits++;
            entry.FlashTimer = entry.Wall.HitFlashDuration;
            UpdateCounter(entry);
            OnWallHit?.Invoke(entry.Wall.Id, racer, Mathf.Max(0, entry.Target - entry.Count));

            if (entry.Count >= entry.Target) Break(entry, racer);
            else ApplyFeedback(entry);
        }

        /// <summary>Does this racer's impact contribute under this wall's rule?</summary>
        private static bool Counts(Entry entry, Racer racer)
        {
            BreakableWall wall = entry.Wall;

            if (wall.IsColorGated && !wall.ColorMatches(racer.Color)) return false;

            if (wall.CountsUniqueRacersOnly && !entry.UniqueRacers.Add(racer.Index)) return false;

            return true;
        }

        public void Step(float deltaTime)
        {
            _time += deltaTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];

                if (entry.FlashTimer > 0f)
                {
                    entry.FlashTimer -= deltaTime;
                    ApplyFeedback(entry);
                }

                if (!entry.Broken || entry.BreakTimer <= 0f) continue;

                entry.BreakTimer -= deltaTime;
                float t = Mathf.Clamp01(1f - entry.BreakTimer / entry.Wall.RemovalDuration);

                // Sink and shrink. The collider is already gone, so this is pure dressing.
                entry.Transform.localScale = Vector3.Lerp(entry.BaseScale,
                    new Vector3(entry.BaseScale.x * 0.85f, 0.02f, entry.BaseScale.z * 0.85f), t);

                entry.Transform.localPosition = entry.BasePosition -
                    new Vector3(0f, entry.BaseScale.y * t, 0f);

                if (entry.BreakTimer <= 0f) entry.Transform.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Opens the wall. The collider goes first and in the same step, so the new path is
        /// traversable immediately and no invisible blocker can survive the animation.
        /// </summary>
        private void Break(Entry entry, Racer racer)
        {
            entry.Broken = true;
            BrokenCount++;

            if (entry.Counter != null) entry.Counter.gameObject.SetActive(false);

            entry.Collider.enabled = false;

            // Drop it from the analytic footprint list too, or spawn and weapon-drop validation
            // would keep treating the opening as solid.
            _arena.RemoveWallRect(entry.Footprint);

            if (entry.Wall.RemovalMode == WallRemovalMode.Instant)
            {
                entry.Transform.gameObject.SetActive(false);
            }
            else
            {
                entry.BreakTimer = entry.Wall.RemovalDuration;
            }

            OnWallBroken?.Invoke(entry.Wall.Id, racer);
        }

        /// <summary>
        /// A flat number lying on top of the wall, facing the top-down camera - the "9999 HITS"
        /// read of the reference channel. TextMesh rather than a canvas: it lives in world space,
        /// scales with the wall, and needs no UI plumbing.
        /// </summary>
        private static TextMesh BuildCounter(Entry entry, Transform parent)
        {
            var go = new GameObject("BreakCounter_" + entry.Wall.Id);
            go.transform.SetParent(parent, false);

            Rect footprint = entry.Footprint;
            float wallTop = entry.BasePosition.y + entry.BaseScale.y * 0.5f;
            go.transform.localPosition = new Vector3(footprint.center.x, wallTop + 0.06f, footprint.center.y);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;

            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;

            // Size the digits to the wall face: roughly two thirds of the shorter side.
            float shorter = Mathf.Min(footprint.width, footprint.height);
            text.fontSize = 64;
            text.characterSize = shorter * 0.65f * 10f / 64f;

            return text;
        }

        private static void UpdateCounter(Entry entry)
        {
            if (entry.Counter == null) return;
            entry.Counter.text = Mathf.Max(0, entry.Target - entry.Count).ToString();
        }

        /// <summary>Tints the wall by how close it is to opening, plus a flash on each valid hit.</summary>
        private void ApplyFeedback(Entry entry)
        {
            if (entry.Renderer == null) return;

            BreakableWall wall = entry.Wall;
            Color accent = wall.IsColorGated ? wall.RequiredColor : new Color(1f, 0.72f, 0.2f);

            float progress = wall.ShowProgress && entry.Target > 1
                ? Mathf.Clamp01((float)entry.Count / entry.Target)
                : 0f;

            Color baseColor = Color.Lerp(_wallColor, accent, wall.ShowAccentColor ? 0.35f + progress * 0.5f : progress * 0.5f);
            float emission = 0.08f + progress * 0.6f;

            if (entry.FlashTimer > 0f)
            {
                float flash = entry.FlashTimer / Mathf.Max(0.01f, wall.HitFlashDuration);
                baseColor = Color.Lerp(baseColor, Color.white, flash * 0.7f);
                emission += flash * 1.2f;
            }

            entry.Renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, baseColor);
            _block.SetColor(EmissionColorId, accent * emission);
            entry.Renderer.SetPropertyBlock(_block);
        }

        /// <summary>Per-wall state for logs and validation.</summary>
        public string Describe()
        {
            if (_entries.Count == 0) return "none";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                sb.Append($"{e.Wall.Id}[{e.Wall.Condition} {e.Count}/{e.Target}{(e.Broken ? " BROKEN" : "")}] ");
            }

            return sb.ToString();
        }

        public bool IsBroken(string wallId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Wall.Id == wallId) return _entries[i].Broken;
            }

            return false;
        }
    }
}
