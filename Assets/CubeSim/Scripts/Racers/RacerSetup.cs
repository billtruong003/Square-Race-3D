using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Racers
{
    public enum StartDirectionMode
    {
        /// <summary>Seeded random direction, biased away from the axes.</summary>
        Random = 0,

        /// <summary>Cycles through <see cref="RacerSetup.startAngles"/>.</summary>
        Explicit = 1,

        /// <summary>Evenly fans the racers around the full circle.</summary>
        Fan = 2
    }

    public enum SpawnPlacement
    {
        /// <summary>Distribute across the declared spawn regions, round robin.</summary>
        SpawnRegions = 0,

        /// <summary>Seeded rejection sampling anywhere in the open playfield.</summary>
        OpenPlayfield = 1,

        /// <summary>
        /// A starting grid: racers are dealt round robin to the spawn areas and spaced evenly along
        /// each area's long axis. This is how the reference lines its racers up before the off.
        /// </summary>
        SpawnSlots = 2
    }

    public enum TeamAssignment
    {
        RoundRobin = 0,
        Blocks = 1,
        Random = 2
    }

    public enum RacerColorSource
    {
        /// <summary>Each racer takes the next colour from the theme palette (the reference look).</summary>
        Palette = 0,

        /// <summary>Every racer wears its team colour.</summary>
        Team = 1
    }

    /// <summary>
    /// Confirmed from the reference video: racers pass straight through each other. Bounce is kept
    /// as an opt-in so the behaviour is a config change rather than a code change.
    /// </summary>
    public enum RacerCollisionMode
    {
        /// <summary>Racers ignore each other completely. Kept for modes that want ghosting.</summary>
        PassThrough = 0,

        /// <summary>Racers collide and deflect off the contact normal at constant speed.</summary>
        Bounce = 1
    }

    /// <summary>How the racer roster is generated. Set <see cref="explicitRacers"/> to bypass generation.</summary>
    [Serializable]
    public class RacerSetup
    {
        public int count = 10;

        [Tooltip("GAMEPLAY size: edge length of the collision box in metres. Drives casts, corridor " +
                 "legality and crush rules. Changing this changes the simulation.")]
        public float cubeSize = 1f;

        public float speed = 9f;

        public float maxHealth = 100f;

        [Header("Visual")]
        [Tooltip("Id looked up in the RacerVisualLibrary. Unknown ids fall back to a coloured cube.")]
        public string visual = "Skeleton";

        [Tooltip("VISUAL size: model height as a multiple of the collision box size. Cosmetic only.")]
        public float visualHeightRatio = 1.6f;

        [Tooltip("Extra multiplier on the fitted model height. One dial to make racers read bigger " +
                 "without touching the collider.")]
        public float racerVisualScale = 1f;

        [Tooltip("Tint each model with its racer colour. Off keeps the pack's own texture - with a " +
                 "full pet roster the species already tell racers apart, and the trail, leaderboard " +
                 "and effects still carry the racer colour.")]
        public bool tintModels = true;

        [Tooltip("Scale of a weapon lying on the ground, so pickups stay legible from above.")]
        public float weaponPickupScale = 1f;

        [Tooltip("Scale of a weapon a racer is carrying.")]
        public float equippedWeaponScale = 1f;

        [Tooltip("Where a carried weapon floats relative to the racer.")]
        public WeaponAnchorSettings weaponAnchor = new WeaponAnchorSettings();

        [Tooltip("Colored ribbon behind each racer. Colour always comes from the racer colour.")]
        public TrailSettings trail = new TrailSettings();

        public StartDirectionMode startDirectionMode = StartDirectionMode.Random;

        [Tooltip("Degrees clockwise from +Z. Used when startDirectionMode is Explicit.")]
        public List<float> startAngles = new List<float>();

        [Tooltip("Keeps random start directions this many degrees away from an axis.")]
        [Range(0f, 44f)] public float minAxisAngle = 20f;

        public SpawnPlacement placement = SpawnPlacement.OpenPlayfield;

        [Tooltip("Clearance kept between a spawn point and any wall, on top of the racer half size.")]
        public float spawnClearance = 0.25f;

        public TeamAssignment teamAssignment = TeamAssignment.RoundRobin;
        public List<TeamDefinition> teams = new List<TeamDefinition>();

        [Tooltip("Palette gives every racer its own colour; Team paints them by side.")]
        public RacerColorSource colorSource = RacerColorSource.Palette;

        [Tooltip("How racers treat each other. Bounce is the default: they collide and deflect off " +
                 "the contact normal, keeping their configured speed.")]
        public RacerCollisionMode racerCollision = RacerCollisionMode.Bounce;

        [Tooltip("Master switch for racer-vs-racer contact. Off skips the broadphase entirely.")]
        public bool racerCollisionEnabled = true;

        [Tooltip("Gap left between two racers after a contact is separated, in metres.")]
        public float racerCollisionSkin = 0.02f;

        [Tooltip("Separation passes per step. More than one lets a pile of three or more settle in " +
                 "the same step instead of staying overlapped until the next one.")]
        [Range(1, 8)] public int racerCollisionIterations = 3;

        [Tooltip("Fully explicit roster. When non-empty this replaces procedural generation entirely.")]
        public List<RacerDefinition> explicitRacers = new List<RacerDefinition>();

        [Tooltip("When set, overrides count: racer i wears palette slot paletteIndices[i]. Lets a knockout format drop cubes without recolouring the survivors.")]
        public List<int> paletteIndices = new List<int>();
    }

    [Serializable]
    public class TeamDefinition
    {
        public string name = "Team";
        public Color color = Color.white;

        public TeamDefinition() { }

        public TeamDefinition(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }

    /// <summary>One racer, fully resolved. This is what the factory consumes.</summary>
    [Serializable]
    public class RacerDefinition
    {
        public string id = "racer";
        public int team = 0;
        public Vector2 spawnPosition;
        public float startAngle;

        [Tooltip("0 or less falls back to the shared RacerSetup speed.")]
        public float speed = 0f;

        [Tooltip("0 or less falls back to the shared RacerSetup cube size.")]
        public float size = 0f;

        [Tooltip("Alpha 0 falls back to the team colour.")]
        public Color colorOverride = new Color(0f, 0f, 0f, 0f);

        [Tooltip("Palette slot; -1 = by index.")]
        public int paletteIndex = -1;
    }
}
