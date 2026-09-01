using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// The sweeping arm. A kinematic pivot rotates around its own LOCAL X; the arm mesh hangs off
    /// that pivot at a radial offset, so local-X rotation produces a windmill sweep across the lane.
    ///
    /// Hits are found with an explicit capsule overlap each physics step rather than trigger events.
    /// A trigger on a fast-rotating collider can tunnel straight past a small unit; a swept overlap
    /// along the arm's length cannot, and it makes the impulse fully authored instead of leaving it
    /// to a kinematic-vs-dynamic contact resolution we cannot tune.
    /// </summary>
    public class RotatingArmObstacle : MonoBehaviour
    {
        public enum AngularMode
        {
            Continuous,
            PingPong
        }

        [Header("Pivot")]
        [Tooltip("Child that actually rotates. Rotation is applied around this transform's LOCAL X.")]
        [SerializeField] private Transform pivot;
        [SerializeField] private Rigidbody pivotBody;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 110f;
        [Tooltip("+1 sweeps down the lane, -1 sweeps back up it.")]
        [SerializeField] private int direction = 1;
        [SerializeField] private float startAngle = 270f;
        [SerializeField] private AngularMode angularMode = AngularMode.Continuous;
        [Tooltip("PingPong only: the arc swept, centred on startAngle.")]
        [SerializeField] private float pingPongArc = 140f;

        [Header("Strike Volume")]
        [Tooltip("Local-space start of the striking segment, measured on the pivot.")]
        [SerializeField] private Vector3 strikeLocalStart = new(0f, 0f, 1.2f);
        [Tooltip("Local-space end of the striking segment — the hand.")]
        [SerializeField] private Vector3 strikeLocalEnd = new(0f, 0f, 5.2f);
        [SerializeField] private float strikeRadius = 0.85f;
        [SerializeField] private LayerMask unitLayers = ~0;

        [Header("Impact")]
        [Tooltip("Base impulse magnitude before the unit's own knockback multiplier is applied.")]
        [SerializeField] private float impactStrength = 260f;
        [Tooltip("Extra upward lift so units arc instead of scraping along the lane.")]
        [SerializeField] private float upwardBias = 0.45f;
        [Tooltip("Mass the base impulse is tuned against.")]
        [SerializeField] private float referenceMass = 40f;
        [Tooltip("0 = fixed impulse, so heavy units barely move and light ones are flung off-screen. " +
                 "1 = fixed speed change, so mass stops mattering at all. Between the two, heavier " +
                 "units still travel less but every unit stays in a filmable range.")]
        [Range(0f, 1f)]
        [SerializeField] private float massCompensation = 0.8f;
        [Tooltip("Seconds before the same unit can be struck again.")]
        [SerializeField] private float perUnitCooldown = 0.6f;
        [Tooltip("Log every strike with the resulting speed change. Useful while tuning the show.")]
        [SerializeField] private bool logStrikes = true;

        /// <summary>Raised for every meaningful contact, before the unit reacts.</summary>
        public event Action<ChallengeUnit, HitInfo> UnitStruck;

        private readonly Dictionary<ChallengeUnit, float> lastHitTime = new();
        private readonly Collider[] overlapBuffer = new Collider[16];
        private float angle;
        private float pingPongTime;

        public float CurrentAngle => angle;
        public Transform Pivot => pivot;

        /// <summary>Angular speed in rad/s about the pivot's local X, signed by direction.</summary>
        public float SignedAngularSpeedRad => rotationSpeed * Mathf.Sign(direction) * Mathf.Deg2Rad;

        private void Reset() => pivot = transform.childCount > 0 ? transform.GetChild(0) : transform;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
            if (pivotBody == null) pivotBody = pivot.GetComponent<Rigidbody>();
            angle = startAngle;
            ApplyRotation();
        }

        /// <summary>
        /// Drop remembered cooldowns for a unit. Instances are pooled and reused across attempts, so
        /// without this a re-summoned unit can inherit a cooldown from its previous run.
        /// </summary>
        public void ForgetUnit(ChallengeUnit unit)
        {
            if (unit != null) lastHitTime.Remove(unit);
        }

        public void ForgetAllUnits() => lastHitTime.Clear();

        private void FixedUpdate()
        {
            AdvanceAngle(Time.fixedDeltaTime);
            ApplyRotation();
            DetectStrikes();
        }

        private void AdvanceAngle(float dt)
        {
            if (angularMode == AngularMode.Continuous)
            {
                angle += rotationSpeed * direction * dt;
                if (angle > 360f) angle -= 360f;
                if (angle < -360f) angle += 360f;
                return;
            }

            pingPongTime += dt * rotationSpeed * direction / Mathf.Max(1f, pingPongArc);
            angle = startAngle + Mathf.Sin(pingPongTime) * pingPongArc * 0.5f;
        }

        private void ApplyRotation()
        {
            // Rotate about LOCAL X only; the arm's radial offset turns that into a sweep.
            Quaternion target = Quaternion.Euler(angle, 0f, 0f);
            if (pivotBody != null && pivotBody.isKinematic)
                pivotBody.MoveRotation(pivot.parent != null ? pivot.parent.rotation * target : target);
            else
                pivot.localRotation = target;
        }

        private void DetectStrikes()
        {
            Vector3 a = pivot.TransformPoint(strikeLocalStart);
            Vector3 b = pivot.TransformPoint(strikeLocalEnd);

            int count = Physics.OverlapCapsuleNonAlloc(a, b, strikeRadius, overlapBuffer,
                                                       unitLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var unit = overlapBuffer[i].GetComponentInParent<ChallengeUnit>();

                // IsHittable covers down, resolved, and the brief immunity while standing back up.
                if (unit == null || !unit.IsHittable) continue;

                if (lastHitTime.TryGetValue(unit, out float t) && Time.time - t < perUnitCooldown) continue;
                lastHitTime[unit] = Time.time;

                Vector3 contact = overlapBuffer[i].ClosestPoint(unit.transform.position);
                HitInfo hit = BuildHit(contact, unit);

                if (logStrikes)
                    Debug.Log($"[Arm] {unit.Definition.displayName} struck at {angle:0}deg, " +
                              $"dV {hit.DeltaV:0.0} m/s (damage threshold {unit.Definition.hitDamageThreshold:0.0}, " +
                              $"durability {unit.DamageTaken:0.00}/{unit.Definition.toughness:0.0})");

                UnitStruck?.Invoke(unit, hit);
                unit.ReceiveHit(hit);
            }
        }

        /// <summary>
        /// Impulse follows the arm's own surface velocity at the contact point (omega x r), so a
        /// faster or longer arm hits harder without extra tuning, plus an authored lift.
        /// </summary>
        private HitInfo BuildHit(Vector3 contactPoint, ChallengeUnit unit)
        {
            Vector3 axis = pivot.right;                       // local X in world space
            Vector3 radius = contactPoint - pivot.position;
            Vector3 surfaceVelocity = Vector3.Cross(axis * SignedAngularSpeedRad, radius);

            Vector3 dir = surfaceVelocity.sqrMagnitude > 0.0001f
                ? surfaceVelocity.normalized
                : -pivot.forward;
            dir = (dir + Vector3.up * upwardBias).normalized;

            float mass = unit.Definition != null ? unit.Definition.mass : referenceMass;
            float massScale = Mathf.Lerp(1f, mass / Mathf.Max(1f, referenceMass), massCompensation);
            Vector3 impulse = dir * impactStrength * massScale;

            return new HitInfo(impulse.magnitude / Mathf.Max(0.01f, mass), impulse, contactPoint, dir, angle);
        }

        private void OnDrawGizmosSelected()
        {
            var p = pivot != null ? pivot : transform;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Vector3 a = p.TransformPoint(strikeLocalStart);
            Vector3 b = p.TransformPoint(strikeLocalEnd);
            Gizmos.DrawWireSphere(a, strikeRadius);
            Gizmos.DrawWireSphere(b, strikeRadius);
            Gizmos.DrawLine(a, b);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(p.position - p.right * 3f, p.position + p.right * 3f);
        }
    }
}
