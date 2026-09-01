using System;
using UnityEngine;

namespace CubeSim.Arena
{
    /// <summary>
    /// Broad spatial character of a generated arena. These are compositions, not just a wall count:
    /// each profile changes structure mix, wall lengths, clustering and how much empty space is
    /// deliberately reserved.
    /// </summary>
    public enum ArenaGenerationProfile
    {
        Sparse = 0,
        Medium = 1,
        Dense = 2,
        Mixed = 3,

        /// <summary>Use the raw values in <see cref="ArenaGenerationSettings"/> without profile presets.</summary>
        Custom = 4
    }

    /// <summary>The building blocks the generator composes an arena from.</summary>
    [Serializable]
    public class StructureWeights
    {
        [Tooltip("A single long wall segment.")]
        public float longWall = 1f;

        [Tooltip("Two walls meeting at a corner.")]
        public float lShape = 1f;

        [Tooltip("Three walls forming a room with one open side.")]
        public float room = 1f;

        [Tooltip("Two parallel walls forming a deliberate corridor.")]
        public float corridorPair = 1f;

        [Tooltip("A small isolated obstacle.")]
        public float block = 1f;

        public float Total => Mathf.Max(0f, longWall) + Mathf.Max(0f, lShape) + Mathf.Max(0f, room)
                              + Mathf.Max(0f, corridorPair) + Mathf.Max(0f, block);
    }

    /// <summary>
    /// The open region reserved at the arena centre. Ordinary walls are never placed inside it, so it
    /// stays a readable focal point and weapons never spawn in a useless corridor.
    /// </summary>
    [Serializable]
    public class CentralClearing
    {
        public bool enabled = true;

        [Tooltip("Half size of the reserved rectangle, in metres.")]
        public Vector2 halfExtents = new Vector2(6.5f, 5.5f);

        [Tooltip("Extra keep-out band around the clearing so walls do not crowd its edge.")]
        public float margin = 1.5f;

        public Rect Rect => Rect.MinMaxRect(-halfExtents.x, -halfExtents.y, halfExtents.x, halfExtents.y);

        public Rect KeepOutRect => Rect.MinMaxRect(
            -halfExtents.x - margin, -halfExtents.y - margin,
            halfExtents.x + margin, halfExtents.y + margin);
    }

    /// <summary>
    /// Everything the composed generator reads. A profile fills these in; setting
    /// <see cref="profile"/> to Custom leaves the authored values alone.
    /// </summary>
    [Serializable]
    public class ArenaGenerationSettings
    {
        public ArenaGenerationProfile profile = ArenaGenerationProfile.Mixed;

        [Tooltip("Upper bound on structures to place. Spacing rules may fit fewer.")]
        public int wallBudget = 24;

        [Tooltip("Min/max length of a generated wall segment, in metres.")]
        public Vector2 wallLengthRange = new Vector2(4f, 16f);

        public Vector2 wallThicknessRange = new Vector2(0.8f, 1.2f);

        [Tooltip("0 = every wall horizontal, 1 = every wall vertical, 0.5 = balanced.")]
        [Range(0f, 1f)] public float orientationBias = 0.5f;

        [Tooltip("Dense pockets that structures are drawn toward. 0 = spread evenly.")]
        public int clusterCount = 0;

        public float clusterRadius = 7f;

        [Tooltip("Share of structures pulled into a cluster rather than placed uniformly.")]
        [Range(0f, 1f)] public float clusterShare = 0.6f;

        [Tooltip("Fraction of the arena reserved as deliberately empty regions.")]
        [Range(0f, 0.8f)] public float openAreaBias = 0f;

        public StructureWeights structureWeights = new StructureWeights();

        [Header("Playable spacing")]
        [Tooltip("Explicit minimum gap between separate walls. 0 = derive from racer size.")]
        public float minimumCorridorWidth = 0f;

        [Tooltip("Derived width = racer diameter * this + safetyMargin.")]
        public float corridorWidthMultiplier = 2.6f;

        public float corridorSafetyMargin = 0.4f;

        [Tooltip("Placement tries per structure before the generator gives up on it.")]
        public int maxPlacementAttempts = 120;

        public CentralClearing centralClearing = new CentralClearing();

        /// <summary>Resolves the minimum gap that must exist between any two separate walls.</summary>
        public float ResolveMinimumCorridorWidth(float racerDiameter)
        {
            if (minimumCorridorWidth > 0f) return minimumCorridorWidth;
            return racerDiameter * Mathf.Max(1f, corridorWidthMultiplier) + Mathf.Max(0f, corridorSafetyMargin);
        }

        /// <summary>
        /// Overwrites the tunables with the preset for the selected profile. Custom is left alone so
        /// a config can hand-author every value.
        /// </summary>
        public void ApplyProfile()
        {
            switch (profile)
            {
                case ArenaGenerationProfile.Sparse:
                    wallBudget = 12;
                    wallLengthRange = new Vector2(9f, 20f);
                    wallThicknessRange = new Vector2(0.9f, 1.4f);
                    orientationBias = 0.5f;
                    clusterCount = 0;
                    openAreaBias = 0.3f;
                    structureWeights = new StructureWeights
                    { longWall = 4f, lShape = 2f, room = 0.75f, corridorPair = 0.5f, block = 1f };
                    break;

                case ArenaGenerationProfile.Medium:
                    wallBudget = 30;
                    wallLengthRange = new Vector2(5f, 14f);
                    wallThicknessRange = new Vector2(0.8f, 1.2f);
                    orientationBias = 0.5f;
                    clusterCount = 0;
                    openAreaBias = 0.12f;
                    structureWeights = new StructureWeights
                    { longWall = 2.5f, lShape = 2.5f, room = 1.5f, corridorPair = 1.5f, block = 0.75f };
                    break;

                case ArenaGenerationProfile.Dense:
                    wallBudget = 70;
                    wallLengthRange = new Vector2(3f, 8f);
                    wallThicknessRange = new Vector2(0.7f, 1f);
                    orientationBias = 0.5f;
                    clusterCount = 0;
                    openAreaBias = 0f;
                    structureWeights = new StructureWeights
                    { longWall = 1.5f, lShape = 3f, room = 2f, corridorPair = 3f, block = 1f };
                    break;

                case ArenaGenerationProfile.Mixed:
                    wallBudget = 48;
                    wallLengthRange = new Vector2(3f, 18f);
                    wallThicknessRange = new Vector2(0.7f, 1.3f);
                    orientationBias = 0.5f;
                    clusterCount = 3;
                    clusterRadius = 7f;
                    clusterShare = 0.6f;
                    openAreaBias = 0.16f;
                    structureWeights = new StructureWeights
                    { longWall = 2.5f, lShape = 2.5f, room = 1.75f, corridorPair = 2f, block = 1f };
                    break;
            }
        }
    }
}
