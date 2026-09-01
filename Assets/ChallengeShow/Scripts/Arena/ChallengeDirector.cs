using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Runs one attempt at a time: pull a unit off its family island, start it down the lane, wait
    /// for a physical verdict, then put it back. Knows nothing about how the arm works or how the
    /// camera is built — it only raises events those systems listen to.
    /// </summary>
    public class ChallengeDirector : MonoBehaviour
    {
        [SerializeField] private ChallengeShowCatalog catalog;
        [SerializeField] private ChallengeLane lane;
        [SerializeField] private ChallengeUnitPool pool;
        [SerializeField] private MonsterFamilyDisplay[] familyDisplays;
        [SerializeField] private RotatingArmObstacle arm;

        [Header("Timing")]
        [Tooltip("Seconds between the unit appearing at the start line and it starting to run.")]
        [SerializeField] private float summonHoldSeconds = 1.2f;
        [Tooltip("Seconds the result is held before the unit returns to its island.")]
        [SerializeField] private float resultHoldSeconds = 2.5f;
        [Tooltip("Hard cap on a single attempt. Multi-hit runs are long, so this is a deadlock " +
                 "backstop rather than a pacing tool.")]
        [SerializeField] private float attemptTimeout = 45f;

        [Header("Sequencing")]
        [SerializeField] private bool autoRunOnStart;

        public event Action<ChallengeUnit> UnitSummoned;
        public event Action<ChallengeUnit> RunStarted;
        public event Action<ChallengeUnit> AttemptResolved;

        public ChallengeUnit ActiveUnit { get; private set; }
        public ChallengeShowCatalog Catalog => catalog;
        public ChallengeLane Lane => lane;

        private Coroutine attemptRoutine;

        /// <summary>
        /// Cleanup for the attempt currently in flight. Held as a field rather than as locals inside
        /// the coroutine so an interruption can still run it — StopCoroutine abandons a coroutine
        /// mid-body, and everything after the last yield would otherwise never execute.
        /// </summary>
        private ChallengeUnit pendingUnit;
        private Action<ChallengeUnit> pendingResolvedHandler;

        private void Start()
        {
            if (autoRunOnStart) RunWholeShow();
        }

        private void OnDisable() => CleanupCurrentAttempt();

        public void RunWholeShow()
        {
            StopAndCleanup();
            attemptRoutine = StartCoroutine(RunAllUnits());
        }

        public void RunSingle(ChallengeUnitDefinition definition)
        {
            StopAndCleanup();
            attemptRoutine = StartCoroutine(RunAttempt(definition));
        }

        /// <summary>Stop whatever is running and return the arena to a clean idle state.</summary>
        public void ResetArena()
        {
            StopAndCleanup();
            pool?.ReleaseAll();
            arm?.ForgetAllUnits();
            foreach (var display in familyDisplays)
                if (display != null) display.RestoreAll();
        }

        private void StopAndCleanup()
        {
            if (attemptRoutine != null) StopCoroutine(attemptRoutine);
            attemptRoutine = null;
            CleanupCurrentAttempt();
        }

        /// <summary>
        /// The single authoritative teardown for one attempt. Safe to call twice; runs from the
        /// coroutine's finally block on the normal path and from StopAndCleanup on interruption.
        /// </summary>
        private void CleanupCurrentAttempt()
        {
            if (pendingUnit == null)
            {
                ActiveUnit = null;
                return;
            }

            if (pendingResolvedHandler != null)
            {
                pendingUnit.AttemptResolved -= pendingResolvedHandler;
                pendingResolvedHandler = null;
            }

            arm?.ForgetUnit(pendingUnit);
            pool?.Release(pendingUnit);
            if (pendingUnit.Definition != null) SetDisplayPresence(pendingUnit.Definition, true);

            pendingUnit = null;
            ActiveUnit = null;
        }

        private IEnumerator RunAllUnits()
        {
            foreach (var definition in EnumerateRoster())
                yield return RunAttempt(definition);
            attemptRoutine = null;
        }

        public IEnumerable<ChallengeUnitDefinition> EnumerateRoster()
        {
            if (catalog == null) yield break;
            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                foreach (var unit in family.ValidUnits) yield return unit;
            }
        }

        private IEnumerator RunAttempt(ChallengeUnitDefinition definition)
        {
            if (definition == null || lane == null || pool == null) yield break;

            ChallengeUnit unit = pool.Acquire(definition);
            if (unit == null) yield break;

            bool resolved = false;
            void OnResolved(ChallengeUnit u) => resolved = true;

            try
            {
                pendingUnit = unit;
                ActiveUnit = unit;
                arm?.ForgetUnit(unit);
                SetDisplayPresence(definition, false);

                unit.PrepareRun(lane, lane.StartPosition, lane.Forward);
                UnitSummoned?.Invoke(unit);
                yield return new WaitForSeconds(summonHoldSeconds);

                pendingResolvedHandler = OnResolved;
                unit.AttemptResolved += OnResolved;

                unit.BeginRun();
                RunStarted?.Invoke(unit);

                float deadline = Time.time + attemptTimeout;
                while (!resolved && Time.time < deadline) yield return null;

                if (!resolved) unit.MarkFailed(ChallengeOutcomeReason.TimedOut);

                LogResult(unit);
                AttemptResolved?.Invoke(unit);

                yield return new WaitForSeconds(resultHoldSeconds);
            }
            finally
            {
                CleanupCurrentAttempt();
            }
        }

        private void SetDisplayPresence(ChallengeUnitDefinition definition, bool present)
        {
            foreach (var display in familyDisplays)
            {
                if (display == null || !display.Contains(definition)) continue;
                display.SetUnitPresent(definition, present);
                return;
            }
        }

        private void LogResult(ChallengeUnit unit)
        {
            float progress = lane.ProgressAlongLane(unit.TrackedPosition);
            Debug.Log($"[ChallengeShow] {unit.Definition.displayName} ({unit.Definition.SizeClass}) -> " +
                      $"{unit.State} ({unit.OutcomeReason}) at {progress:0.0}m / {lane.Length:0.0}m | " +
                      $"hits {unit.MeaningfulHits}, damage {unit.DamageTaken:0.00}/{unit.Definition.toughness:0.0}, " +
                      $"recoveries {unit.Recoveries}");
        }
    }
}
