using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Combat
{
    /// <summary>
    /// Maps a weapon id to its model prefab, mirroring the racer and arena libraries. A weapon
    /// definition stays a plain data record referring to a string; the asset reference lives here.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponVisualLibrary", menuName = "CubeSim/Weapon Visual Library", order = 3)]
    public class WeaponVisualLibrary : ScriptableObject
    {
        /// <summary>
        /// Per-weapon visual data. The packs have wildly different model sizes and local axes, so one
        /// universal transform cannot make a pistol read as a pistol and a spear as a spear - each
        /// weapon is tuned individually.
        /// </summary>
        [Serializable]
        public class Entry
        {
            public string id = "weapon";
            public GameObject prefab;

            [Tooltip("Base scale that normalises this pack model against the others.")]
            public float scale = 1f;

            [Tooltip("Extra multiplier when the weapon is lying on the floor as a pickup.")]
            public float pickupScale = 1f;

            [Tooltip("Extra multiplier when the weapon is carried beside a racer.")]
            public float equippedScale = 1f;

            [Tooltip("Euler rotation that lays this model flat and points it along +Z.")]
            public Vector3 orientation;

            [Tooltip("Extra rotation applied only while carried, to angle it for the top-down read.")]
            public Vector3 equippedEuler;

            [Tooltip("Offset applied after orientation, to centre the model on its anchor.")]
            public Vector3 offset;

            [Tooltip("Model used for this weapon's projectile. Empty falls back to a primitive.")]
            public GameObject projectilePrefab;

            [Tooltip("Visual size of the projectile. Independent of its gameplay radius.")]
            public float projectileVisualScale = 1f;

            public Vector3 projectileEuler;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase)) return entries[i];
            }

            return null;
        }

        public void SetEntries(List<Entry> value) => entries = value;
    }
}
