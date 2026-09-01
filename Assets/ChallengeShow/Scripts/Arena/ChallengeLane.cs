using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Geometry of the challenge run: where a unit starts, which way it faces, and where the
    /// pass line is. Everything else in the arena asks the lane for these instead of hard-coding
    /// world positions.
    /// </summary>
    public class ChallengeLane : MonoBehaviour
    {
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform finishPoint;

        [Tooltip("Playable width of the lane. Used for spawn jitter and gizmos only — falling off " +
                 "the edge is what actually fails a unit.")]
        [SerializeField] private float laneWidth = 7f;

        public Vector3 StartPosition => startPoint != null ? startPoint.position : transform.position;
        public Vector3 FinishPosition => finishPoint != null ? finishPoint.position : transform.position + Vector3.forward * 40f;
        public float LaneWidth => laneWidth;

        /// <summary>Unit heading, always horizontal.</summary>
        public Vector3 Forward
        {
            get
            {
                Vector3 d = FinishPosition - StartPosition;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            }
        }

        public float Length => Vector3.Distance(
            new Vector3(StartPosition.x, 0f, StartPosition.z),
            new Vector3(FinishPosition.x, 0f, FinishPosition.z));

        /// <summary>How far down the lane a world position sits, in metres from the start line.</summary>
        public float ProgressAlongLane(Vector3 worldPosition) =>
            Vector3.Dot(worldPosition - StartPosition, Forward);

        /// <summary>
        /// The point on the centre line level with <paramref name="worldPosition"/>.
        ///
        /// A recovered unit re-anchors its lane centring here rather than to the start line, so it
        /// drifts back to the middle while continuing forward instead of being dragged backwards.
        /// </summary>
        public Vector3 ClosestCenterPoint(Vector3 worldPosition) =>
            StartPosition + Forward * ProgressAlongLane(worldPosition);

        /// <summary>How far off the centre line a position is, in metres (unsigned).</summary>
        public float DistanceFromCenter(Vector3 worldPosition)
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, Forward);
            return Mathf.Abs(Vector3.Dot(worldPosition - StartPosition, lateral));
        }

        /// <summary>
        /// Whether a unit that came to rest here can plausibly stand up and carry on: still over the
        /// deck and not past the finish.
        /// </summary>
        public bool IsRecoverablePosition(Vector3 worldPosition, float margin = 1.5f) =>
            DistanceFromCenter(worldPosition) <= laneWidth * 0.5f + margin &&
            ProgressAlongLane(worldPosition) < Length;

        private void OnDrawGizmos()
        {
            Vector3 a = StartPosition;
            Vector3 b = FinishPosition;
            Vector3 side = Vector3.Cross(Vector3.up, Forward) * (laneWidth * 0.5f);

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawLine(a - side, b - side);
            Gizmos.DrawLine(a + side, b + side);
            Gizmos.DrawLine(a - side, a + side);

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawLine(b - side, b + side);
        }
    }
}
