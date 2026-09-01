using System;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// How one arena is composed out of baked environment macros.
    ///
    /// This replaces the previous model, where the whole map lived as hard-coded coordinates inside
    /// one 1000-line builder that placed several hundred individual tiles. A map is now a short list
    /// of macro references: lane sections chained head to tail through their entrance/exit sockets,
    /// plus a handful of explicitly positioned bastions and landmarks.
    ///
    /// Deliberately not a procedural generator. The point is cheap composition and legible diffs -
    /// swapping an obstacle section or reordering the lane should be an inspector edit, not a code
    /// change.
    /// </summary>
    [CreateAssetMenu(menuName = "Challenge Show/Arena Recipe", fileName = "ArenaRecipe")]
    public class ArenaRecipe : ScriptableObject
    {
        /// <summary>One section of the main lane. Sections abut via their sockets, in array order.</summary>
        [Serializable]
        public struct LaneSection
        {
            public GameObject macro;
            [Tooltip("Why this section is here - the lane's visual rhythm, in one word or two.")]
            public string beat;
        }

        /// <summary>A macro placed at an explicit spot rather than chained into the lane.</summary>
        [Serializable]
        public struct Placement
        {
            public string label;
            public GameObject macro;
            public Vector3 position;
            public float yaw;
            [Tooltip("Uniform scale. Distant macros are enlarged so their contents stay readable.")]
            public float scale;
        }

        [Tooltip("World Z where the first lane section's entrance socket is placed.")]
        public float laneStartZ = -6f;

        [Tooltip("Lane sections in order, chained entrance-to-exit from laneStartZ.")]
        public LaneSection[] lane = Array.Empty<LaneSection>();

        [Tooltip("Family bastions. Order must match the catalog's family order.")]
        public Placement[] bastions = Array.Empty<Placement>();

        [Tooltip("Landmarks positioned independently of the lane chain.")]
        public Placement[] landmarks = Array.Empty<Placement>();
    }
}
