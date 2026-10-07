using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Combat
{
    public enum WeaponCategory
    {
        Melee = 0,
        Ranged = 1
    }

    /// <summary>
    /// How a racer loses a weapon. Ownership is meant to be temporary: the weapon circulates so
    /// several racers get a turn with it. Owner death also releases it, but that is the exception,
    /// not the mechanism.
    /// </summary>
    /// <summary>How a held weapon behaves on its own in Weapon Arena. Classic = legacy melee/ranged path.</summary>
    public enum WeaponKind
    {
        Classic = 0,
        /// <summary>Blade sweeping around the body (sword, axe, laser sword).</summary>
        Orbit = 1,
        /// <summary>Blade held in front, grinding continuously (chainsaw).</summary>
        Front = 2,
        /// <summary>Punch straight ahead with heavy knockback (boxing glove).</summary>
        Punch = 3,
        /// <summary>Fires along the run direction on a cadence (revolver, shotgun, SMG, rocket).</summary>
        Gun = 4,
        /// <summary>Locks the nearest cube with a laser, then fires a piercing line.</summary>
        Sniper = 5,
        /// <summary>Lobs a shell onto the nearest cube with a floor warning.</summary>
        Lob = 6,
        /// <summary>Absorbs hits until it breaks.</summary>
        Shield = 7,
        /// <summary>Suicide device on a fuse.</summary>
        Vest = 8,
    }

    public enum WeaponReleaseMode
    {
        /// <summary>Held for a fixed number of seconds, then dropped automatically.</summary>
        TimeBased = 0,

        /// <summary>Each attack spends a use; dropped automatically when they run out.</summary>
        AmmoBased = 1
    }

    /// <summary>Why a weapon left its owner. One enum so every release goes through one pathway.</summary>
    public enum DropReason
    {
        Timeout = 0,
        OutOfAmmo = 1,
        OwnerDeath = 2,
        Replaced = 3,
        System = 4
    }

    /// <summary>
    /// One weapon archetype, entirely data. Adding a weapon to an episode means adding an entry to
    /// <see cref="WeaponConfig.catalog"/>, not writing code.
    /// </summary>
    [Serializable]
    public class WeaponDefinition
    {
        public string id = "weapon";
        public WeaponCategory category = WeaponCategory.Melee;

        public float damage = 34f;

        [Tooltip("Seconds between attacks.")]
        public float attackCooldown = 1.1f;

        [Tooltip("Maximum distance to a target, in metres.")]
        public float attackRange = 1.8f;

        [Header("Melee")]
        [Tooltip("Half angle, in degrees, around the movement direction that counts as a hit. 180 = any direction.")]
        public float attackArc = 100f;

        [Header("Ranged")]
        [Tooltip("Resolve the shot instantly with a raycast instead of spawning a travelling projectile.")]
        public bool hitscan = false;

        public float projectileSpeed = 26f;
        public float projectileRadius = 0.18f;

        [Tooltip("Walls block the shot. Leave on unless a weapon is explicitly meant to shoot through geometry.")]
        public bool requireLineOfSight = true;

        [Header("Ownership")]
        [Tooltip("Overrides the episode release mode when useOwnRelease is on.")]
        public WeaponReleaseMode releaseMode = WeaponReleaseMode.TimeBased;

        [Tooltip("Use this weapon's own release settings instead of the episode defaults.")]
        public bool useOwnRelease = false;

        public float holdDuration = 7f;
        public int ammo = 5;

        [Header("Visual")]
        [Tooltip("Optional Resources path to a model. Empty builds a primitive stand-in.")]
        public string visualPrefabPath = "";

        public Color color = Color.white;

        [Tooltip("Size of the primitive stand-in, in metres.")]
        public Vector3 visualSize = new Vector3(0.09f, 0.09f, 0.95f);

        [Tooltip("Offset and rotation applied when the weapon is held, relative to the hand bone.")]
        public Vector3 gripOffset = Vector3.zero;

        public Vector3 gripEuler = Vector3.zero;

        [Header("Arena arsenal")]
        [Tooltip("Metres the victim is shoved along the hit direction (0 = none).")]
        public float knockback = 0f;
        [Tooltip("Projectiles per shot, fanned over spreadDegrees (1 = single bolt).")]
        public int pellets = 1;
        public float spreadDegrees = 0f;
        [Tooltip("Racers a projectile can pass through before it stops (0 = stops on first).")]
        public int pierce = 0;
        [Tooltip("Lobbed: the projectile arcs and detonates at the end of its range or on impact.")]
        public bool lobbed = false;
        [Tooltip("Blast radius on detonation, metres (0 = no blast).")]
        public float blastRadius = 0f;
        [Tooltip("Damage at the blast edge; the centre deals 'damage'.")]
        public float blastEdgeDamage = 0f;
        [Tooltip("Suicide device: detonates after this many seconds held, or on touching an opponent.")]
        public float fuseSeconds = 0f;
        [Tooltip("Airdrop weight in Arena mode (0 = never drops).")]
        public float dropWeight = 1f;

        public bool IsSuicide => fuseSeconds > 0f;

        [Header("Weapon Arena behaviour")]
        public WeaponKind kind = WeaponKind.Classic;
        [Tooltip("Orbit: seconds per full sweep around the body.")]
        public float orbitPeriod = 0.8f;
        [Tooltip("Orbit/Front: blade length from the body edge, metres.")]
        public float bladeLength = 2.5f;
        [Tooltip("Orbit/Front: half width of the blade hitbox, metres.")]
        public float bladeWidth = 0.35f;
        [Tooltip("Seconds the victim stands stunned after a hit (0 = none).")]
        public float staggerSeconds = 0f;
        [Tooltip("Gun: times a bullet bounces off walls before it dies.")]
        public int bounces = 0;
        [Tooltip("Sniper: seconds the laser sits on the target before the shot.")]
        public float aimSeconds = 0.8f;
        [Tooltip("Shield: hits absorbed before it breaks.")]
        public int shieldHits = 3;
    }

    /// <summary>Episode-level weapon rules.</summary>
    [Serializable]
    public class WeaponConfig
    {
        public bool enabled = true;

        [Tooltip("Weapons spawned at the start of an episode.")]
        public int count = 1;

        [Tooltip("Weapon ids eligible to spawn. Empty = the whole catalog.")]
        public List<string> allowedIds = new List<string>();

        [Tooltip("Categories eligible to spawn. Empty = any category.")]
        public List<WeaponCategory> allowedCategories = new List<WeaponCategory>();

        [Tooltip("How close a racer must get to collect a weapon, on top of its half extent.")]
        public float pickupRadius = 0.55f;

        [Tooltip("Off: attacks never target or damage a racer on the same team (team formats).")]
        public bool friendlyFire = true;

        [Header("Temporary ownership")]
        [Tooltip("Default release rule. A weapon can override it with useOwnRelease.")]
        public WeaponReleaseMode releaseMode = WeaponReleaseMode.TimeBased;

        [Tooltip("Seconds a racer keeps a TimeBased weapon before it drops automatically.")]
        public float holdDuration = 7f;

        [Tooltip("Attacks an AmmoBased weapon allows before it drops automatically.")]
        public int ammo = 5;

        [Tooltip("Seconds a dropped weapon is uncollectable by anyone.")]
        public float dropRearmDelay = 0.4f;

        [Tooltip("Extra seconds the previous owner specifically cannot re-collect it.")]
        public float repickupCooldown = 1.5f;

        public List<WeaponDefinition> catalog = new List<WeaponDefinition>();

        /// <summary>The two shipped archetypes. Used when a config leaves the catalog empty.</summary>
        /// <summary>
        /// Anime Fight Arena arsenal, on the 100 HP scale. Each entry is one kind of death; the
        /// drop weights sum to 100 so the table reads as percentages.
        /// </summary>
        public static List<WeaponDefinition> ArenaCatalog() => new List<WeaponDefinition>
        {
            new WeaponDefinition { id = "Knife", kind = WeaponKind.Orbit, category = WeaponCategory.Melee, damage = 25f, attackCooldown = 0.25f,
                orbitPeriod = 0.8f, bladeLength = 2.8f, bladeWidth = 0.4f, knockback = 1.5f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 14f, dropWeight = 18f },
            new WeaponDefinition { id = "Axe", kind = WeaponKind.Orbit, category = WeaponCategory.Melee, damage = 35f, attackCooldown = 0.8f,
                orbitPeriod = 1.4f, bladeLength = 3.0f, bladeWidth = 0.5f, knockback = 6f, staggerSeconds = 0.25f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 14f, dropWeight = 10f },
            new WeaponDefinition { id = "LaserSword", kind = WeaponKind.Orbit, category = WeaponCategory.Melee, damage = 20f, attackCooldown = 0.25f,
                orbitPeriod = 0.5f, bladeLength = 3.4f, bladeWidth = 0.35f, knockback = 1f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 10f, dropWeight = 3f },
            new WeaponDefinition { id = "Chainsaw", kind = WeaponKind.Front, category = WeaponCategory.Melee, damage = 14f, attackCooldown = 0.15f,
                bladeLength = 2.8f, bladeWidth = 0.8f, knockback = 0.2f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 14f, dropWeight = 9f },
            new WeaponDefinition { id = "Glove", kind = WeaponKind.Punch, category = WeaponCategory.Melee, damage = 20f, attackCooldown = 0.5f,
                attackRange = 2.2f, attackArc = 180f, knockback = 9f, staggerSeconds = 0.1f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 14f, dropWeight = 12f },
            new WeaponDefinition { id = "Revolver", kind = WeaponKind.Gun, category = WeaponCategory.Ranged, damage = 18f, attackCooldown = 0.7f,
                attackRange = 14f, projectileSpeed = 26f, projectileRadius = 0.16f, bounces = 2, knockback = 1.5f, requireLineOfSight = false,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 8, dropWeight = 12f },
            new WeaponDefinition { id = "Shotgun", kind = WeaponKind.Gun, category = WeaponCategory.Ranged, damage = 12f, attackCooldown = 1.2f,
                attackRange = 6f, projectileSpeed = 28f, projectileRadius = 0.14f, pellets = 5, spreadDegrees = 40f, knockback = 3f, requireLineOfSight = true,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 4, dropWeight = 10f },
            new WeaponDefinition { id = "SMG", kind = WeaponKind.Gun, category = WeaponCategory.Ranged, damage = 8f, attackCooldown = 0.1f,
                attackRange = 9f, projectileSpeed = 32f, projectileRadius = 0.12f, spreadDegrees = 15f, knockback = 0.5f, requireLineOfSight = true,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 24, dropWeight = 10f },
            new WeaponDefinition { id = "Sniper", kind = WeaponKind.Sniper, category = WeaponCategory.Ranged, damage = 60f, attackCooldown = 2f,
                attackRange = 30f, hitscan = true, pierce = 1, aimSeconds = 1.0f, knockback = 3f, staggerSeconds = 0.4f, requireLineOfSight = true,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 3, dropWeight = 6f },
            new WeaponDefinition { id = "Rocket", kind = WeaponKind.Gun, category = WeaponCategory.Ranged, damage = 70f, attackCooldown = 2.5f,
                attackRange = 16f, projectileSpeed = 18f, projectileRadius = 0.25f, blastRadius = 3.5f, blastEdgeDamage = 35f, knockback = 5f, requireLineOfSight = true,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 2, dropWeight = 5f },
            new WeaponDefinition { id = "Grenade", kind = WeaponKind.Lob, category = WeaponCategory.Ranged, damage = 50f, attackCooldown = 2f,
                attackRange = 14f, projectileSpeed = 16f, projectileRadius = 0.22f, lobbed = true, blastRadius = 4f, blastEdgeDamage = 25f, knockback = 4f, requireLineOfSight = false,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.AmmoBased, ammo = 3, dropWeight = 7f },
            new WeaponDefinition { id = "Shield", kind = WeaponKind.Shield, category = WeaponCategory.Melee, damage = 0f, attackCooldown = 99f,
                shieldHits = 3, useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 30f, dropWeight = 6f },
            new WeaponDefinition { id = "BombVest", kind = WeaponKind.Vest, category = WeaponCategory.Melee, damage = 80f, attackCooldown = 0.2f, attackRange = 1.2f, attackArc = 180f,
                fuseSeconds = 4f, blastRadius = 5f, blastEdgeDamage = 40f, knockback = 6f,
                useOwnRelease = true, releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 4f, dropWeight = 4f },
        };

        public static List<WeaponDefinition> DefaultCatalog() => new List<WeaponDefinition>
        {
            new WeaponDefinition
            {
                id = "BoneCleaver",
                category = WeaponCategory.Melee,
                damage = 42f,
                attackCooldown = 1.0f,
                attackRange = 1.9f,
                attackArc = 120f,
                color = new Color(0.95f, 0.86f, 0.55f),
                visualSize = new Vector3(0.10f, 0.28f, 1.05f),
                gripOffset = new Vector3(0f, 0f, 0.42f),
                releaseMode = WeaponReleaseMode.TimeBased,
                holdDuration = 7f
            },
            new WeaponDefinition
            {
                id = "BoltCaster",
                category = WeaponCategory.Ranged,
                damage = 26f,
                attackCooldown = 1.25f,
                attackRange = 13f,
                hitscan = false,
                projectileSpeed = 24f,
                projectileRadius = 0.18f,
                requireLineOfSight = true,
                color = new Color(0.35f, 0.85f, 1f),
                visualSize = new Vector3(0.16f, 0.22f, 0.8f),
                gripOffset = new Vector3(0f, 0f, 0.3f),
                releaseMode = WeaponReleaseMode.AmmoBased,
                ammo = 5
            }
        };
    }
}
