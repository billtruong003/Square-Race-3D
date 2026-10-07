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
        AuthoredTrack = 1,

        /// <summary>
        /// Closes the floor cell by cell along walking distance, so it follows corridors and never
        /// cuts through a wall. See ZoneConfig for the three shapes.
        /// </summary>
        Zone = 2
    }

    public enum ZoneShape
    {
        /// <summary>Race: eats the course from the far end toward the goal; the goal is always last.</summary>
        PathChase = 0,

        /// <summary>Battle: a ring around a seeded open-floor centre, shrinking in announced phases.</summary>
        Circle = 1,

        /// <summary>Battle: floor tiles fall away in waves toward a final platform.</summary>
        Collapse = 2
    }

    [Serializable]
    public class ZoneConfig
    {
        public ZoneShape shape = ZoneShape.PathChase;

        [Tooltip("Seconds before anything closes.")]
        public float startDelay = 6f;

        [Tooltip("PathChase / Collapse: metres of walking distance the front eats per second. Ignored when closeSeconds > 0.")]
        public float speed = 1.2f;

        [Tooltip("PathChase / Collapse: seconds (after the start delay) to close the whole course, whatever its length. " +
                 "A fixed speed made long courses run for minutes; 0 = use speed.")]
        public float closeSeconds = 40f;

        [Tooltip("PathChase: floor within this walking distance of the goal never closes.")]
        public float keepNearGoal = 5f;

        [Tooltip("Circle / Collapse: walking radius around the centre that never closes.")]
        public float finalRadius = 3.5f;

        [Tooltip("Circle: number of shrink phases.")]
        public int phases = 4;

        [Tooltip("Circle: seconds each phase holds (and announces the next ring) before shrinking.")]
        public float holdSeconds = 6f;

        [Tooltip("Circle: seconds each shrink takes.")]
        public float shrinkSeconds = 5f;

        [Tooltip("Collapse: tile edge in metres.")]
        public float tileSize = 2f;

        [Tooltip("Collapse: seeded per-tile delay spread in seconds, for a ragged edge and islands.")]
        public float tileJitter = 1.5f;

        [Tooltip("Seconds of warning before a cell closes (Circle warns for the whole hold).")]
        public float warnSeconds = 2.5f;

        [Tooltip("A cube a closing cell swallows dies. Off: the zone only shoves, and only a cube with nowhere to go is crushed.")]
        public bool lethal = true;

        [Tooltip("Path grid resolution in metres.")]
        public float cellSize = 0.5f;
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

        public ZoneConfig zone = new ZoneConfig();

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
