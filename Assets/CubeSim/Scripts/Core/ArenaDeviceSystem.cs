using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Runs every authored device on the map: saw blades, crushers, spike traps, bumpers,
    /// conveyors, key-and-gate, coins, potions and teleporters.
    ///
    /// Two passes per step. PreMove poses everything that moves and applies the forces that act
    /// on a racer before it travels (conveyor drag, boost expiry), so the mover and the constraint
    /// pass see the final geometry. PostMove evaluates the consequences of where racers ended up:
    /// cuts, spikes, pins, pickups, teleports. Everything is a pure function of elapsed time and
    /// racer state, iterated in a fixed order - no physics callbacks, no randomness.
    /// </summary>
    public sealed class ArenaDeviceSystem
    {
        private sealed class Saw
        {
            public SawBlade Blade;
            public Transform Root;
            public Transform Spin;
            public readonly Dictionary<int, float> LastCut = new Dictionary<int, float>();
        }

        private sealed class Slab
        {
            public Crusher Body;
            public Transform Root;
            public Vector3 HalfSize;
            public float LastProgress;
        }

        private sealed class Spikes
        {
            public SpikeTrap Trap;
            public Transform[] Visuals;
            public SpikeState State = SpikeState.Idle;
            public readonly Dictionary<int, float> LastHit = new Dictionary<int, float>();
        }

        private sealed class Bump
        {
            public Bumper Pad;
            public Transform Root;
            public Transform Visual;
            public float SquashUntil;
            public readonly Dictionary<int, float> LastBump = new Dictionary<int, float>();
        }

        private sealed class Gate
        {
            public LockedGate Lock;
            public Transform Root;
            public Collider Collider;
            public Vector3 ClosedScale;
            public Vector3 ClosedPosition;
            public bool Open;
        }

        private sealed class Pickup
        {
            public Transform Root;
            public Transform Visual;
            public bool Taken;
            public float BackAt = -1f;
            public float Radius;
            public string Id;
            public int Value;
            public float Heal;
            public float Respawn;
        }

        private readonly List<Saw> _saws = new List<Saw>();
        private readonly List<Slab> _slabs = new List<Slab>();
        private readonly List<Spikes> _spikes = new List<Spikes>();
        private readonly List<Bump> _bumps = new List<Bump>();
        private readonly List<ConveyorArea> _conveyors = new List<ConveyorArea>();
        private readonly List<Material> _beltMaterials = new List<Material>();
        private readonly List<Gate> _gates = new List<Gate>();
        private readonly List<Pickup> _keys = new List<Pickup>();
        private readonly List<Pickup> _coins = new List<Pickup>();
        private readonly List<Pickup> _potions = new List<Pickup>();
        private readonly List<Teleporter> _pads = new List<Teleporter>();
        private static readonly MaterialPropertyBlock _beltBlock = new MaterialPropertyBlock();
        private static readonly int ScrollDirId = Shader.PropertyToID("_ScrollDir");
        private static readonly int ScrollDistId = Shader.PropertyToID("_ScrollDist");
        private static readonly int AngleId = Shader.PropertyToID("_Angle");
        private static readonly int MetresPerRepeatId = Shader.PropertyToID("_MetresPerRepeat");
        private readonly Dictionary<int, float> _lastTeleport = new Dictionary<int, float>();
        private readonly float _groundY;

        public bool Any => _saws.Count + _slabs.Count + _spikes.Count + _bumps.Count + _conveyors.Count +
                           _gates.Count + _keys.Count + _coins.Count + _potions.Count + _pads.Count > 0;

        public int SawCount => _saws.Count;
        public int CrusherCount => _slabs.Count;
        public int CoinCount => _coins.Count;

        /// <summary>(racer, position) - a coin was taken.</summary>
        public event Action<Racer, Vector3> OnCoin;
        public event Action<Racer, Vector3> OnKey;
        public event Action<string> OnGateOpened;
        public event Action<Racer, Vector3> OnPotion;
        public event Action<Racer, Vector3> OnBump;
        public event Action<Racer, Vector3, Vector3> OnTeleport;
        public event Action<Racer, Vector3> OnSawCut;
        public event Action<Racer, Vector3> OnSpike;
        /// <summary>A crusher just hit the end of its stroke.</summary>
        public event Action<Vector3> OnCrusherSlam;
        /// <summary>A spike trap just switched to its warning tell.</summary>
        public event Action<Vector3> OnSpikeWarn;

        public ArenaDeviceSystem(ArenaRuntime arena)
        {
            _groundY = arena.GroundY;
            if (arena.Authored == null) return;
            Transform root = arena.Authored.transform;

            foreach (SawBlade blade in root.GetComponentsInChildren<SawBlade>(true))
            {
                _saws.Add(new Saw { Blade = blade, Root = blade.transform, Spin = blade.transform.Find("Spin") });
            }

            foreach (Crusher body in root.GetComponentsInChildren<Crusher>(true))
            {
                _slabs.Add(new Slab { Body = body, Root = body.transform, HalfSize = body.transform.lossyScale * 0.5f });
            }

            foreach (SpikeTrap trap in root.GetComponentsInChildren<SpikeTrap>(true))
            {
                var visuals = new List<Transform>();
                foreach (Transform child in trap.transform)
                {
                    Transform spike = child.Find("Spike");
                    if (spike != null) visuals.Add(spike);
                }

                _spikes.Add(new Spikes { Trap = trap, Visuals = visuals.ToArray() });
            }

            foreach (Bumper pad in root.GetComponentsInChildren<Bumper>(true))
            {
                _bumps.Add(new Bump { Pad = pad, Root = pad.transform, Visual = pad.transform.Find("Visual") });
            }

            _conveyors.AddRange(root.GetComponentsInChildren<ConveyorArea>(true));
            for (int i = 0; i < _conveyors.Count; i++)
            {
                // The planar shader takes its orientation from the belt: pattern turned to face the
                // drag direction, scroll along it. Per-renderer property block, so the material stays
                // shared and nothing is instanced.
                Renderer plate = _conveyors[i].Plate;
                if (plate != null)
                {
                    Vector3 dir = _conveyors[i].Direction;
                    plate.GetPropertyBlock(_beltBlock);
                    _beltBlock.SetVector(ScrollDirId, new Vector4(dir.x, dir.z, 0f, 0f));
                    _beltBlock.SetFloat(AngleId, Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg);
                    _beltBlock.SetFloat(MetresPerRepeatId, _conveyors[i].TileLength);
                    _beltBlock.SetFloat(ScrollDistId, 0f);
                    plate.SetPropertyBlock(_beltBlock);
                }
                _beltMaterials.Add(null);
            }

            foreach (LockedGate gate in root.GetComponentsInChildren<LockedGate>(true))
            {
                _gates.Add(new Gate
                {
                    Lock = gate,
                    Root = gate.transform,
                    Collider = gate.GetComponent<Collider>(),
                    ClosedScale = gate.transform.localScale,
                    ClosedPosition = gate.transform.localPosition,
                });
            }

            foreach (KeyPickup key in root.GetComponentsInChildren<KeyPickup>(true))
            {
                _keys.Add(new Pickup { Root = key.transform, Visual = key.transform.Find("Visual"), Radius = key.Radius, Id = key.GateId });
            }

            foreach (CoinPickup coin in root.GetComponentsInChildren<CoinPickup>(true))
            {
                _coins.Add(new Pickup
                {
                    Root = coin.transform, Visual = coin.transform.Find("Visual"), Radius = coin.Radius,
                    Value = coin.Value, Respawn = coin.RespawnDelay,
                });
            }

            foreach (PotionPickup potion in root.GetComponentsInChildren<PotionPickup>(true))
            {
                _potions.Add(new Pickup
                {
                    Root = potion.transform, Visual = potion.transform.Find("Visual"), Radius = potion.Radius,
                    Heal = potion.Heal, Respawn = potion.RespawnDelay,
                });
            }

            _pads.AddRange(root.GetComponentsInChildren<Teleporter>(true));
        }

        // ------------------------------------------------------------------ before movement

        public void PreMove(float elapsed, float deltaTime, Racer[] racers, ArenaRuntime arena,
            Action<Racer, DeathCause> kill)
        {
            if (!Any) return;

            bool movedColliders = false;

            for (int i = 0; i < _saws.Count; i++)
            {
                Saw saw = _saws[i];
                if (saw.Blade.OnRail)
                {
                    Vector3 p = saw.Blade.PositionAt(elapsed);
                    saw.Root.localPosition = new Vector3(p.x, saw.Root.localPosition.y, p.z);
                }

                if (saw.Spin != null) saw.Spin.localRotation = Quaternion.Euler(0f, elapsed * saw.Blade.DegreesPerSecond, 0f);
            }

            for (int i = 0; i < _slabs.Count; i++)
            {
                Slab slab = _slabs[i];
                float progress = slab.Body.Progress(elapsed);
                Vector3 p = slab.Body.RestPosition + slab.Body.Travel * progress;
                slab.Root.localPosition = new Vector3(p.x, slab.Root.localPosition.y, p.z);
                if (slab.LastProgress < 0.97f && progress >= 0.97f) OnCrusherSlam?.Invoke(slab.Root.position);
                slab.LastProgress = progress;
                movedColliders = true;
            }

            // The pin test has to run here, while the slab genuinely overlaps whoever it just
            // moved into: once the mover and the constraint pass have run, the racer has already
            // been shoved sideways out of the slab and nothing is left to detect.
            if (_slabs.Count > 0)
            {
                for (int i = 0; i < racers.Length; i++)
                {
                    Racer racer = racers[i];
                    if (!racer.IsActive) continue;
                    StepCrushers(racer, arena, kill);
                }
            }

            for (int i = 0; i < _spikes.Count; i++)
            {
                Spikes s = _spikes[i];
                SpikeState next = s.Trap.StateAt(elapsed);
                if (next == SpikeState.Warning && s.State != SpikeState.Warning) OnSpikeWarn?.Invoke(s.Trap.Center);
                s.State = next;
                float height = s.State == SpikeState.Up ? 1f : s.State == SpikeState.Warning ? 0.25f : 0.04f;
                for (int v = 0; v < s.Visuals.Length; v++)
                {
                    Vector3 scale = s.Visuals[v].localScale;
                    s.Visuals[v].localScale = new Vector3(scale.x, height, scale.z);
                }
            }

            for (int i = 0; i < _bumps.Count; i++)
            {
                Bump b = _bumps[i];
                if (b.Visual == null) continue;
                float squash = elapsed < b.SquashUntil ? 0.75f : 1f;
                b.Visual.localScale = new Vector3(2f - squash, squash, 2f - squash);
            }

            for (int i = 0; i < _conveyors.Count; i++)
            {
                Renderer plate = _conveyors[i].Plate;
                if (plate == null) continue;
                plate.GetPropertyBlock(_beltBlock);
                _beltBlock.SetFloat(ScrollDistId, elapsed * _conveyors[i].Speed);
                plate.SetPropertyBlock(_beltBlock);
            }

            SpinPickups(_keys, elapsed, 90f);
            SpinPickups(_coins, elapsed, 180f);
            SpinPickups(_potions, elapsed, 60f);
            Respawn(_coins, elapsed);
            Respawn(_potions, elapsed);

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive) continue;

                if (racer.BoostUntil > 0f && elapsed >= racer.BoostUntil)
                {
                    racer.Speed = racer.BaseSpeed;
                    racer.BoostUntil = 0f;
                }

                for (int c = 0; c < _conveyors.Count; c++)
                {
                    ConveyorArea belt = _conveyors[c];
                    if (!belt.Contains(racer.Position)) continue;
                    racer.Position += belt.Direction * (belt.Speed * deltaTime);
                    break;
                }
            }

            if (movedColliders) Physics.SyncTransforms();
        }

        private static void SpinPickups(List<Pickup> list, float elapsed, float degreesPerSecond)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Pickup p = list[i];
                if (p.Taken || p.Visual == null) continue;
                p.Visual.localRotation = Quaternion.Euler(0f, elapsed * degreesPerSecond, 0f);
                float bob = Mathf.Sin(elapsed * 3f + i) * 0.12f;
                Vector3 lp = p.Visual.localPosition;
                p.Visual.localPosition = new Vector3(lp.x, 0.6f + bob, lp.z);
            }
        }

        private static void Respawn(List<Pickup> list, float elapsed)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Pickup p = list[i];
                if (!p.Taken || p.BackAt < 0f || elapsed < p.BackAt) continue;
                p.Taken = false;
                p.BackAt = -1f;
                if (p.Visual != null) p.Visual.gameObject.SetActive(true);
            }
        }

        // ------------------------------------------------------------------ after movement

        public void PostMove(float elapsed, float deltaTime, Racer[] racers, ArenaRuntime arena,
            Action<Racer, Racer, float, DeathCause> damage, Action<Racer, DeathCause> kill)
        {
            if (!Any) return;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive) continue;

                StepSaws(racer, elapsed, damage);
                if (!racer.Alive) continue;

                StepSpikes(racer, elapsed, damage);
                if (!racer.Alive) continue;

                StepBumpers(racer, elapsed);
                StepKeys(racer, elapsed);
                StepCoins(racer, elapsed);
                StepPotions(racer, elapsed);
                StepTeleporters(racer, elapsed);
            }
        }

        private static bool Within(Vector3 a, Vector3 b, float reach)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz <= reach * reach;
        }

        private void StepSaws(Racer racer, float elapsed, Action<Racer, Racer, float, DeathCause> damage)
        {
            for (int s = 0; s < _saws.Count; s++)
            {
                Saw saw = _saws[s];
                if (saw.Blade.DamagePerHit <= 0f) continue;
                if (!Within(racer.Position, saw.Root.position, saw.Blade.Radius + racer.Radius * 0.6f)) continue;

                if (saw.LastCut.TryGetValue(racer.Index, out float last) && elapsed - last < saw.Blade.HitCooldown) continue;
                saw.LastCut[racer.Index] = elapsed;

                OnSawCut?.Invoke(racer, racer.Position);
                damage(racer, null, saw.Blade.DamagePerHit, DeathCause.Hazard);
                if (!racer.Alive) return;
            }
        }

        private void StepCrushers(Racer racer, ArenaRuntime arena, Action<Racer, DeathCause> kill)
        {
            for (int s = 0; s < _slabs.Count; s++)
            {
                Slab slab = _slabs[s];
                Vector3 c = slab.Root.position;
                float ox = slab.HalfSize.x + racer.HalfExtent - Mathf.Abs(racer.Position.x - c.x);
                float oz = slab.HalfSize.z + racer.HalfExtent - Mathf.Abs(racer.Position.z - c.z);
                Vector3 dir = slab.Body.TravelDirection;
                if (dir.sqrMagnitude < 1e-6f) dir = racer.Position - c;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
                dir.Normalize();

                // The slab advances a few centimetres per step, so the overlap along its travel is
                // always shallow - any bite there counts. Only a graze along the side (the racer
                // half off the slab's face) is left to the mover to slide past.
                bool alongX = Mathf.Abs(dir.x) >= Mathf.Abs(dir.z);
                float travelOverlap = alongX ? ox : oz;
                float lateralOverlap = alongX ? oz : ox;
                if (travelOverlap <= 0.005f || lateralOverlap <= racer.HalfExtent * 0.35f) continue;

                // Shove the racer out ahead of the slab. If that spot is wall, it is pinned.
                float push = travelOverlap;
                Vector3 candidate = racer.Position + dir * (push + 0.05f);
                var flat = new Vector2(candidate.x, candidate.z);

                if (arena.OverlapsWall(flat, racer.HalfExtent) || !arena.InsidePlayable(flat, racer.HalfExtent))
                {
                    kill(racer, DeathCause.Crushed);
                    return;
                }

                racer.Position = candidate;
            }
        }

        private void StepSpikes(Racer racer, float elapsed, Action<Racer, Racer, float, DeathCause> damage)
        {
            for (int s = 0; s < _spikes.Count; s++)
            {
                Spikes spikes = _spikes[s];
                if (spikes.State != SpikeState.Up || !spikes.Trap.Contains(racer.Position)) continue;
                if (spikes.LastHit.TryGetValue(racer.Index, out float last) && elapsed - last < spikes.Trap.HitCooldown) continue;
                spikes.LastHit[racer.Index] = elapsed;

                OnSpike?.Invoke(racer, racer.Position);
                damage(racer, null, spikes.Trap.Damage, DeathCause.Hazard);
                if (!racer.Alive) return;
            }
        }

        private void StepBumpers(Racer racer, float elapsed)
        {
            for (int b = 0; b < _bumps.Count; b++)
            {
                Bump bump = _bumps[b];
                Vector3 c = bump.Root.position;
                if (!Within(racer.Position, c, bump.Pad.Radius + racer.Radius)) continue;
                if (bump.LastBump.TryGetValue(racer.Index, out float last) && elapsed - last < bump.Pad.Cooldown) continue;
                bump.LastBump[racer.Index] = elapsed;

                Vector3 away = racer.Position - c;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-6f) away = -racer.Direction;
                away.Normalize();

                racer.Direction = away;
                // Step clear of the barrel so the next cast does not start inside it.
                racer.Position = c + away * (bump.Pad.Radius + racer.Radius + 0.05f);

                if (racer.BoostUntil <= 0f) racer.BaseSpeed = racer.Speed;
                racer.Speed = racer.BaseSpeed * bump.Pad.BoostMultiplier;
                racer.BoostUntil = elapsed + bump.Pad.BoostDuration;

                bump.SquashUntil = elapsed + 0.25f;
                OnBump?.Invoke(racer, racer.Position);
            }
        }

        private void StepKeys(Racer racer, float elapsed)
        {
            for (int k = 0; k < _keys.Count; k++)
            {
                Pickup key = _keys[k];
                if (key.Taken || !Within(racer.Position, key.Root.position, key.Radius + racer.Radius)) continue;

                key.Taken = true;
                if (key.Visual != null) key.Visual.gameObject.SetActive(false);
                OnKey?.Invoke(racer, key.Root.position);

                // One key is enough: every sibling key for the same gate vanishes with it.
                for (int j = 0; j < _keys.Count; j++)
                {
                    if (_keys[j].Id != key.Id || _keys[j].Taken) continue;
                    _keys[j].Taken = true;
                    if (_keys[j].Visual != null) _keys[j].Visual.gameObject.SetActive(false);
                }

                bool opened = false;
                for (int g = 0; g < _gates.Count; g++)
                {
                    Gate gate = _gates[g];
                    if (gate.Open || gate.Lock.GateId != key.Id) continue;
                    gate.Open = true;
                    opened = true;

                    // Drop into the floor the way a cycling door does; collider off so nothing
                    // can be caught on a sliver of it.
                    // Sink the block clean under the floor. Flattening it in Y read as "still
                    // closed" from the top-down camera - a gold slab is a gold slab from above -
                    // so racers seemed to walk through a shut gate. Gone below ground, the gap is
                    // unmistakable.
                    gate.Root.localPosition = gate.ClosedPosition - new Vector3(0f, gate.ClosedScale.y + 0.3f, 0f);
                    if (gate.Collider != null) gate.Collider.enabled = false;
                }

                if (opened)
                {
                    Physics.SyncTransforms();
                    OnGateOpened?.Invoke(key.Id);
                }
            }
        }

        private void StepCoins(Racer racer, float elapsed)
        {
            for (int c = 0; c < _coins.Count; c++)
            {
                Pickup coin = _coins[c];
                if (coin.Taken || !Within(racer.Position, coin.Root.position, coin.Radius + racer.Radius)) continue;

                coin.Taken = true;
                coin.BackAt = coin.Respawn > 0f ? elapsed + coin.Respawn : -1f;
                if (coin.Visual != null) coin.Visual.gameObject.SetActive(false);
                racer.Coins += coin.Value;
                OnCoin?.Invoke(racer, coin.Root.position);
            }
        }

        private void StepPotions(Racer racer, float elapsed)
        {
            if (racer.Health >= racer.MaxHealth) return;

            for (int p = 0; p < _potions.Count; p++)
            {
                Pickup potion = _potions[p];
                if (potion.Taken || !Within(racer.Position, potion.Root.position, potion.Radius + racer.Radius)) continue;

                potion.Taken = true;
                potion.BackAt = potion.Respawn > 0f ? elapsed + potion.Respawn : -1f;
                if (potion.Visual != null) potion.Visual.gameObject.SetActive(false);
                racer.Health = Mathf.Min(racer.MaxHealth, racer.Health + potion.Heal);
                OnPotion?.Invoke(racer, potion.Root.position);
                return;
            }
        }

        private void StepTeleporters(Racer racer, float elapsed)
        {
            if (_pads.Count < 2) return;
            if (_lastTeleport.TryGetValue(racer.Index, out float last) && elapsed - last < 1.5f) return;

            for (int p = 0; p < _pads.Count; p++)
            {
                Teleporter pad = _pads[p];
                if (!pad.Contains(racer.Position)) continue;

                Teleporter twin = null;
                for (int q = 0; q < _pads.Count; q++)
                {
                    if (q != p && _pads[q].PairId == pad.PairId) { twin = _pads[q]; break; }
                }

                if (twin == null) return;

                Vector3 from = racer.Position;
                Vector3 to = twin.Center;
                racer.Position = new Vector3(to.x, racer.Position.y, to.z);
                _lastTeleport[racer.Index] = elapsed;
                OnTeleport?.Invoke(racer, from, racer.Position);
                return;
            }
        }
    }
}
