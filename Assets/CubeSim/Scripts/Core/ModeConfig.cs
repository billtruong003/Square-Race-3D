using System;
using UnityEngine;

namespace CubeSim.Core
{
    /// <summary>Which extra rule set rides on top of the base simulation this round.</summary>
    public enum ModeKind
    {
        None = 0,
        /// <summary>One cube starts infected; touch spreads it; the last clean cube wins.</summary>
        Infection = 1,
        /// <summary>A bomb passes by touch and explodes on its holder; last alive wins.</summary>
        HotPotato = 2,
        /// <summary>Crates drop seeded loot; no weapons on the floor at start.</summary>
        LuckyBlock = 3,
        /// <summary>Racers paint the floor they cross; most tiles when the clock ends wins.</summary>
        PaintWar = 4,
    }

    /// <summary>
    /// Tunables for the touch-and-position modes. Everything here feeds a deterministic system
    /// inside the runner; nothing reads Unity's random or physics beyond the wall queries.
    /// </summary>
    [Serializable]
    public class ModeConfig
    {
        public ModeKind kind = ModeKind.None;

        [Tooltip("Optional rule line for the HUD strip and round card. Empty = derived from the win condition.")]
        public string ruleLabel = "";

        [Header("Infection")]
        [Tooltip("Seconds before patient zero is chosen.")]
        public float infectionStart = 4f;
        [Tooltip("Speed multiplier for infected cubes, so the last clean cube cannot outrun them forever.")]
        public float infectedSpeedScale = 1.08f;
        [Tooltip("Seconds a freshly infected cube needs before it can spread - the 15-second wipeouts came from instant chains.")]
        public float infectionIncubation = 3f;
        [Tooltip("Seconds an infected cube must wait between two bites.")]
        public float infectionBiteCooldown = 3f;
        [Tooltip("Bites a clean cube takes before it turns. Each bite costs a heart, so the HUD hearts double as the infection meter.")]
        public int infectionBitesToTurn = 2;

        [Header("Hot Potato")]
        public float bombFuse = 6f;
        public float bombRespawnDelay = 2f;
        public float bombBlastRadius = 3f;
        public float bombBlastDamage = 1f;
        [Tooltip("Seconds during which the bomb cannot be passed straight back to the cube that just handed it over.")]
        public float bombPassLockout = 1f;
        [Tooltip("Speed multiplier while holding the bomb: the panicked holder bumps into more cubes, so the bomb travels.")]
        public float bombHolderSpeedScale = 1.3f;

        [Header("Lucky Block (weights, any scale)")]
        public float lootKnife = 30f;
        public float lootPotion = 20f;
        public float lootShield = 15f;
        public float lootBoost = 15f;
        public float lootBomb = 20f;
        public float lootBoostSeconds = 5f;
        public float lootBoostScale = 1.6f;

        [Header("Paint War")]
        public float paintTileSize = 1.42f;
    }
}
