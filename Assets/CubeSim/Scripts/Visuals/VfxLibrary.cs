using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>Every effect the simulation can ask for. Adding one is data, not code.</summary>
    public enum VfxId
    {
        None = 0,
        MuzzleFlash = 1,
        ProjectileHitWall = 2,
        ProjectileHitRacer = 3,
        MeleeSlash = 4,
        MeleeHit = 5,
        WeaponPickup = 6,
        WeaponDrop = 7,
        RacerDeath = 8,
        CrushDeath = 9,
        GoalReached = 10,
        WallBreak = 11,
        BloodPool = 12
    }

    public enum VfxTintMode
    {
        /// <summary>Play the effect exactly as the pack authored it.</summary>
        None = 0,

        /// <summary>Recolour to the racer or weapon colour. Only for effects that survive it.</summary>
        Full = 1,

        /// <summary>Blend halfway, keeping the effect's own character.</summary>
        Accent = 2
    }

    /// <summary>
    /// Maps an effect id to an Epic Toon FX prefab plus how CubeSim should present it. Kept as a
    /// library like the racer, arena and weapon ones, so the pack stays untouched and the choice of
    /// effect is a data decision.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "CubeSim/VFX Library", order = 4)]
    public class VfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public VfxId id = VfxId.None;
            public GameObject prefab;

            [Tooltip("Uniform scale. Pack effects are authored for a first-person camera and read " +
                     "far too small from the top-down framing.")]
            public float scale = 1f;

            [Tooltip("Seconds before the instance is returned to the pool.")]
            public float lifetime = 1.5f;

            public VfxTintMode tint = VfxTintMode.None;

            [Tooltip("Height above the ground plane the effect is played at.")]
            public float heightOffset = 0.5f;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Find(VfxId id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id) return entries[i];
            }

            return null;
        }

        public void SetEntries(List<Entry> value) => entries = value;
    }
}
