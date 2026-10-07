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
    public sealed partial class CombatSystem
    {
        private readonly WeaponConfig _config;
        private readonly List<WeaponPickup> _pickups = new List<WeaponPickup>(4);

        // ---------------------------------------------------------------- arena arsenal
        private Core.ModeConfig _arenaMode;
        private Core.SimulationRandom _arenaRandom;
        private List<WeaponDefinition> _arenaCatalog;
        private float _nextDropAt;
        private float _pendingLandAt = -1f;
        private Vector3 _pendingDrop;
        private WeaponDefinition _pendingWeapon;
        private bool _nukeDropped;
        private float _time;

        public bool ArenaActive => _arenaMode != null;
        public System.Collections.Generic.Dictionary<string, int> KillsByWeapon => _projectiles.KillsByWeapon;
        public float NextDropIn => _pendingLandAt >= 0f ? Mathf.Max(0f, _pendingLandAt - _time) : Mathf.Max(0f, _nextDropAt - _time);
        public int WeaponsOnFloor { get { int n = 0; for (int i = 0; i < _pickups.Count; i++) if (_pickups[i].Available) n++; return n; } }

        /// <summary>(position, weapon) - a drop shadow appeared; the weapon lands after the warning.</summary>
        public event Action<Vector3, WeaponDefinition> OnDropWarning;
        /// <summary>(position, weapon) - the weapon hit the floor.</summary>
        public event Action<Vector3, WeaponDefinition> OnDropLanded;
        /// <summary>(holder, seconds left) - a fuse ticked to a whole second.</summary>
        public event Action<Racer, int> OnFuseTick;

        /// <summary>
        /// Turns this episode into an Arena: the floor starts empty and weapons airdrop inside
        /// the current safe zone on a schedule, drawn from the arena catalog by weight.
        /// </summary>
        public void EnableArena(Core.ModeConfig mode, Core.SimulationRandom random)
        {
            _arenaMode = mode;
            _arenaRandom = random;
            _arenaCatalog = WeaponConfig.ArenaCatalog();
            _nextDropAt = 2f;
            BuildPads();
            _projectiles.Knock = Knock;
        }

        /// <summary>Shove: the victim's heading becomes the hit direction and it slides 'metres' over 0.3 s.</summary>
        public void Knock(Racer victim, Vector3 direction, float metres)
        {
            if (victim == null || !victim.IsActive || metres <= 0f) return;
            direction.y = 0f;
            if (!PlanarMath.TryNormalizePlanar(direction, out Vector3 dir)) return;
            if (victim.BoostUntil <= 0f) victim.BaseSpeed = victim.Speed;
            victim.Direction = dir;
            victim.Speed = metres / 0.25f;
            victim.BoostUntil = _time + 0.25f;
            victim.Visual?.SnapToDirection(dir);
            OnKnock?.Invoke(victim, dir, metres);
        }

        /// <summary>(victim, direction, metres) - a hit shoved a cube; the visual layer sells the flight.</summary>
        public event Action<Racer, Vector3, float> OnKnock;

        private void StepArena(float deltaTime, Racer[] racers)
        {
            // Boost / knockback expiry for every mode: the device system only restores speed when
            // it exists, and an arena court has no devices.
            for (int i = 0; i < racers.Length; i++)
            {
                Racer r = racers[i];
                if (r.BoostUntil > 0f && _time >= r.BoostUntil) { r.Speed = r.BaseSpeed; r.BoostUntil = 0f; }
                // Orbit/front weapons: the sim owns the heading, the anchor only displays it.
                if (r.Armed && r.Visual != null && r.Visual.Anchor != null && r.Visual.Anchor.OrbitMode)
                    r.Visual.Anchor.OrbitAngle = r.WeaponAngle;
            }

            if (_arenaMode == null) return;

            if (_pads.Count > 0) StepPads();

            if (_pendingLandAt >= 0f)
            {
                if (_time < _pendingLandAt) return;
                // An airdrop that nobody wants clears itself; the pads are the reliable supply.
                _pickups.Add(new WeaponPickup(_pendingWeapon, _pendingDrop, _groundY, _materials, _root, _pickupScale, _visuals) { ExpiresAt = _pads.Count > 0 ? _time + 15f : -1f });
                OnDropLanded?.Invoke(_pendingDrop, _pendingWeapon);
                _pendingLandAt = -1f;
                _nextDropAt = _time + _arenaMode.arenaDropInterval;
                return;
            }

            int alive = 0; for (int i = 0; i < racers.Length; i++) if (racers[i].IsActive) alive++;

            // The nuke: once, when the field is down to five, regardless of the cap.
            if (!_nukeDropped && alive <= 5 && alive >= 2)
            {
                WeaponDefinition nuke = _arenaCatalog.Find(d => d.id == "Nuke");
                if (nuke != null) { _nukeDropped = true; ScheduleDrop(nuke); return; }
            }

            // Pad arenas: the drops are the surprise on top of the menu, capped at two loose ones.
            int floor = _pads.Count > 0 ? LooseWeaponsOnFloor : WeaponsOnFloor;
            int cap = _pads.Count > 0 ? 2 : _arenaMode.arenaMaxWeapons;
            if (_time < _nextDropAt || floor >= cap) return;
            WeaponDefinition pick = RollDrop();
            if (pick != null) ScheduleDrop(pick);
            else _nextDropAt = _time + 1f;
        }

        private WeaponDefinition RollDrop()
        {
            float total = 0f;
            for (int i = 0; i < _arenaCatalog.Count; i++) total += Mathf.Max(0f, _arenaCatalog[i].dropWeight);
            if (total <= 0f) return null;
            float pick = _arenaRandom.NextFloat() * total;
            for (int i = 0; i < _arenaCatalog.Count; i++)
            {
                pick -= Mathf.Max(0f, _arenaCatalog[i].dropWeight);
                if (pick <= 0f && _arenaCatalog[i].dropWeight > 0f) return _arenaCatalog[i];
            }
            return _arenaCatalog[0];
        }

        private void ScheduleDrop(WeaponDefinition weapon)
        {
            Rect zone = _pressure.CurrentBounds(_arena.PlayableRect);
            float margin = _arenaMode.arenaDropEdgeMargin;
            var inner = new Rect(zone.xMin + margin, zone.yMin + margin, Mathf.Max(1f, zone.width - 2f * margin), Mathf.Max(1f, zone.height - 2f * margin));
            var placed = new List<Vector3>();
            for (int i = 0; i < _pickups.Count; i++) if (_pickups[i].Available) placed.Add(_pickups[i].Position);
            Vector3 at = PickSpawnPosition(_arenaRandom, inner, placed);
            at = FindValidDropPosition(at);

            _pendingWeapon = weapon;
            _pendingDrop = at;
            _pendingLandAt = _time + _arenaMode.arenaDropWarning;
            OnDropWarning?.Invoke(at, weapon);
        }

        /// <summary>Suicide devices: count the fuse down, detonate on timeout or on a touch.</summary>
        private void StepFuses(Racer[] racers, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive || !racer.Armed || !racer.Weapon.IsSuicide) continue;

                int before = Mathf.CeilToInt(racer.WeaponHoldRemaining + 1e-3f);
                bool touching = false;
                if (racer.Weapon.attackRange > 0f)
                {
                    float reach = racer.Weapon.attackRange + racer.HalfExtent;
                    for (int j = 0; j < racers.Length; j++)
                    {
                        Racer other = racers[j];
                        if (other == racer || !other.IsActive) continue;
                        if (!_config.friendlyFire && other.Team == racer.Team) continue;
                        if (!HasLineOfSight(racer.Position, other.Position)) continue;
                        Vector3 d = other.Position - racer.Position; d.y = 0f;
                        if (d.sqrMagnitude <= reach * reach) { touching = true; break; }
                    }
                }

                if (!touching && racer.WeaponHoldRemaining > 0f)
                {
                    int after = Mathf.CeilToInt(racer.WeaponHoldRemaining + 1e-3f);
                    if (after != before) OnFuseTick?.Invoke(racer, after);
                    continue;
                }

                WeaponDefinition bomb = racer.Weapon;
                Vector3 centre = racer.Position;
                // The device is consumed: strip it before the blast so it never re-drops.
                racer.Weapon = null;
                WeaponPickup pickup = racer.HeldPickup;
                racer.HeldPickup = null;
                racer.Visual?.DetachWeapon();
                if (pickup != null) _pickups.Remove(pickup);
                _projectiles.Detonate(racer, centre, bomb.blastRadius, bomb.damage, bomb.blastEdgeDamage, bomb.knockback, applyDamage, bomb.id, true);
                applyDamage(racer, null, 9999f, DeathCause.Hazard);
                OnMeleeHit?.Invoke(racer, racer, centre);
            }
        }
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

            _projectiles = new ProjectilePool(_root, materials, groundY, visuals) { FriendlyFire = config.friendlyFire };

            if (config.enabled) SpawnWeapons(random);
        }

        // ---------------------------------------------------------------- spawning

        /// <summary>
        /// Two pickups spawning on top of each other read as one glitched weapon from above.
        /// Keep at least this much floor between them.
        /// </summary>
        private const float MinPickupSeparation = 3f;

        /// <summary>
        /// Places the episode's weapons. Authored maps choose from their declared weapon areas;
        /// procedural maps use the reserved central clearing. Either way the exact point comes from
        /// the run seed, so the same seed reproduces the same spawn.
        ///
        /// Both the weapon types and the areas are dealt round-robin from a seeded shuffle rather
        /// than rolled independently: with two knives and two areas that means one of each knife,
        /// one per side - never two identical weapons stacked in the same clearing.
        /// </summary>
        private void SpawnWeapons(SimulationRandom random)
        {
            List<WeaponDefinition> catalog = ResolveCatalog();
            if (catalog.Count == 0)
            {
                Debug.LogWarning("[CubeSim] Weapon config has no eligible weapons; combat will be inert.");
                return;
            }

            Shuffle(catalog, random);
            List<Rect> areas = ResolveShuffledAreas(random);

            int count = Mathf.Max(0, _config.count);
            var placed = new List<Vector3>(count);

            for (int i = 0; i < count; i++)
            {
                WeaponDefinition definition = catalog[i % catalog.Count];
                Vector3 position = PickSpawnPosition(random, areas[i % areas.Count], placed);
                placed.Add(position);
                _pickups.Add(new WeaponPickup(definition, position, _groundY, _materials, _root,
                    _pickupScale, _visuals));
            }
        }

        private static void Shuffle<T>(List<T> list, SimulationRandom random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The spawn areas in a seeded order; pickups walk this list round-robin.</summary>
        private List<Rect> ResolveShuffledAreas(SimulationRandom random)
        {
            var areas = new List<Rect>();

            List<WeaponSpawnArea> authored = _arena.WeaponAreas;
            if (authored != null && authored.Count > 0)
            {
                for (int i = 0; i < authored.Count; i++) areas.Add(authored[i].Footprint);
                Shuffle(areas, random);
            }
            else
            {
                areas.Add(_arena.HasClearing ? _arena.ClearingRect : _arena.PlayableRect);
            }

            return areas;
        }

        private List<WeaponDefinition> ResolveCatalog()
        {
            List<WeaponDefinition> source = _arenaCatalog != null ? _arenaCatalog
                : _config.catalog != null && _config.catalog.Count > 0 ? _config.catalog
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

        private Vector3 PickSpawnPosition(SimulationRandom random, Rect area, List<Vector3> placed)
        {
            float inset = Mathf.Min(1.2f, Mathf.Min(area.width, area.height) * 0.25f);
            Vector3 bestClear = new Vector3(area.center.x, _groundY, area.center.y);
            bool foundClear = false;

            for (int attempt = 0; attempt < 64; attempt++)
            {
                var candidate = new Vector3(
                    random.Range(area.xMin + inset, area.xMax - inset),
                    _groundY,
                    random.Range(area.yMin + inset, area.yMax - inset));

                if (_arena.OverlapsWall(new Vector2(candidate.x, candidate.z), 0.6f)) continue;

                if (!foundClear)
                {
                    // Remember the first wall-clear point: if separation never works out (a tiny
                    // area with a pickup already in it), a clear-but-close spot beats the centre.
                    bestClear = candidate;
                    foundClear = true;
                }

                if (IsSeparated(candidate, placed)) return candidate;
            }

            if (!foundClear) Debug.LogWarning("[CubeSim] Weapon spawn fell back to the area centre.");
            return bestClear;
        }

        private static bool IsSeparated(Vector3 candidate, List<Vector3> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                Vector3 delta = placed[i] - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < MinPickupSeparation * MinPickupSeparation) return false;
            }

            return true;
        }

        /// <summary>
        /// Drops a fresh weapon on the floor mid-round (Lucky Block). Uses the first eligible
        /// catalog entry, so a knife map drops knives; the position is nudged to legal floor.
        /// </summary>
        public WeaponPickup SpawnPickup(Vector3 position)
        {
            List<WeaponDefinition> catalog = ResolveCatalog();
            if (catalog.Count == 0) return null;
            var pickup = new WeaponPickup(catalog[0], FindValidDropPosition(position), _groundY, _materials, _root,
                _pickupScale, _visuals);
            _pickups.Add(pickup);
            return pickup;
        }

        // ---------------------------------------------------------------- per-step

        public void Step(float deltaTime, Racer[] racers, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            if (!_config.enabled) return;

            _time += deltaTime;
            for (int i = 0; i < _pickups.Count; i++) _pickups[i].Tick(deltaTime);
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                WeaponPickup p = _pickups[i];
                if (p.Available && p.ExpiresAt >= 0f && _time >= p.ExpiresAt) { _pickups.RemoveAt(i); p.Remove(); }
            }
            StepArena(deltaTime, racers);

            // A loose weapon must stay reachable. It was dropped inside the safe zone, but the
            // collapse keeps moving: once the boundary passes it, nobody can ever pick it up and a
            // one-knife fight stalls. Walk it back to the nearest valid floor, same deterministic
            // ring search a drop uses, so it rides the shrinking zone instead of dying outside it.
            for (int i = 0; i < _pickups.Count; i++)
            {
                WeaponPickup pickup = _pickups[i];
                if (!pickup.Available) continue;
                if (IsValidDrop(pickup.Position, 0.65f)) continue;
                pickup.SetPosition(FindValidDropPosition(pickup.Position));
            }

            ResolvePickups(racers);
            for (int i = 0; i < racers.Length; i++)
            {
                Racer r = racers[i];
                if (r.Armed && r.Weapon.kind == WeaponKind.Shield && r.Shield <= 0)
                {
                    WeaponPickup used = r.HeldPickup;
                    ForceDrop(r, DropReason.OutOfAmmo);
                    // Pad arenas already destroyed it inside ForceDrop; legacy maps consume it here.
                    if (used != null && _pickups.Remove(used)) used.Remove();
                    OnShieldHit?.Invoke(r, true);
                }
            }
            StepFuses(racers, applyDamage);
            TickOwnership(deltaTime, racers);

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive) continue;

                if (racer.AttackCooldown > 0f) racer.AttackCooldown -= deltaTime;
                if (!racer.Armed) continue;
                if (racer.Weapon.kind != WeaponKind.Classic)
                {
                    // Arsenal weapons aim every tick (the heading is a sim value) and gate their own cadence.
                    if (racer.StaggerUntil > _time || racer.Weapon.IsSuicide) continue;
                    StepArsenalWeapon(racer, racers, deltaTime, applyDamage);
                    continue;
                }
                if (racer.AttackCooldown > 0f || racer.StaggerUntil > _time) continue;
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
            if (definition.kind == WeaponKind.Shield) { racer.Shield = definition.shieldHits; racer.Badge = "\U0001F6E1"; }
            racer.WeaponAngle = Mathf.Atan2(racer.Direction.x, racer.Direction.z) * Mathf.Rad2Deg;
            racer.AimTarget = null;

            GameObject visual = WeaponVisualFactory.Create(definition, _materials, _root, _visuals,
                WeaponVisualFactory.Context.Equipped);
            racer.Visual?.AttachWeapon(visual, definition, _equippedScale);
            if (racer.Visual?.Anchor != null)
            {
                bool orbit = definition.kind == WeaponKind.Orbit || definition.kind == WeaponKind.Front;
                bool aimed = definition.kind == WeaponKind.Gun || definition.kind == WeaponKind.Sniper ||
                             definition.kind == WeaponKind.Lob || definition.kind == WeaponKind.Punch;
                // Every arsenal weapon swings freely round the cube on the sim heading; only the
                // shield and the vest sit on the old side anchor.
                racer.Visual.Anchor.OrbitMode = orbit || aimed;
                racer.Visual.Anchor.OrbitAngle = racer.WeaponAngle;
                racer.Visual.Anchor.OrbitRadius = orbit ? racer.HalfExtent + definition.bladeLength * 0.5f
                    : definition.kind == WeaponKind.Punch ? racer.HalfExtent + 1.3f    // guard stance: fists up in front of the face, on the travel/aim heading
                    : racer.HalfExtent + 0.9f;
                if (definition.kind == WeaponKind.Shield) { racer.Visual.Anchor.OrbitMode = true; racer.Visual.Anchor.OrbitRadius = 0f; }
            }

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
            if (racer.Visual?.Anchor != null) racer.Visual.Anchor.OrbitMode = false;
            if (definition.kind == WeaponKind.Shield) { racer.Shield = 0; racer.Badge = ""; }

            DropCount++;
            switch (reason)
            {
                case DropReason.Timeout: TimeoutDrops++; break;
                case DropReason.OutOfAmmo: AmmoDrops++; break;
                case DropReason.OwnerDeath: DeathDrops++; break;
            }

            if (pickup != null)
            {
                if (_pads.Count > 0 && reason != DropReason.OwnerDeath)
                {
                    // Pad arenas: a spent or expired weapon is gone; its pad regrows a fresh one.
                    _pickups.Remove(pickup);
                    pickup.Remove();
                }
                else
                {
                    pickup.Drop(racer, FindValidDropPosition(racer.Position),
                        _config.dropRearmDelay, _config.repickupCooldown);
                    // A dead cube's weapon is up for grabs briefly, then the floor clears.
                    if (_pads.Count > 0) pickup.ExpiresAt = _time + 8f;
                }
            }

            OnDropped?.Invoke(racer, definition, reason);
        }

        /// <summary>Spends one use of an ammo-based weapon and releases it when they run out.</summary>
        /// <summary>No blade, punch or beam reaches through a wall.</summary>
        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            float y = _groundY + 0.5f;
            return !Physics.Linecast(new Vector3(from.x, y, from.z), new Vector3(to.x, y, to.z),
                _wallMask, QueryTriggerInteraction.Ignore);
        }

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
                if (!_config.friendlyFire && other.Team == racer.Team) continue;
                if (!HasLineOfSight(racer.Position, other.Position)) continue;

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
            if (!target.Alive) _projectiles.CreditKill(weapon.id);
            if (target.IsActive && weapon.knockback > 0f) Knock(target, swing, weapon.knockback);
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
                if (!_config.friendlyFire && other.Team == racer.Team) continue;
                if (!HasLineOfSight(racer.Position, other.Position)) continue;

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
                // A piercing shot is a line: every opponent within its width along the beam is hit,
                // nearest first, up to pierce + 1 victims.
                int budget = weapon.pierce + 1;
                var line = new List<Racer>();
                for (int i = 0; i < racers.Length && budget > 0; i++)
                {
                    Racer other = racers[i];
                    if (!other.IsActive || other == racer) continue;
                    if (!_config.friendlyFire && other.Team == racer.Team) continue;
                    if (!HasLineOfSight(racer.Position, other.Position)) continue;
                    Vector3 rel = other.Position - racer.Position; rel.y = 0f;
                    float along = Vector3.Dot(rel, shot);
                    if (along <= 0f || along > weapon.attackRange) continue;
                    float side = (rel - shot * along).magnitude;
                    if (side > other.HalfExtent + 0.3f) continue;
                    line.Add(other);
                }
                line.Sort((a, b) => Vector3.Dot(a.Position - racer.Position, shot).CompareTo(Vector3.Dot(b.Position - racer.Position, shot)));
                for (int i = 0; i < line.Count && i < budget; i++)
                {
                    Racer hit = line[i];
                    Vector3 impact = hit.Position;
                    applyDamage(hit, racer, weapon.damage, DeathCause.Ranged);
                    if (!hit.Alive) _projectiles.CreditKill(weapon.id);
                    if (hit.IsActive && weapon.knockback > 0f) Knock(hit, shot, weapon.knockback);
                    _projectiles.ReportHitscanHit(hit, racer, impact, shot);
                }
            }
            else
            {
                int pellets = Mathf.Max(1, weapon.pellets);
                for (int k = 0; k < pellets; k++)
                {
                    float angle = pellets == 1 ? 0f : weapon.spreadDegrees * ((float)k / (pellets - 1) - 0.5f);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * shot;
                    _projectiles.Spawn(racer, weapon, origin, dir, weapon.attackRange);
                }
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

                    // Reachable from where it fell: never through a wall into a sealed room.
                    if (IsValidDrop(candidate, clearance) && HasLineOfSight(origin, candidate)) return candidate;
                }
            }

            // Last resort: the open centre, but only if it is actually open floor; otherwise the
            // weapon stays where it fell rather than appearing inside a wall.
            Rect bounds = _pressure.CurrentBounds(_arena.PlayableRect);
            var centre = new Vector3(bounds.center.x, _groundY, bounds.center.y);
            return IsValidDrop(centre, clearance) ? centre : origin;
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
