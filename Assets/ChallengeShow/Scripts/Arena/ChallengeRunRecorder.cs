using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Records what actually happened during each attempt: how far the unit got, every meaningful
    /// strike, each recovery, wall impacts, and how the run ended.
    ///
    /// The show's premise is that outcomes are earned rather than scripted, so the outcomes need to
    /// be measured. Doubles as the episode log for editing.
    /// </summary>
    public class ChallengeRunRecorder : MonoBehaviour
    {
        public enum EventKind
        {
            AttemptStart,
            Hit,
            RagdollStart,
            WallImpact,
            RecoveryStart,
            RecoveryComplete,
            Passed,
            Failed
        }

        public struct RunEvent
        {
            public float time;
            public EventKind kind;
            public float progress;
            public float value;      // dV for Hit, damage total after the hit, impulse for WallImpact
            public string note;
        }

        public class RunLog
        {
            public string unitName;
            public string sizeClass;
            public float mass;
            public float toughness;
            public ChallengeState finalState;
            public ChallengeOutcomeReason reason;
            public float maxProgress;
            public float finalProgress;
            public float laneLength;
            public int meaningfulHits;
            public int recoveries;
            public float damageTaken;
            public float strongestHitDeltaV;
            public bool hitWall;
            public int gooSplatters;
            public float duration;
            public readonly List<RunEvent> events = new();
        }

        [SerializeField] private ChallengeDirector director;
        [SerializeField] private ChallengeLane lane;
        [SerializeField] private RotatingArmObstacle arm;

        private readonly List<RunLog> completed = new();
        private RunLog current;
        private ChallengeUnit tracked;
        private float startTime;

        public IReadOnlyList<RunLog> Completed => completed;

        private void OnEnable()
        {
            if (director != null)
            {
                director.UnitSummoned += OnSummoned;
                director.AttemptResolved += OnResolved;
            }
            if (arm != null) arm.UnitStruck += OnStruck;
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.UnitSummoned -= OnSummoned;
                director.AttemptResolved -= OnResolved;
            }
            if (arm != null) arm.UnitStruck -= OnStruck;
            Unsubscribe(tracked);
        }

        private void OnSummoned(ChallengeUnit unit)
        {
            Unsubscribe(tracked);
            tracked = unit;
            startTime = Time.time;

            current = new RunLog
            {
                unitName = unit.Definition.displayName,
                sizeClass = unit.Definition.SizeClass,
                mass = unit.Definition.mass,
                toughness = unit.Definition.toughness,
                laneLength = lane != null ? lane.Length : 0f
            };
            Log(EventKind.AttemptStart, 0f, unit.Definition.displayName);

            unit.RecoveryStarted += OnRecoveryStarted;
            unit.RecoveryCompleted += OnRecoveryCompleted;
        }

        private void Unsubscribe(ChallengeUnit unit)
        {
            if (unit == null) return;
            unit.RecoveryStarted -= OnRecoveryStarted;
            unit.RecoveryCompleted -= OnRecoveryCompleted;
        }

        private void OnStruck(ChallengeUnit unit, HitInfo hit)
        {
            if (current == null || unit != tracked) return;
            current.strongestHitDeltaV = Mathf.Max(current.strongestHitDeltaV, hit.DeltaV);
            Log(EventKind.Hit, hit.DeltaV, $"dV {hit.DeltaV:0.0} at {hit.ObstacleAngle:0}deg");
            Log(EventKind.RagdollStart, unit.DamageTaken, $"damage now {unit.DamageTaken:0.00}");
        }

        /// <summary>Called by the crystal wall so the log knows whether the launch reached it.</summary>
        public void RecordWallImpact(ChallengeUnit unit, float impulse, bool splattered)
        {
            if (current == null || unit != tracked) return;
            current.hitWall = true;
            if (splattered) current.gooSplatters++;
            Log(EventKind.WallImpact, impulse, splattered ? "splatter" : "contact");
        }

        private void OnRecoveryStarted(ChallengeUnit unit) => Log(EventKind.RecoveryStart, unit.DamageTaken, null);
        private void OnRecoveryCompleted(ChallengeUnit unit) => Log(EventKind.RecoveryComplete, unit.Recoveries, null);

        private void FixedUpdate()
        {
            if (current == null || tracked == null || lane == null) return;
            float progress = lane.ProgressAlongLane(tracked.TrackedPosition);
            current.maxProgress = Mathf.Max(current.maxProgress, progress);
            current.finalProgress = progress;
        }

        private void OnResolved(ChallengeUnit unit)
        {
            if (current == null) return;

            current.finalState = unit.State;
            current.reason = unit.OutcomeReason;
            current.meaningfulHits = unit.MeaningfulHits;
            current.recoveries = unit.Recoveries;
            current.damageTaken = unit.DamageTaken;
            current.duration = Time.time - startTime;
            Log(unit.State == ChallengeState.Passed ? EventKind.Passed : EventKind.Failed,
                current.finalProgress, unit.OutcomeReason.ToString());

            completed.Add(current);
            current = null;
            Unsubscribe(tracked);
            tracked = null;
        }

        private void Log(EventKind kind, float value, string note)
        {
            if (current == null) return;
            current.events.Add(new RunEvent
            {
                time = Time.time - startTime,
                kind = kind,
                progress = lane != null && tracked != null ? lane.ProgressAlongLane(tracked.TrackedPosition) : 0f,
                value = value,
                note = note
            });
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("UNIT           | RESULT | REASON        | HITS | DMG/TOUGH | REC | MAX/LEN     | WALL | GOO | SECS");
            foreach (var r in completed)
                sb.AppendLine($"{r.unitName,-14} | {(r.finalState == ChallengeState.Passed ? "PASS  " : "FAIL  ")} | " +
                              $"{r.reason,-13} | {r.meaningfulHits,4} | {r.damageTaken,4:0.00}/{r.toughness,-4:0.0} | " +
                              $"{r.recoveries,3} | {r.maxProgress,5:0.0}/{r.laneLength,-5:0.0} | " +
                              $"{(r.hitWall ? "yes" : "no "),-4} | {r.gooSplatters,3} | {r.duration,4:0.0}");
            return sb.ToString();
        }

        public string BuildTimeline(int index)
        {
            if (index < 0 || index >= completed.Count) return "no such run";
            var r = completed[index];
            var sb = new StringBuilder();
            sb.AppendLine($"=== {r.unitName} timeline ===");
            foreach (var e in r.events)
                sb.AppendLine($"  {e.time,5:0.00}s  {e.kind,-16} progress {e.progress,6:0.0}m  {e.value,6:0.00}  {e.note}");
            return sb.ToString();
        }

        public void Clear() => completed.Clear();
    }
}
