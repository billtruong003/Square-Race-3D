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
