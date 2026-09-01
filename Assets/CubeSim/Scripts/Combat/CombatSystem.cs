using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Core;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Combat
{
    /// <summary>
    /// Weapon circulation and attack resolution. It sits entirely on top of the movement system: a
    /// racer never steers toward a weapon or a target. It collects a weapon by wandering into it,
    /// attacks whatever happens to be in range, and loses the weapon on a timer or on ammo.
    ///
    /// Ownership is temporary by design - the weapon is meant to pass between racers during a run.
    /// </summary>
    public sealed class CombatSystem
    {
        private readonly WeaponConfig _config;
        private readonly List<WeaponPickup> _pickups = new List<WeaponPickup>(4);
        private readonly ProjectilePool _projectiles;
        private readonly MaterialLibrary _materials;
        private readonly ArenaRuntime _arena;
        private readonly PressureField _pressure;
        private readonly Transform _root;
        private readonly float _groundY;
        private readonly int _wallMask;
        private readonly float _pickupScale;
        private readonly float _equippedScale;
        private readonly WeaponVisualLibrary _visuals;

        public IReadOnlyList<WeaponPickup> Pickups => _pickups;

        /// <summary>Counters the validation harness reads, so circulation can be proven, not assumed.</summary>
        public int PickupCount { get; private set; }
        public int DropCount { get; private set; }
        public int TimeoutDrops { get; private set; }
        public int AmmoDrops { get; private set; }
        public int DeathDrops { get; private set; }

        public event Action<Racer, WeaponDefinition> OnEquipped;
        public event Action<Racer, WeaponDefinition, DropReason> OnDropped;

        /// <summary>(attacker, swing direction) - fired on every melee swing that finds a target.</summary>
        public event Action<Racer, Vector3> OnMeleeSwing;

        /// <summary>(victim, attacker, impact point) - fired when melee damage actually lands.</summary>
        public event Action<Racer, Racer, Vector3> OnMeleeHit;

        /// <summary>(shooter, muzzle point, shot direction) - fired on every ranged attack.</summary>
        public event Action<Racer, Vector3, Vector3> OnRangedShot;

        /// <summary>Exposed so presentation layers can hook projectile impacts.</summary>
        public ProjectilePool Projectiles => _projectiles;

        public CombatSystem(WeaponConfig config, ArenaRuntime arena, PressureField pressure,
            SimulationRandom random, MaterialLibrary materials, float groundY, Transform parent,
            float pickupScale, float equippedScale, WeaponVisualLibrary visuals)
        {
            _pickupScale = Mathf.Max(0.1f, pickupScale);
            _equippedScale = Mathf.Max(0.1f, equippedScale);
            _visuals = visuals;
            _config = config;
            _arena = arena;
            _pressure = pressure;
            _materials = materials;
            _groundY = groundY;
            _wallMask = SimulationLayers.WallMask;

            _root = new GameObject("Combat").transform;
            _root.SetParent(parent, false);

            _projectiles = new ProjectilePool(_root, materials, groundY, visuals);

            if (config.enabled) SpawnWeapons(random);
        }

        // ---------------------------------------------------------------- spawning

        /// <summary>
        /// Places the episode's weapons. Authored maps choose from their declared weapon areas;
        /// procedural maps use the reserved central clearing. Either way the exact point comes from
        /// the run seed, so the same seed reproduces the same spawn.
        /// </summary>
        private void SpawnWeapons(SimulationRandom random)
        {
            List<WeaponDefinition> catalog = ResolveCatalog();
            if (catalog.Count == 0)
            {
                Debug.LogWarning("[CubeSim] Weapon config has no eligible weapons; combat will be inert.");
                return;
            }

            int count = Mathf.Max(0, _config.count);
            for (int i = 0; i < count; i++)
            {
                WeaponDefinition definition = catalog[random.Range(0, catalog.Count)];
                Vector3 position = PickSpawnPosition(random);
                _pickups.Add(new WeaponPickup(definition, position, _groundY, _materials, _root,
                    _pickupScale, _visuals));
            }
        }

        private List<WeaponDefinition> ResolveCatalog()
        {
            List<WeaponDefinition> source = _config.catalog != null && _config.catalog.Count > 0
                ? _config.catalog
                : WeaponConfig.DefaultCatalog();

            var eligible = new List<WeaponDefinition>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                WeaponDefinition definition = source[i];

                if (_config.allowedIds.Count > 0 && !_config.allowedIds.Contains(definition.id)) continue;
                if (_config.allowedCategories.Count > 0 && !_config.allowedCategories.Contains(definition.category)) continue;

                eligible.Add(definition);
            }

            return eligible;
        }

        private Vector3 PickSpawnPosition(SimulationRandom random)
        {
            Rect area = ResolveSpawnArea(random);
            float inset = Mathf.Min(1.2f, Mathf.Min(area.width, area.height) * 0.25f);

            for (int attempt = 0; attempt < 64; attempt++)
            {
                var candidate = new Vector3(
                    random.Range(area.xMin + inset, area.xMax - inset),
                    _groundY,
                    random.Range(area.yMin + inset, area.yMax - inset));

                if (!_arena.OverlapsWall(new Vector2(candidate.x, candidate.z), 0.6f)) return candidate;
            }

            Debug.LogWarning("[CubeSim] Weapon spawn fell back to the area centre.");
            return new Vector3(area.center.x, _groundY, area.center.y);
        }

        private Rect ResolveSpawnArea(SimulationRandom random)
        {
            List<WeaponSpawnArea> authored = _arena.WeaponAreas;
            if (authored != null && authored.Count > 0)
            {
                // Weighted pick, still entirely seed driven.
                float total = 0f;
                for (int i = 0; i < authored.Count; i++) total += authored[i].Weight;

                if (total > 0f)
                {
                    float roll = random.Range(0f, total);
                    for (int i = 0; i < authored.Count; i++)
                    {
                        roll -= authored[i].Weight;
                        if (roll <= 0f) return authored[i].Footprint;
                    }
                }

                return authored[authored.Count - 1].Footprint;
            }

            return _arena.HasClearing ? _arena.ClearingRect : _arena.PlayableRect;
        }

        // ---------------------------------------------------------------- per-step

        public void Step(float deltaTime, Racer[] racers, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            if (!_config.enabled) return;

            for (int i = 0; i < _pickups.Count; i++) _pickups[i].Tick(deltaTime);

            ResolvePickups(racers);
            TickOwnership(deltaTime, racers);

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive) continue;

                if (racer.AttackCooldown > 0f) racer.AttackCooldown -= deltaTime;
                if (!racer.Armed || racer.AttackCooldown > 0f) continue;

                if (racer.Weapon.category == WeaponCategory.Melee) TryMelee(racer, racers, applyDamage);
                else TryRanged(racer, racers, applyDamage);
            }

            _projectiles.Step(deltaTime, racers, applyDamage);
        }

        /// <summary>Runs the hold timer and releases the weapon when it expires.</summary>
        private void TickOwnership(float deltaTime, Racer[] racers)
        {
            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.Armed) continue;

                if (ResolveReleaseMode(racer.Weapon) != WeaponReleaseMode.TimeBased) continue;

                racer.WeaponHoldRemaining -= deltaTime;
                if (racer.WeaponHoldRemaining <= 0f) ForceDrop(racer, DropReason.Timeout);
            }
        }

        private void ResolvePickups(Racer[] racers)
        {
            for (int p = 0; p < _pickups.Count; p++)
            {
                WeaponPickup pickup = _pickups[p];
                if (!pickup.CanBeCollected) continue;

                for (int i = 0; i < racers.Length; i++)
                {
                    Racer racer = racers[i];
                    if (!racer.IsActive || racer.Armed) continue;
                    if (!pickup.CanBeCollectedBy(racer)) continue;

                    float reach = racer.HalfExtent + _config.pickupRadius;
                    Vector3 offset = racer.Position - pickup.Position;
                    if (Mathf.Abs(offset.x) > reach || Mathf.Abs(offset.z) > reach) continue;

                    Equip(racer, pickup);
                    break;
                }
            }
        }

        private WeaponReleaseMode ResolveReleaseMode(WeaponDefinition weapon)
            => weapon.useOwnRelease ? weapon.releaseMode : _config.releaseMode;

        private float ResolveHoldDuration(WeaponDefinition weapon)
            => weapon.useOwnRelease ? weapon.holdDuration : _config.holdDuration;

        private int ResolveAmmo(WeaponDefinition weapon)
            => weapon.useOwnRelease ? weapon.ammo : _config.ammo;

        private void Equip(Racer racer, WeaponPickup pickup)
        {
            // One racer, one weapon. Anything already held is released first, through the same path.
            if (racer.Armed) ForceDrop(racer, DropReason.Replaced);

            WeaponDefinition definition = pickup.Definition;

            racer.Weapon = definition;
            racer.HeldPickup = pickup;
            racer.AttackCooldown = definition.attackCooldown * 0.5f;
            racer.WeaponHoldRemaining = ResolveHoldDuration(definition);
            racer.WeaponAmmo = Mathf.Max(1, ResolveAmmo(definition));

            pickup.Collect(racer);
            PickupCount++;
            racer.TimesArmed++;

            GameObject visual = WeaponVisualFactory.Create(definition, _materials, _root, _visuals,
                WeaponVisualFactory.Context.Equipped);
            racer.Visual?.AttachWeapon(visual, definition, _equippedScale);

            OnEquipped?.Invoke(racer, definition);
        }

        /// <summary>
        /// The one authoritative release pathway. Every drop - timeout, ammo, death, replacement -
        /// goes through here, so the detach, state clear, relocation and cooldown can never diverge.
        /// </summary>
        public void ForceDrop(Racer racer, DropReason reason)
        {
            if (racer == null || !racer.Armed) return;

            WeaponPickup pickup = racer.HeldPickup;
            WeaponDefinition definition = racer.Weapon;

            racer.Weapon = null;
            racer.HeldPickup = null;
            racer.WeaponHoldRemaining = 0f;
            racer.WeaponAmmo = 0;
            racer.Visual?.DetachWeapon();

            DropCount++;
            switch (reason)
            {
                case DropReason.Timeout: TimeoutDrops++; break;
                case DropReason.OutOfAmmo: AmmoDrops++; break;
                case DropReason.OwnerDeath: DeathDrops++; break;
            }

            if (pickup != null)
            {
                pickup.Drop(racer, FindValidDropPosition(racer.Position),
                    _config.dropRearmDelay, _config.repickupCooldown);
            }

            OnDropped?.Invoke(racer, definition, reason);
        }

        /// <summary>Spends one use of an ammo-based weapon and releases it when they run out.</summary>
        private void ConsumeUse(Racer racer)
        {
            if (ResolveReleaseMode(racer.Weapon) != WeaponReleaseMode.AmmoBased) return;

            racer.WeaponAmmo--;
            if (racer.WeaponAmmo <= 0) ForceDrop(racer, DropReason.OutOfAmmo);
        }

        // ---------------------------------------------------------------- attacks

        /// <summary>
        /// Melee: nearest living opponent inside range and inside the attack arc around the racer's
        /// current movement direction. No steering, no navigation - just a contact test.
        /// </summary>
        private void TryMelee(Racer racer, Racer[] racers, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition weapon = racer.Weapon;
            float range = weapon.attackRange + racer.HalfExtent;
            float cosArc = Mathf.Cos(Mathf.Clamp(weapon.attackArc, 1f, 180f) * Mathf.Deg2Rad);

            Racer target = null;
            float bestSqr = range * range;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer other = racers[i];
                if (!other.IsActive || other == racer) continue;

                Vector3 offset = other.Position - racer.Position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > bestSqr) continue;

                if (sqr > 1e-6f)
                {
                    Vector3 toTarget = offset / Mathf.Sqrt(sqr);
                    if (Vector3.Dot(toTarget, racer.Direction) < cosArc) continue;
                }

                bestSqr = sqr;
                target = other;
            }

            if (target == null) return;

            racer.AttackCooldown = weapon.attackCooldown;
            racer.Visual?.PlayAttack(WeaponCategory.Melee);

            Vector3 swing = target.Position - racer.Position;
            swing.y = 0f;
            OnMeleeSwing?.Invoke(racer, swing.sqrMagnitude > 1e-6f ? swing.normalized : racer.Direction);

            Vector3 contact = Vector3.Lerp(racer.Position, target.Position, 0.65f);
            applyDamage(target, racer, weapon.damage, DeathCause.Melee);
            OnMeleeHit?.Invoke(target, racer, contact);

            ConsumeUse(racer);
        }

        /// <summary>
        /// Ranged: nearest living opponent in range with clear line of sight. Walls block the shot,
        /// both for target selection and for the projectile itself.
        /// </summary>
        private void TryRanged(Racer racer, Racer[] racers, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition weapon = racer.Weapon;
            Racer target = null;
            float bestSqr = weapon.attackRange * weapon.attackRange;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer other = racers[i];
                if (!other.IsActive || other == racer) continue;

                Vector3 offset = other.Position - racer.Position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (weapon.requireLineOfSight && !HasLineOfSight(racer, other)) continue;

                bestSqr = sqr;
                target = other;
            }

            if (target == null) return;

            Vector3 direction = target.Position - racer.Position;
            direction.y = 0f;
            if (!PlanarMath.TryNormalizePlanar(direction, out Vector3 shot)) return;

            racer.AttackCooldown = weapon.attackCooldown;
            racer.Visual?.SnapToDirection(shot);
            racer.Visual?.PlayAttack(WeaponCategory.Ranged);

            Vector3 origin = racer.Position + shot * (racer.HalfExtent + weapon.projectileRadius + 0.05f);
            OnRangedShot?.Invoke(racer, origin, shot);

            if (weapon.hitscan)
            {
                Vector3 impact = target.Position;
                applyDamage(target, racer, weapon.damage, DeathCause.Ranged);
                _projectiles.ReportHitscanHit(target, racer, impact, shot);
            }
            else
            {
                _projectiles.Spawn(racer, weapon, origin, shot, weapon.attackRange);
            }

            ConsumeUse(racer);
        }

        private bool HasLineOfSight(Racer from, Racer to)
        {
            Vector3 a = new Vector3(from.Position.x, _groundY + from.HalfExtent, from.Position.z);
            Vector3 b = new Vector3(to.Position.x, _groundY + to.HalfExtent, to.Position.z);
            Vector3 delta = b - a;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return true;

            return !Physics.Raycast(a, delta / distance, distance, _wallMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- drop placement

        /// <summary>
        /// A drop must land clear of walls, inside the playfield and clear of filled pressure,
        /// otherwise the weapon would be unreachable for the rest of the episode. The search is a
        /// deterministic widening ring from the drop point - never a random teleport across the map.
        /// </summary>
        private Vector3 FindValidDropPosition(Vector3 origin)
        {
            const float clearance = 0.65f;
            if (IsValidDrop(origin, clearance)) return origin;

            for (int ring = 1; ring <= 10; ring++)
            {
                float radius = ring * 0.85f;
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * (Mathf.PI * 2f / 12f);
                    var candidate = new Vector3(
                        origin.x + Mathf.Cos(angle) * radius,
                        _groundY,
                        origin.z + Mathf.Sin(angle) * radius);

                    if (IsValidDrop(candidate, clearance)) return candidate;
                }
            }

            Rect bounds = _pressure.CurrentBounds(_arena.PlayableRect);
            return new Vector3(bounds.center.x, _groundY, bounds.center.y);
        }

        private bool IsValidDrop(Vector3 position, float clearance)
        {
            var planar = new Vector2(position.x, position.z);
            if (!_arena.InsidePlayable(planar, clearance)) return false;
            if (_arena.OverlapsWall(planar, clearance)) return false;
            return _pressure.IsInsideBounds(position, clearance);
        }
    }
}
