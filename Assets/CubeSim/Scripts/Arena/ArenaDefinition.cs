using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Arena
{
    /// <summary>
    /// Where an arena's geometry comes from. Deliberately designed courses must not be forced
    /// through the random generator, so both paths are first class.
    /// </summary>
    public enum ArenaMode
    {
        Procedural = 0,
        Authored = 1
    }

    public enum ArenaLayoutMode
    {
        /// <summary>Only the border plus whatever is listed in <see cref="ArenaDefinition.extraWalls"/>.</summary>
        Explicit = 0,

        /// <summary>Composed structures driven by <see cref="ArenaDefinition.generation"/>.</summary>
        Generated = 1
    }

    /// <summary>
    /// Pure data description of the playfield. Coordinates are arena-local metres on the XZ plane,
    /// with (0,0) at the arena centre.
    /// </summary>
    [Serializable]
    public class ArenaDefinition
    {
        public ArenaMode mode = ArenaMode.Procedural;

        [Tooltip("Id of the authored arena prefab to load. Used when mode is Authored.")]
        public string arenaId = "";

        [Tooltip("Full playfield size in metres: x = width (X axis), y = depth (Z axis). Procedural only.")]
        public Vector2 size = new Vector2(52f, 30f);

        public float wallHeight = 2.4f;

        [Tooltip("Thickness of the border walls. Generated walls use the generation thickness range.")]
        public float wallThickness = 1.2f;

        public float floorThickness = 0.5f;

        [Tooltip("Generate the four enclosing border walls.")]
        public bool generateBorder = true;

        public ArenaLayoutMode layout = ArenaLayoutMode.Generated;
        public ArenaGenerationSettings generation = new ArenaGenerationSettings();

        [Tooltip("Always built, in addition to whatever the layout mode generates.")]
        public List<WallDefinition> extraWalls = new List<WallDefinition>();

        [Tooltip("Racers are distributed inside these. Empty = the whole open playfield is used.")]
        public List<SpawnRegion> spawnRegions = new List<SpawnRegion>();

        [Tooltip("Flat coloured markers. Visual only.")]
        public List<ZoneDefinition> zones = new List<ZoneDefinition>();

        public float HalfWidth => size.x * 0.5f;
        public float HalfDepth => size.y * 0.5f;

        /// <summary>Inner playable rectangle, i.e. the arena minus the border walls.</summary>
        public Rect PlayableRect
        {
            get
            {
                float inset = generateBorder ? wallThickness : 0f;
                return Rect.MinMaxRect(
                    -HalfWidth + inset, -HalfDepth + inset,
                    HalfWidth - inset, HalfDepth - inset);
            }
        }
    }

    [Serializable]
    public class WallDefinition
    {
        public Vector2 center;
        public Vector2 size = Vector2.one;

        public WallDefinition() { }

        public WallDefinition(Vector2 center, Vector2 size)
        {
            this.center = center;
            this.size = size;
        }
    }

    [Serializable]
    public class SpawnRegion
    {
        public string id = "region";
        public Vector2 center;
        public Vector2 size = new Vector2(4f, 12f);
    }

    [Serializable]
    public class ZoneDefinition
    {
        public string id = "zone";
        public Vector2 center;
        public Vector2 size = new Vector2(6f, 3f);
        public Color color = Color.green;
        public float emission = 0.35f;
    }
}
