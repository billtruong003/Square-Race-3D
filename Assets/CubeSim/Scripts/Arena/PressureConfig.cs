using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Arena
{
    public enum PressureSide
    {
        Left = 0,   // advances along +X from the -X edge
        Right = 1,  // advances along -X from the +X edge
        Back = 2,   // advances along +Z from the -Z edge
        Front = 3   // advances along -Z from the +Z edge
    }

    public enum PressureMode
    {
        /// <summary>Straight slabs closing in from the arena edges.</summary>
        LinearSlabs = 0,

        /// <summary>Follows the PressureTrack the authored map declares.</summary>
        AuthoredTrack = 1
    }

    /// <summary>
    /// The shrinking playfield. Linear slabs are axis-aligned half spaces marching inward; an
    /// authored track fills a designed route segment by segment. Racers treat either as solid.
    /// </summary>
    [Serializable]
    public class PressureConfig
    {
        public bool enabled = true;

        public PressureMode mode = PressureMode.LinearSlabs;

        [Tooltip("How far a slab extends past the arena edge. Small - it only has to hide its own back face.")]
        public float overhang = 1.5f;

        [Tooltip("Slab height. Taller than the walls so the boundary reads clearly from above.")]
        public float height = 3.2f;

        [Header("Authored track overrides")]
        [Tooltip("Multiplies every segment's speed. 0 = use the value on the PressureTrack itself.")]
        public float trackSpeedScale = 0f;

        [Tooltip("Seconds before the route starts. Negative = use the value on the PressureTrack.")]
        public float trackStartDelay = -1f;

        public List<PressureSlabConfig> slabs = new List<PressureSlabConfig>
        {
            new PressureSlabConfig { side = PressureSide.Left },
            new PressureSlabConfig { side = PressureSide.Right }
        };
    }

    [Serializable]
    public class PressureSlabConfig
    {
        public PressureSide side = PressureSide.Left;

        [Tooltip("Boundary distance from the arena edge at t=0, in metres.")]
        public float startInset = 0f;

        [Tooltip("Boundary distance from the arena edge the slab stops at, in metres.")]
        public float targetInset = 20f;

        [Tooltip("Seconds of run time before the slab starts advancing.")]
        public float startDelay = 4f;

        [Tooltip("Inward advance rate in metres per second.")]
        public float speed = 0.16f;
    }
}
