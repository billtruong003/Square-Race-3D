using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Play-mode watchdog for the movement and constraint systems. Catches the failures that are
    /// easy to miss by eye: a racer embedded in a wall, a racer swallowed by an advancing pressure
    /// slab, a racer that left the XZ plane, or a direction that drifted off unit length.
    ///
    /// A racer embedded in a wall is now a hard failure, not an accepted transient - the constraint
    /// solver is supposed to either free it or eliminate it in the same step.
    /// </summary>
    [RequireComponent(typeof(SimulationBootstrap))]
    public class SimulationValidator : MonoBehaviour
    {
        [Tooltip("Simulation steps between checks. 1 = every step.")]
        [SerializeField] private int stepInterval = 1;

        [SerializeField] private int maxLoggedViolations = 8;

        public int WallEmbedViolations { get; private set; }

        /// <summary>Steps that ended with two living racers still overlapping each other.</summary>
        public int RacerOverlapViolations { get; private set; }

        /// <summary>Racer-vs-racer contacts the simulation resolved, read straight off the runner.</summary>
        public int RacerCollisionCount { get; private set; }

        /// <summary>Deepest racer-vs-racer overlap seen at the end of a step, in metres.</summary>
        public float MaxRacerPenetration { get; private set; }
        public int PressureViolations { get; private set; }
        public int PlaneViolations { get; private set; }
        public int DirectionViolations { get; private set; }
        public int Checks { get; private set; }

        /// <summary>Longest run of consecutive checks a single racer spent inside a wall.</summary>
        public int LongestEmbedStreak { get; private set; }

        private SimulationBootstrap _bootstrap;
        private readonly Collider[] _overlap = new Collider[8];
        private int[] _embedStreak;
        private int _stepCounter;
        private int _logged;

        private void Awake() => _bootstrap = GetComponent<SimulationBootstrap>();

        private void FixedUpdate()
        {
            SimulationRunner runner = _bootstrap.Runner;
            if (runner == null) return;

            // Once the episode ends the pressure stops advancing and the survivor is frozen wherever
            // it stood, so it can sit inside a slab forever. Checking past the finish counts that as
            // a violation every step and buries the real numbers.
            if (runner.Finished) return;

            if (++_stepCounter < Mathf.Max(1, stepInterval)) return;
            _stepCounter = 0;

            Racer[] racers = runner.Racers;
            if (_embedStreak == null || _embedStreak.Length != racers.Length) _embedStreak = new int[racers.Length];

            float groundY = _bootstrap.ActiveConfig.simulation.groundY;
            float skin = _bootstrap.ActiveConfig.simulation.skinWidth;
            int wallMask = SimulationLayers.WallMask;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.Alive || racer.Retired) { _embedStreak[i] = 0; continue; }

                Checks++;

                // The mover keeps a skin gap, so any real overlap means a resolution failure.
                float probe = Mathf.Max(0.01f, racer.HalfExtent - skin * 2f);
                int count = Physics.OverlapBoxNonAlloc(racer.Position, new Vector3(probe, probe, probe),
                    _overlap, Quaternion.identity, wallMask, QueryTriggerInteraction.Ignore);

                if (count > 0)
                {
                    WallEmbedViolations++;
                    _embedStreak[i]++;
                    LongestEmbedStreak = Mathf.Max(LongestEmbedStreak, _embedStreak[i]);
                    Report($"racer {racer.Id} is embedded in {_overlap[0].name} at {racer.Position} " +
                           $"(streak {_embedStreak[i]}, t={runner.ElapsedTime:F2}s)");
                    for (int c = 0; c < count; c++) _overlap[c] = null;
                }
                else
                {
                    _embedStreak[i] = 0;
                }

                if (!runner.Pressure.IsInsideBounds(racer.Position, racer.HalfExtent))
                {
                    PressureViolations++;
                    Report($"racer {racer.Id} is outside the pressure bounds at {racer.Position}");
                }

                float expectedY = groundY + racer.HalfExtent;
                if (Mathf.Abs(racer.Position.y - expectedY) > 1e-3f)
                {
                    PlaneViolations++;
                    Report($"racer {racer.Id} left the XZ plane: y={racer.Position.y} expected {expectedY}");
                }

                if (Mathf.Abs(racer.Direction.magnitude - 1f) > 1e-3f || Mathf.Abs(racer.Direction.y) > 1e-4f)
                {
                    DirectionViolations++;
                    Report($"racer {racer.Id} has a non-unit planar direction: {racer.Direction}");
                }
            }

            CheckRacerOverlap(runner, racers);
        }

        /// <summary>
        /// Confirms the contact pass actually finished its job. The contact grid separates pairs, but
        /// the constraint solver runs afterwards and can push a racer back toward another one, so the
        /// only honest place to check for a leftover overlap is at the very end of the step.
        ///
        /// This is a validation pass, not simulation, so the plain O(n^2) sweep is fine - it never
        /// runs in a shipped episode.
        /// </summary>
        private void CheckRacerOverlap(SimulationRunner runner, Racer[] racers)
        {
            RacerCollisionCount = runner.RacerContactCount;
            if (!runner.RacerCollisionEnabled) return;

            // Allow the separation skin plus the mover's own skin before calling it an overlap.
            float tolerance = _bootstrap.ActiveConfig.racers.racerCollisionSkin +
                              _bootstrap.ActiveConfig.simulation.skinWidth + 1e-3f;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer a = racers[i];
                if (!a.IsActive) continue;

                for (int j = i + 1; j < racers.Length; j++)
                {
                    Racer b = racers[j];
                    if (!b.IsActive) continue;

                    float dx = b.Position.x - a.Position.x;
                    float dz = b.Position.z - a.Position.z;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    float penetration = a.HalfExtent + b.HalfExtent - distance;
                    if (penetration <= tolerance) continue;

                    MaxRacerPenetration = Mathf.Max(MaxRacerPenetration, penetration);
                    RacerOverlapViolations++;
                    Report($"racers {a.Id} and {b.Id} still overlap by {penetration:F3}m " +
                           $"at t={runner.ElapsedTime:F2}s");
                }
            }
        }

        private void Report(string message)
        {
            if (_logged >= maxLoggedViolations) return;
            _logged++;
            Debug.LogWarning("[CubeSim.Validator] " + message);
        }

        public string Summary()
            => $"checks={Checks} wallEmbed={WallEmbedViolations} longestEmbedStreak={LongestEmbedStreak} " +
               $"pressure={PressureViolations} plane={PlaneViolations} direction={DirectionViolations} " +
               $"racerOverlap={RacerOverlapViolations} racerContacts={RacerCollisionCount} " +
               $"maxRacerPenetration={MaxRacerPenetration:F3}";
    }
}
