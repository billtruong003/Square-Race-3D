using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Measures how far a ragdoll's joints actually come apart while it is being thrown around.
    ///
    /// "The limbs look detached" is not something that can be tuned by eye, because the worst frame
    /// lasts a fraction of a second and never appears in a screenshot taken afterwards. This watches
    /// every joint each physics step and keeps the peaks.
    ///
    /// The headline number is <see cref="PeakRelativeError"/>: anchor separation divided by the
    /// bone's own length. Relative, because an absolute threshold is meaningless across a roster
    /// spanning a 1.04 m Cacti and a 4.25 m Cactus Boss - 5 cm of drift is invisible on one and a
    /// severed limb on the other.
    ///
    /// Attach at runtime for a measurement session; it does nothing unless enabled.
    /// </summary>
    public class RagdollStretchProbe : MonoBehaviour
    {
        private CharacterJoint[] joints;
        private float[] restLength;

        public float PeakAbsoluteError { get; private set; }
        public float PeakRelativeError { get; private set; }
        public string WorstJoint { get; private set; } = "-";
        public float PeakLinearSpeed { get; private set; }
        public float PeakAngularSpeed { get; private set; }
        public int ClampHits { get; private set; }

        // --- root locomotion, for diagnosing units that never leave the spawn ---
        private Rigidbody root;
        private ChallengeUnitMotor motor;
        public float MinZ { get; private set; } = float.MaxValue;
        public float MaxZ { get; private set; } = float.MinValue;
        public float PeakRootSpeed { get; private set; }
        public bool EverMoving { get; private set; }
        public bool EverKinematic { get; private set; }

        private void Awake()
        {
            root = GetComponent<Rigidbody>();
            motor = GetComponent<ChallengeUnitMotor>();
            joints = GetComponentsInChildren<CharacterJoint>(true);
            restLength = new float[joints.Length];

            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j.connectedBody == null) continue;
                // The bone's own length: how far this joint sits from the body it hangs off.
                restLength[i] = Mathf.Max(0.05f,
                    Vector3.Distance(j.transform.position, j.connectedBody.transform.position));
            }
        }

        private void FixedUpdate()
        {
            if (root != null)
            {
                float z = transform.position.z;
                if (z < MinZ) MinZ = z;
                if (z > MaxZ) MaxZ = z;
                if (root.isKinematic) EverKinematic = true;
                else PeakRootSpeed = Mathf.Max(PeakRootSpeed, root.linearVelocity.magnitude);
                if (motor != null && motor.IsMoving) EverMoving = true;
            }

            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j == null || j.connectedBody == null) continue;

                // At rest these two world points coincide. Anything else is the solver failing to
                // hold the joint together, which is exactly what reads on screen as a stretched or
                // detached limb.
                Vector3 a = j.transform.TransformPoint(j.anchor);
                Vector3 b = j.connectedBody.transform.TransformPoint(j.connectedAnchor);
                float err = Vector3.Distance(a, b);

                if (err > PeakAbsoluteError) PeakAbsoluteError = err;

                float rel = err / restLength[i];
                if (rel > PeakRelativeError)
                {
                    PeakRelativeError = rel;
                    WorstJoint = $"{j.name}<-{j.connectedBody.name}";
                }

                var rb = j.GetComponent<Rigidbody>();
                if (rb == null) continue;
                float v = rb.linearVelocity.magnitude;
                float w = rb.angularVelocity.magnitude;
                if (v > PeakLinearSpeed) PeakLinearSpeed = v;
                if (w > PeakAngularSpeed) PeakAngularSpeed = w;

                // A body pinned at its clamp every step is a symptom, not a solution - it means
                // something upstream is still feeding it unbounded energy.
                if (v >= rb.maxLinearVelocity - 0.01f || w >= rb.maxAngularVelocity - 0.01f) ClampHits++;
            }
        }

        /// <summary>What the root body is actually touching, for units that refuse to move.</summary>
        public string Contacts { get; private set; } = "";

        private void OnCollisionStay(Collision c)
        {
            if (c.collider == null) return;
            var other = c.collider;
            if (other.transform.IsChildOf(transform)) return;

            string tag = other.name + "(" + (other.transform.parent != null ? other.transform.parent.name : "-") + ")";
            if (!Contacts.Contains(tag)) Contacts += tag + " ";
        }

        public void ResetPeaks()
        {
            PeakAbsoluteError = 0f;
            PeakRelativeError = 0f;
            PeakLinearSpeed = 0f;
            PeakAngularSpeed = 0f;
            ClampHits = 0;
            WorstJoint = "-";
        }

        public string Summary() =>
            $"stretch abs {PeakAbsoluteError:0.000}m rel {PeakRelativeError:0.00}x ({WorstJoint}) " +
            $"vMax {PeakLinearSpeed:0.0} wMax {PeakAngularSpeed:0.0} clampHits {ClampHits} | " +
            $"rootZ {MinZ:0.0}..{MaxZ:0.0} rootSpeed {PeakRootSpeed:0.0} everMoving={EverMoving} everKinematic={EverKinematic} | touching: {Contacts}";
    }
}
