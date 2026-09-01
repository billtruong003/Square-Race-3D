using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Combat;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Core
{
    /// <summary>
    /// The complete description of one simulation episode. Everything an automated agent needs to
    /// vary lives here; no gameplay values are hidden in MonoBehaviour inspectors.
    /// Plain [Serializable] (not a ScriptableObject) so it round-trips through JsonUtility.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        public int seed = 20260831;
        public SimulationSettings simulation = new SimulationSettings();
        public ArenaDefinition arena = new ArenaDefinition();
        public RacerSetup racers = new RacerSetup();
        public PressureConfig pressure = new PressureConfig();
        public WeaponConfig weapons = new WeaponConfig();
        public VisualTheme visuals = new VisualTheme();
        public CameraDefinition camera = new CameraDefinition();
        public EndRules endRules = new EndRules();

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        public static SimulationConfig FromJson(string json) => JsonUtility.FromJson<SimulationConfig>(json);

        public SimulationConfig Clone() => FromJson(ToJson(false));
    }

    [Serializable]
    public class SimulationSettings
    {
        [Tooltip("Simulation step. The runner ticks in FixedUpdate, so this is the determinism grid.")]
        public float fixedTimeStep = 1f / 60f;

        [Tooltip("Apply fixedTimeStep to Time.fixedDeltaTime when the simulation starts.")]
        public bool applyFixedTimeStep = true;

        [Tooltip("Contact offset kept between a racer and any surface. Prevents re-hitting the same face.")]
        public float skinWidth = 0.02f;

        [Tooltip("Maximum reflections resolved within a single step. Guards against corner ping-pong.")]
        public int maxCollisionIterations = 6;

        [Tooltip("Y coordinate of the gameplay plane. Racers never leave it.")]
        public float groundY = 0f;
    }

    public enum WinCondition
    {
        /// <summary>The episode ends when one racer is left standing.</summary>
        LastAlive = 0,

        /// <summary>The episode ends when a racer reaches a goal area.</summary>
        ReachGoal = 1,

        /// <summary>No win check; the run stops only on the duration limit.</summary>
        None = 2
    }

    [Serializable]
    public class EndRules
    {
        public WinCondition winCondition = WinCondition.LastAlive;

        [Tooltip("Backstop so an episode always terminates. 0 = no limit.")]
        public float maxDuration = 300f;

        [Tooltip("Restart automatically once the episode finishes.")]
        public bool loopOnEnd = false;

        [Tooltip("Seconds to hold on the result before an automatic restart.")]
        public float resultHoldTime = 3f;

        [Tooltip("Advance the seed by 1 on each automatic restart, so a loop produces new episodes.")]
        public bool advanceSeedOnLoop = true;

        [Tooltip("ReachGoal only: finishers needed before the run ends. 1 = first past the post.")]
        public int requiredFinishers = 1;
    }

    [Serializable]
    public class CameraDefinition
    {
        public bool orthographic = false;
        [Range(10f, 90f)] public float fieldOfView = 42f;

        [Tooltip("0 = straight down. Small values add readable 3D depth without changing gameplay.")]
        [Range(0f, 45f)] public float tiltDegrees = 0f;

        [Tooltip("Extra framing room around the arena bounds. 1 = exact fit.")]
        public float margin = 1.08f;

        [Tooltip("Above 0, overrides the height derived from arena bounds.")]
        public float heightOverride = 0f;

        public Color backgroundColor = new Color(0.09f, 0.09f, 0.10f, 1f);
    }
}
