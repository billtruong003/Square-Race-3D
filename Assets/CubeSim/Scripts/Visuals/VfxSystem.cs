using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Plays pooled Epic Toon FX effects.
    ///
    /// Muzzle flashes, impacts and melee hits fire many times a second across ten racers, so every
    /// effect is pooled rather than instantiated and destroyed. Purely cosmetic: nothing here reads
    /// or writes simulation state, and it is driven by events the systems already raise.
    /// </summary>
    public sealed class VfxSystem
    {
        /// <summary>
        /// One pooled copy of an effect. The whole particle set is kept, not just the root system:
        /// pack effects are several sibling systems under one empty, and playing or scaling only the
        /// first one leaves most of the effect behind.
        /// </summary>
        private sealed class Instance
        {
            public Transform Root;
            public ParticleSystem[] Systems;
            public Color[] BaseColors;
        }

        private sealed class Pool
        {
            public VfxLibrary.Entry Entry;
            public readonly Stack<Instance> Idle = new Stack<Instance>();
            public readonly List<(Instance instance, float returnAt)> Live =
                new List<(Instance, float)>();
        }

        private readonly Dictionary<VfxId, Pool> _pools = new Dictionary<VfxId, Pool>();
        private readonly VfxLibrary _library;
        private readonly Transform _root;
        private readonly float _groundY;
        private float _time;

        public int Played { get; private set; }
        public bool Enabled => _library != null;

        public VfxSystem(VfxLibrary library, float groundY, Transform parent)
        {
            _library = library;
            _groundY = groundY;

            _root = new GameObject("Vfx").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>Plays an effect at a world position, optionally facing a direction.</summary>
        public void Play(VfxId id, Vector3 position, Vector3 direction = default, Color tint = default)
        {
            if (_library == null || id == VfxId.None) return;

            Pool pool = GetPool(id);
            if (pool?.Entry?.prefab == null) return;

            Instance instance = Rent(pool);
            if (instance == null) return;

            Transform t = instance.Root;
            t.position = new Vector3(position.x, _groundY + pool.Entry.heightOffset, position.z);
            t.rotation = direction.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up)
                : Quaternion.identity;

            t.localScale = Vector3.one * Mathf.Max(0.01f, pool.Entry.scale);

            ApplyTint(instance, tint, pool.Entry.tint);

            t.gameObject.SetActive(true);

            for (int i = 0; i < instance.Systems.Length; i++)
            {
                ParticleSystem system = instance.Systems[i];
                if (system == null) continue;

                system.Clear(false);
                system.Play(false);
            }

            pool.Live.Add((instance, _time + Mathf.Max(0.1f, pool.Entry.lifetime)));
            Played++;
        }

        /// <summary>Returns finished effects to their pool. Driven from the bootstrap, not the sim.</summary>
        public void Tick(float deltaTime)
        {
            _time += deltaTime;

            foreach (KeyValuePair<VfxId, Pool> pair in _pools)
            {
                if (pair.Value == null) continue;

                List<(Instance instance, float returnAt)> live = pair.Value.Live;
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    if (_time < live[i].returnAt) continue;

                    Instance instance = live[i].instance;
                    live.RemoveAt(i);

                    if (instance?.Root == null) continue;

                    for (int s = 0; s < instance.Systems.Length; s++)
                    {
                        ParticleSystem system = instance.Systems[s];
                        if (system == null) continue;

                        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }

                    instance.Root.gameObject.SetActive(false);
                    pair.Value.Idle.Push(instance);
                }
            }
        }

        private Pool GetPool(VfxId id)
        {
            if (_pools.TryGetValue(id, out Pool existing)) return existing;

            VfxLibrary.Entry entry = _library.Find(id);
            if (entry == null || entry.prefab == null)
            {
                _pools[id] = null;
                return null;
            }

            var pool = new Pool { Entry = entry };
            _pools[id] = pool;
            return pool;
        }

        private Instance Rent(Pool pool)
        {
            while (pool.Idle.Count > 0)
            {
                Instance pooled = pool.Idle.Pop();
                if (pooled?.Root != null) return pooled;
            }

            GameObject go = Object.Instantiate(pool.Entry.prefab, _root);
            go.name = pool.Entry.id.ToString();

            // Pack effects can carry lights and audio; both are noise at ten racers on screen.
            foreach (Light light in go.GetComponentsInChildren<Light>(true)) Object.Destroy(light);
            foreach (AudioSource audio in go.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(audio);

            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                Object.Destroy(go);
                return null;
            }

            var baseColors = new Color[systems.Length];

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;

                // The pack authors these in Local scaling mode, where a child system ignores its
                // parents entirely - so scaling the instance root did nothing and every effect stayed
                // pack-sized, which is a handful of pixels at this camera height.
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                baseColors[i] = main.startColor.color;
            }

            go.SetActive(false);
            return new Instance { Root = go.transform, Systems = systems, BaseColors = baseColors };
        }

        /// <summary>
        /// Tints an effect toward a racer's colour. Tinting reads from the captured pack colours
        /// rather than the current ones, so an instance that has already been tinted once does not
        /// drift further toward every colour it is reused for.
        /// </summary>
        private static void ApplyTint(Instance instance, Color tint, VfxTintMode mode)
        {
            bool restore = mode == VfxTintMode.None || tint.a <= 0f;

            for (int i = 0; i < instance.Systems.Length; i++)
            {
                ParticleSystem system = instance.Systems[i];
                if (system == null) continue;

                Color original = instance.BaseColors[i];
                Color result;

                if (restore)
                {
                    result = original;
                }
                else
                {
                    // Accent keeps some of the effect's own colour, which matters for effects whose
                    // read depends on their gradient rather than a flat hue.
                    result = mode == VfxTintMode.Full
                        ? new Color(tint.r, tint.g, tint.b, original.a)
                        : Color.Lerp(original, tint, 0.55f);

                    result.a = original.a;
                }

                ParticleSystem.MainModule main = system.main;
                main.startColor = result;
            }
        }
    }
}
