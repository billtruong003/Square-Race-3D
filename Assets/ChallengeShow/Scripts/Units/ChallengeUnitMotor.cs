using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Deterministic straight-lane locomotion. Drives the Rigidbody's horizontal velocity toward
    /// the lane direction and leaves gravity alone, so the arm can still knock the unit around
    /// before ragdoll takes over. No navigation, no pathfinding — the lane is a straight line.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ChallengeUnitMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float acceleration = 12f;

        [Tooltip("How hard the unit steers back to the lane centre line. 0 = no correction.")]
        [SerializeField] private float laneCenteringStrength = 2f;

        private Rigidbody body;
        private Vector3 laneForward = Vector3.forward;
        private Vector3 laneCenterPoint;

        public bool IsMoving { get; private set; }
        public float MoveSpeed => moveSpeed;

        private void Awake() => body = GetComponent<Rigidbody>();

        public void Configure(ChallengeUnitDefinition definition)
        {
            moveSpeed = definition.moveSpeed;
            acceleration = definition.acceleration;
        }

        /// <summary>Point the unit down the lane and remember the centre line to steer toward.</summary>
        public void SetLane(Vector3 centerPoint, Vector3 forward)
        {
            laneCenterPoint = centerPoint;
            laneForward = forward.normalized;
            transform.rotation = Quaternion.LookRotation(laneForward, Vector3.up);
        }

        public void BeginRun() => IsMoving = true;

        public void StopRun()
        {
            IsMoving = false;
            if (body != null && !body.isKinematic)
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }

        private void FixedUpdate()
        {
            if (!IsMoving || body.isKinematic) return;

            Vector3 lateral = Vector3.Cross(Vector3.up, laneForward);
            float offLane = Vector3.Dot(transform.position - laneCenterPoint, lateral);

            Vector3 desired = laneForward * moveSpeed - lateral * (offLane * laneCenteringStrength);
            Vector3 current = body.linearVelocity;
            Vector3 horizontal = new Vector3(current.x, 0f, current.z);
            Vector3 delta = Vector3.MoveTowards(horizontal, desired, acceleration * Time.fixedDeltaTime);

            body.linearVelocity = new Vector3(delta.x, current.y, delta.z);

            // Keep facing down-lane; the arm's shove should not spin the runner.
            body.MoveRotation(Quaternion.LookRotation(laneForward, Vector3.up));
        }
    }
}
