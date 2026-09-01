using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Owns the switch between animated and simulated state for one unit.
    /// The bodies themselves are generated offline by <c>RagdollBuilder</c>; this component only
    /// flips them on and off and applies the launch impulse.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChallengeUnitRagdoll : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private Collider rootCollider;

        [Tooltip("Share of the impulse given to the whole ragdoll rather than just the impact bone.")]
        [Range(0f, 1f)]
        [SerializeField] private float bodyWideImpulseShare = 0.55f;

        [Tooltip("Hard speed ceiling per bone. Joint solvers occasionally blow up on a bad frame " +
                 "and fling a body hundreds of metres; this keeps a take usable when that happens.")]
        [SerializeField] private float maxBoneSpeed = 30f;
        [SerializeField] private float maxBoneSpin = 30f;

        /// <summary>
        /// Cap on how fast PhysX may push a bone out of another collider.
        ///
        /// This is the one clamp that actually stops ragdoll blow-ups, and it is separate from
        /// maxLinearVelocity: depenetration is applied as a positional correction with its own speed
        /// limit, so a bone that ends a step overlapping the crystal wall could be ejected at
        /// hundreds of m/s no matter how tightly ordinary velocity was clamped. Left unset, the
        /// project inherits an effectively unlimited default, and roughly one attempt in five ended
        /// with a unit thrown thousands of metres down-lane after its first hard hit into the wall.
        ///
        /// 10 m/s, not 3. Both values eliminate the blow-ups completely, but at 3 a downed unit could
        /// no longer push itself clear of the wall or the deck, so it lingered inside the arm's reach
        /// and was re-struck: average meaningful hits rose from 2.8 to 3.7 per unit and the entire
        /// roster was overwhelmed, with nobody finishing. 10 still makes a kilometre-scale ejection
        /// impossible while leaving ordinary contact resolution intact.
        /// </summary>
        [SerializeField] private float maxDepenetrationSpeed = 10f;

        [Tooltip("Position solver iterations per bone. Project default is 6, which is too loose for " +
                 "a jointed chain. Runtime-only in Unity 6 - not serialized on Rigidbody.")]
        [SerializeField] private int solverIterations = 12;
        [Tooltip("Velocity solver iterations per bone. Project default is 1.")]
        [SerializeField] private int solverVelocityIterations = 4;

        [Tooltip("Longest lever arm the focused strike impulse may act on, in metres. Bounds how " +
                 "much of a hit becomes spin instead of travel. Set from the creature's own size.")]
        [SerializeField] private float impactLeverLimit = 0.12f;

        [Tooltip("Largest velocity change, in m/s, the single struck bone may receive. Anything " +
                 "beyond this is spread over the whole body so total momentum is preserved.")]
        [SerializeField] private float maxBoneImpactDeltaV = 12f;

        private readonly List<Rigidbody> bones = new();
        private readonly List<Collider> boneColliders = new();
        private Vector3[] boneLocalPositions;
        private Quaternion[] boneLocalRotations;

        public bool IsActive { get; private set; }

        /// <summary>
        /// The body the rest of the game treats as "where the monster is".
        ///
        /// This is bones[0], which RagdollBuilder always seeds from the rig's root bone, so it is
        /// the pelvis on all 15 rigs. Deliberately NOT the heaviest bone: mass follows bone volume,
        /// and that picks the head on Skeleton, a calf on Skeleton Giant and an arm on Cactus Boss,
        /// which made the camera chase a flailing limb instead of the body.
        /// </summary>
        public Rigidbody PelvisBone { get; private set; }

        /// <summary>Only used to bias where a focused impulse lands.</summary>
        public Rigidbody HeaviestBone { get; private set; }

        /// <summary>Bone rigidbodies exist only if the ragdoll was generated for this prefab.</summary>
        public bool HasRagdoll => bones.Count > 0;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (rootBody == null) rootBody = GetComponent<Rigidbody>();
            if (rootCollider == null) rootCollider = GetComponent<Collider>();

            CacheBones();
            SetRagdollEnabled(false);
        }

        /// <summary>
        /// Stop ragdoll bones that already overlap from shoving each other apart.
        ///
        /// Unity suppresses collision across a joint automatically, so a bone never fights its
        /// direct parent. What it does not cover is bones TWO joints apart - a spine segment and the
        /// neck above it - and bilateral pairs like a left and right collarbone, whose capsules are
        /// generated from bone length and routinely intersect at the bind pose. The audit measured
        /// real penetration on 8 of the 15 rigs, up to 0.303 m on Cactus Boss between Spine2 and
        /// Neck. Every one of those pairs starts the attempt already inside each other, so the
        /// solver's first job on spawn is to push them apart - which reads on screen as a body
        /// bursting open.
        ///
        /// The policy is deliberately narrow: suppress a pair only if it is a near neighbour in the
        /// joint graph, or if it is MEASURABLY interpenetrating right now. General body-on-body
        /// collision is left intact, because a ragdoll whose limbs pass through its own torso looks
        /// just as wrong as one that explodes.
        /// </summary>
        private void SuppressBadSelfCollisions()
        {
            // Joint-graph neighbours, by walking each bone's connected body up to two hops.
            for (int i = 0; i < boneColliders.Count; i++)
            {
                var a = boneColliders[i];
                var body = a.attachedRigidbody;
                if (body == null) continue;

                var joint = body.GetComponent<CharacterJoint>();
                for (int hop = 0; hop < 2 && joint != null && joint.connectedBody != null; hop++)
                {
                    var other = joint.connectedBody.GetComponent<Collider>();
                    if (other != null && other != a) Physics.IgnoreCollision(a, other, true);
                    joint = joint.connectedBody.GetComponent<CharacterJoint>();
                }
            }

            // Anything still starting inside something else, whatever its position in the graph.
            for (int i = 0; i < boneColliders.Count; i++)
                for (int k = i + 1; k < boneColliders.Count; k++)
                {
                    var a = boneColliders[i];
                    var b = boneColliders[k];
                    if (Physics.ComputePenetration(
                            a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation,
                            out _, out float depth) && depth > 0.001f)
                        Physics.IgnoreCollision(a, b, true);
                }
        }

        private void CacheBones()
        {
            foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == rootBody) continue;
                bones.Add(rb);
                if (PelvisBone == null) PelvisBone = rb;   // bones[0] is always the rig root bone
                rb.maxLinearVelocity = maxBoneSpeed;
                rb.maxAngularVelocity = maxBoneSpin;
                rb.maxDepenetrationVelocity = maxDepenetrationSpeed;

                // A joint chain is solved iteratively and the project default of 6/1 is tuned for
                // loose props, not an articulated body - it is the difference between a joint that
                // holds and one that visibly drifts apart under load. Set here rather than in the
                // ragdoll builder because Unity 6 does not serialize these on Rigidbody, so an
                // edit-time write is discarded. Only one ragdoll is ever active, so the cost is
                // negligible.
                rb.solverIterations = solverIterations;
                rb.solverVelocityIterations = solverVelocityIterations;
                var col = rb.GetComponent<Collider>();
                if (col != null) boneColliders.Add(col);
                if (HeaviestBone == null || rb.mass > HeaviestBone.mass) HeaviestBone = rb;
            }

            if (PelvisBone != null)
                BindPelvisHeight = transform.InverseTransformPoint(PelvisBone.position).y;

            boneLocalPositions = new Vector3[bones.Count];
            boneLocalRotations = new Quaternion[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                boneLocalPositions[i] = bones[i].transform.localPosition;
                boneLocalRotations[i] = bones[i].transform.localRotation;
            }
        }

        /// <summary>Hand the body over to physics and launch it.</summary>
        public void Activate(Vector3 impulse, Vector3 impactPoint)
        {
            if (IsActive || !HasRagdoll) return;
            IsActive = true;

            if (animator != null) animator.enabled = false;
            SetRagdollEnabled(true);

            // Inherit the running velocity so the launch reads as continuous motion.
            Vector3 carriedVelocity = rootBody != null ? rootBody.linearVelocity : Vector3.zero;

            Rigidbody nearest = FindNearestBone(impactPoint);
            Vector3 bodyWide = impulse * bodyWideImpulseShare;
            Vector3 focused = impulse * (1f - bodyWideImpulseShare);

            // Share the body-wide impulse BY MASS, not equally per bone.
            //
            // Dividing it by bone count gave every bone the same impulse regardless of what it
            // weighs, so a 0.99 kg spine tip and a 3.17 kg pelvis came out of the same hit at
            // completely different speeds. That velocity differential is applied directly across
            // the joints holding them together, and on a light chain rig it diverged outright:
            // Cacti's spine reached 2,094 m/s and 39 km of joint separation while its own root had
            // barely moved. Weighting by mass gives every bone the SAME delta-v, so the body leaves
            // the ground as one object and the joints start the flight with nothing to resolve.
            float totalBoneMass = 0f;
            foreach (var bone in bones) totalBoneMass += bone.mass;
            totalBoneMass = Mathf.Max(0.0001f, totalBoneMass);

            foreach (var bone in bones)
            {
                bone.linearVelocity = carriedVelocity;
                bone.AddForce(bodyWide * (bone.mass / totalBoneMass), ForceMode.Impulse);
            }

            if (nearest != null)
            {
                // Apply the focused impulse on a SHORT lever arm.
                //
                // This used to go straight in at the contact point, and torque is r x F: a hit
                // landing even a few centimetres off a light bone's centre of mass turned most of
                // the impulse into spin. Measured on Cat Meow, bones reached 112 rad/s - about 18
                // revolutions per second - and slammed into the angular clamp 32 times in a single
                // attempt, while the worst joint separated by 69% of its own bone length. No solver
                // holds a chain together against that, and it is what read on screen as limbs
                // tearing off.
                //
                // Clamping the lever to a fraction of the creature's own size keeps the hit
                // rotational - the body still tumbles rather than sliding flat - while bounding the
                // angular energy. Linear momentum is untouched, so knockback distance is unchanged.
                Vector3 com = nearest.worldCenterOfMass;
                Vector3 lever = impactPoint - com;
                float maxLever = Mathf.Max(0.05f, impactLeverLimit);
                if (lever.magnitude > maxLever) lever = lever.normalized * maxLever;

                // Cap what a SINGLE bone can absorb, and give the remainder to the whole body.
                //
                // The focused share is sized against the creature, not against the bone it happens
                // to land on. On a light chain rig that is ruinous: Cacti's spine segments weigh
                // about 1 kg each, so the focused impulse alone implied a 18-54 m/s change on one
                // link, and the joint chain diverged - measured at 7.78 m of separation on a 1.04 m
                // creature, with bones ending up 8 m from their own root. Heavier creatures were
                // fine only because their bones are heavier.
                //
                // Total momentum is unchanged: whatever the bone cannot take is redistributed
                // across every bone, so knockback distance and direction are preserved.
                float cap = nearest.mass * maxBoneImpactDeltaV;
                Vector3 applied = focused;
                if (applied.magnitude > cap)
                {
                    Vector3 overflow = applied - applied.normalized * cap;
                    applied = applied.normalized * cap;

                    foreach (var bone in bones)
                        bone.AddForce(overflow * (bone.mass / totalBoneMass), ForceMode.Impulse);
                }

                nearest.AddForceAtPosition(applied, com + lever, ForceMode.Impulse);
            }

            if (rootCollider != null) rootCollider.enabled = false;
            if (rootBody != null) rootBody.isKinematic = true;
        }

        /// <summary>Return to the animated state and restore the bind pose.</summary>
        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            SetRagdollEnabled(false);
            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].transform.localPosition = boneLocalPositions[i];
                bones[i].transform.localRotation = boneLocalRotations[i];
            }

            if (rootBody != null) rootBody.isKinematic = false;
            if (rootCollider != null) rootCollider.enabled = true;
            if (animator != null) animator.enabled = true;
        }

        /// <summary>Where the ragdoll actually is, for camera framing and pass/fail checks.</summary>
        public Vector3 CurrentCenter =>
            PelvisBone != null && IsActive ? PelvisBone.worldCenterOfMass : transform.position;

        /// <summary>World pose of the pelvis right now — the anchor recovery re-roots the unit to.</summary>
        public Vector3 PelvisPosition => PelvisBone != null ? PelvisBone.position : transform.position;

        /// <summary>
        /// Pelvis heading flattened to the horizontal plane. Used only as a hint; the unit snaps to
        /// lane forward on recovery, because a monster that stands up facing the wall is not content.
        /// </summary>
        public Vector3 PelvisFlatForward
        {
            get
            {
                if (PelvisBone == null) return transform.forward;
                Vector3 f = PelvisBone.transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
            }
        }

        /// <summary>Offset from the root to the pelvis in the bind pose, i.e. standing hip height.</summary>
        public float BindPelvisHeight { get; private set; }

        /// <summary>True once the ragdoll has stopped tumbling.</summary>
        public bool IsSettled(float speedThreshold = 0.35f)
        {
            if (!IsActive) return false;
            foreach (var bone in bones)
                if (bone.linearVelocity.sqrMagnitude > speedThreshold * speedThreshold) return false;
            return true;
        }

        private void SetRagdollEnabled(bool on)
        {
            foreach (var bone in bones)
            {
                // Clear momentum while the body is still dynamic — writing velocity on a body that
                // has already been made kinematic is ignored and logs a warning every time.
                if (!on && !bone.isKinematic)
                {
                    bone.linearVelocity = Vector3.zero;
                    bone.angularVelocity = Vector3.zero;
                }
                bone.isKinematic = !on;
            }
            foreach (var col in boneColliders) col.enabled = on;

            // Re-apply every time the ragdoll is switched on, NOT once at Awake.
            //
            // Unity discards IgnoreCollision state whenever a collider is disabled and re-enabled,
            // and the line above does precisely that on every activation - so suppressing these
            // pairs during Awake looked correct and silently did nothing by the time the body was
            // actually ragdolling.
            if (on) SuppressBadSelfCollisions();
        }

        private Rigidbody FindNearestBone(Vector3 point)
        {
            Rigidbody best = null;
            float bestSqr = float.MaxValue;
            foreach (var bone in bones)
            {
                float sqr = (bone.worldCenterOfMass - point).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = bone;
            }
            return best ?? HeaviestBone;
        }
    }
}
