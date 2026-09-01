using System;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// One monster taking part in a challenge.
    ///
    /// Owns the attempt state machine and the unit's durability; delegates movement to the motor and
    /// simulation to the ragdoll. It does not know about obstacles, islands or cameras — obstacles
    /// push a <see cref="HitInfo"/> in, and everything else observes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ChallengeUnitMotor))]
    public class ChallengeUnit : MonoBehaviour
    {
        [SerializeField] private ChallengeUnitDefinition definition;
        [SerializeField] private ChallengeUnitMotor motor;
        [SerializeField] private ChallengeUnitRagdoll ragdoll;
        [SerializeField] private Animator animator;
        [SerializeField] private CapsuleCollider bodyCollider;

        /// <summary>State names in the generated per-unit controller (see UnitAnimatorBuilder).</summary>
        private const string IdleStateName = "Idle";
        private const string MoveStateName = "Move";
        private const string RecoverStateName = "Recover";

        /// <summary>Forward metres that count as progress rather than being shoved about.</summary>
        private const float ProgressEpsilon = 0.5f;
        private const float StallSecondsBeforeFail = 4f;
        private const float GroundProbeHeight = 3f;
        private const float GroundProbeDistance = 8f;
        /// <summary>Breathing room after a stagger, so a heavy unit is not chain-shoved.</summary>
        private const float StaggerImmunity = 0.5f;

        private Rigidbody body;
        private ChallengeLane lane;

        private float stateEnteredTime;
        private float stallTimer;
        private float bestProgress;
        private float immuneUntilTime;
        private float recoverEndTime;
        private bool finalBlow;

        private Vector3 runStart;
        private Vector3 runForward = Vector3.forward;

        public ChallengeUnitDefinition Definition => definition;
        public ChallengeState State { get; private set; } = ChallengeState.Waiting;
        public ChallengeOutcomeReason OutcomeReason { get; private set; } = ChallengeOutcomeReason.None;

        /// <summary>Damage absorbed this attempt. Reaches toughness and the unit stops getting up.</summary>
        public float DamageTaken { get; private set; }
        public int MeaningfulHits { get; private set; }
        public int Recoveries { get; private set; }

        /// <summary>The arm skips units that are down, or briefly immune after standing up.</summary>
        public bool IsHittable =>
            (State == ChallengeState.Running || State == ChallengeState.Waiting) && Time.time >= immuneUntilTime;

        public event Action<ChallengeUnit> AttemptResolved;
        public event Action<ChallengeUnit, HitInfo, float> Struck;
        public event Action<ChallengeUnit> RecoveryStarted;
        public event Action<ChallengeUnit> RecoveryCompleted;

        /// <summary>Point the camera tracks — the pelvis once the ragdoll takes over.</summary>
        public Vector3 TrackedPosition =>
            ragdoll != null && ragdoll.IsActive
                ? ragdoll.CurrentCenter
                : transform.position + Vector3.up * (definition != null ? definition.height * 0.5f : 0.5f);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<ChallengeUnitMotor>();
            if (ragdoll == null) ragdoll = GetComponent<ChallengeUnitRagdoll>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
            ApplyDefinition();
        }

        /// <summary>Push the ScriptableObject values into the live components.</summary>
        public void ApplyDefinition()
        {
            if (definition == null) return;

            if (body != null)
            {
                body.mass = definition.mass;
                body.linearDamping = definition.linearDamping;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (bodyCollider != null)
            {
                bodyCollider.radius = definition.colliderRadius;
                bodyCollider.height = definition.colliderHeight;
                bodyCollider.center = new Vector3(0f, definition.colliderCenterY, 0f);
                bodyCollider.direction = 1;
            }

            motor?.Configure(definition);
        }

        public void SetDefinition(ChallengeUnitDefinition value)
        {
            definition = value;
            ApplyDefinition();
        }

        // ------------------------------------------------------------------ attempt lifecycle

        /// <summary>Park the unit at the start line, animated but not yet moving.</summary>
        public void PrepareRun(ChallengeLane challengeLane, Vector3 startPoint, Vector3 laneForward)
        {
            lane = challengeLane;

            DamageTaken = 0f;
            MeaningfulHits = 0;
            Recoveries = 0;
            finalBlow = false;
            stallTimer = 0f;
            bestProgress = 0f;
            immuneUntilTime = 0f;
            OutcomeReason = ChallengeOutcomeReason.None;

            ragdoll?.Deactivate();

            Quaternion facing = Quaternion.LookRotation(laneForward, Vector3.up);
            TeleportRoot(startPoint, facing);

            runStart = startPoint;
            runForward = laneForward.normalized;

            motor.SetLane(startPoint, laneForward);
            motor.StopRun();
            if (animator != null) animator.speed = 1f;
            PlayState(IdleStateName);

            EnterState(ChallengeState.Waiting);
        }

        public void BeginRun()
        {
            if (State != ChallengeState.Waiting) return;
            StartRunning();
        }

        // ------------------------------------------------------------------ impacts

        /// <summary>
        /// Called by obstacles. Three tiers, so there is a real middle ground between a tap and a
        /// terminal launch:
        ///
        ///   dV below hitDamageThreshold          - a shove. No damage, no knockdown.
        ///   below stabilityVelocity              - staggered. Costs durability, stays on its feet.
        ///   at or above stabilityVelocity        - knocked down and launched.
        ///
        /// The middle tier is what keeps the heavy units in character: Skeleton Giant absorbs about
        /// 5.6 m/s from this arm and stands at 5.9, so it is worn down over several strikes instead
        /// of being flattened by the first, without anything hard-coding it as the tough one.
        /// A unit always goes down on the blow that exhausts its durability, whatever the tier.
        /// </summary>
        public void ReceiveHit(in HitInfo hit)
        {
            if (!IsHittable || definition == null) return;

            Vector3 scaled = hit.Impulse * definition.knockbackMultiplier;
            float deltaV = hit.DeltaV * definition.knockbackMultiplier;

            if (deltaV < definition.hitDamageThreshold)
            {
                if (body != null && !body.isKinematic) body.AddForce(scaled, ForceMode.Impulse);
                return;
            }

            // Severity clamped so a clean strike is always worth roughly one hit of durability.
            float damage = Mathf.Clamp(deltaV / Mathf.Max(0.01f, definition.fullHitDeltaV), 0.75f, 1.25f);
            DamageTaken += damage;
            MeaningfulHits++;
            finalBlow = DamageTaken >= definition.toughness;

            Struck?.Invoke(this, hit, damage);

            // Being repeatedly clobbered is not the same as being stuck. Without this a unit the arm
            // can stagger but not knock down - Cactus Boss absorbs 5.5 m/s and stands at 6.9 - trips
            // the no-progress timer and fails as "Stalled" while it is still visibly being fought.
            // Its durability is what should resolve it.
            stallTimer = 0f;

            bool knockedDown = finalBlow || deltaV >= definition.stabilityVelocity;
            if (knockedDown)
            {
                EnterRagdoll(scaled * definition.ragdollImpulseMultiplier, hit.Point);
                return;
            }

            // Staggered: shoved hard and briefly unable to be hit again, but still upright.
            if (body != null && !body.isKinematic) body.AddForce(scaled, ForceMode.Impulse);
            immuneUntilTime = Time.time + StaggerImmunity;
        }

        public void EnterRagdoll(Vector3 impulse, Vector3 contactPoint)
        {
            if (State == ChallengeState.Ragdoll) return;

            if (ragdoll == null || !ragdoll.HasRagdoll)
            {
                if (body != null && !body.isKinematic) body.AddForce(impulse, ForceMode.Impulse);
                return;
            }

            motor.StopRun();
            ragdoll.Activate(impulse, contactPoint);
            EnterState(ChallengeState.Ragdoll);
        }

        // ------------------------------------------------------------------ verdicts

        /// <summary>The unit physically crossed the finish trigger.</summary>
        public void MarkPassed()
        {
            if (IsResolved) return;
            OutcomeReason = ChallengeOutcomeReason.ReachedFinish;
            motor.StopRun();
            EnterState(ChallengeState.Passed);
            AttemptResolved?.Invoke(this);
        }

        public void MarkFailed(ChallengeOutcomeReason reason)
        {
            if (IsResolved) return;
            OutcomeReason = reason;
            motor.StopRun();
            EnterState(ChallengeState.Failed);
            AttemptResolved?.Invoke(this);
        }

        public bool IsResolved => State == ChallengeState.Passed || State == ChallengeState.Failed;

        // ------------------------------------------------------------------ state machine

        private void EnterState(ChallengeState next)
        {
            State = next;
            stateEnteredTime = Time.time;
        }

        private float TimeInState => Time.time - stateEnteredTime;

        private void Update()
        {
            switch (State)
            {
                case ChallengeState.Running:    TickRunning();    break;
                case ChallengeState.Ragdoll:    TickRagdoll();    break;
                case ChallengeState.Recovering: TickRecovering(); break;
            }
        }

        private void StartRunning()
        {
            motor.BeginRun();
            PlayState(MoveStateName);
            stallTimer = 0f;
            EnterState(ChallengeState.Running);
        }

        private void TickRunning()
        {
            if (body == null || body.isKinematic) return;

            // Progress rather than speed: a unit shunted back and forth under the arm is never
            // stationary but is going nowhere, and a speed check misses that entirely.
            float progress = Vector3.Dot(transform.position - runStart, runForward);
            if (progress > bestProgress + ProgressEpsilon)
            {
                bestProgress = progress;
                stallTimer = 0f;
            }
            else
            {
                stallTimer += Time.deltaTime;
                if (stallTimer > StallSecondsBeforeFail)
                {
                    MarkFailed(ChallengeOutcomeReason.Stalled);
                    return;
                }
            }

            MatchAnimationToSpeed();
        }

        private void TickRagdoll()
        {
            if (definition == null) return;

            bool timedOut = TimeInState > definition.ragdollTimeout;
            bool settled = ragdoll != null && ragdoll.IsSettled() &&
                           TimeInState > definition.recoveryMinimumRagdollTime;
            if (!timedOut && !settled) return;

            Vector3 landing = ragdoll != null ? ragdoll.PelvisPosition : transform.position;

            if (finalBlow)
            {
                MarkFailed(ChallengeOutcomeReason.Overwhelmed);
                return;
            }
            if (lane != null && !lane.IsRecoverablePosition(landing))
            {
                MarkFailed(ChallengeOutcomeReason.Unrecoverable);
                return;
            }

            BeginRecovery(landing);
        }

        /// <summary>
        /// Re-root the animated body onto wherever the ragdoll came to rest, then hand control back
        /// to the Animator.
        ///
        /// The order matters. During ragdoll the root transform stays frozen where the hit landed
        /// while the bones fly on — measured over five metres apart — so the root must be moved onto
        /// the pelvis BEFORE the bind pose is restored and the Animator re-enabled, or the monster
        /// teleports the length of that gap the instant animation resumes.
        /// </summary>
        private void BeginRecovery(Vector3 landing)
        {
            Vector3 forward = lane != null ? lane.Forward : runForward;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

            float groundY = ProbeGroundHeight(landing);
            float hover = definition != null ? definition.recoverGroundOffset : 0f;
            Vector3 root = new(landing.x, groundY + hover, landing.z);

            // Deactivate first. The root Rigidbody is kinematic for the whole ragdoll, and a
            // kinematic body ignores position writes, so teleporting before this only moved the
            // transform - the body then dragged it straight back to where the hit landed.
            // Restoring the bind pose is purely local to each bone, so it does not care where the
            // root is; the root can be placed immediately afterwards, still before animation.
            ragdoll.Deactivate();                // 1. bind pose restored, root body dynamic again
            TeleportRoot(root, facing);          // 2. root moved for real, body included
            if (animator != null) animator.speed = 1f;
            PlayState(RecoverStateName);         // 3. animation resumes on an already-correct root

            // Re-anchor centring at the current progress, not the start line, so the unit drifts
            // back to the middle while advancing rather than being dragged backwards.
            Vector3 centre = lane != null ? lane.ClosestCenterPoint(root) : root;
            motor.SetLane(centre, forward);

            runStart = root;
            runForward = forward;
            bestProgress = 0f;
            stallTimer = 0f;

            float duration = Mathf.Clamp(RecoverClipLength(), 0.6f, 1.0f);
            recoverEndTime = Time.time + duration;
            immuneUntilTime = Time.time +
                Mathf.Max(duration, definition != null ? definition.recoveryImmunity : 0.95f);

            Recoveries++;
            EnterState(ChallengeState.Recovering);
            RecoveryStarted?.Invoke(this);
        }

        private void TickRecovering()
        {
            if (Time.time < recoverEndTime) return;
            RecoveryCompleted?.Invoke(this);
            StartRunning();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Move the body for real. Writing only the transform leaves an interpolated Rigidbody's own
        /// pose behind, and interpolation drags the transform back to it on the next physics step.
        /// </summary>
        private void TeleportRoot(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (body == null || body.isKinematic) return;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private float ProbeGroundHeight(Vector3 near)
        {
            Vector3 origin = near + Vector3.up * GroundProbeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeDistance,
                                ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return near.y - (ragdoll != null ? ragdoll.BindPelvisHeight : 0f);
        }

        private float RecoverClipLength()
        {
            if (animator == null || animator.runtimeAnimatorController == null || definition == null)
                return 0.7f;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == definition.recoverStateName) return clip.length;
            return 0.7f;
        }

        private void MatchAnimationToSpeed()
        {
            if (animator == null || !animator.enabled || definition == null || definition.moveSpeed <= 0.01f) return;
            float planarSpeed = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude;
            animator.speed = Mathf.Clamp(planarSpeed / definition.moveSpeed, 0.25f, 1.6f);
        }

        private void PlayState(string stateName)
        {
            if (animator == null || !animator.enabled || string.IsNullOrEmpty(stateName)) return;
            if (animator.HasState(0, Animator.StringToHash(stateName)))
                animator.Play(stateName, 0, 0f);
        }
    }
}
