using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// The show camera. A plain Camera driven by this script — no Cinemachine — because the shots
    /// this format needs are simple and predictable, and a hand-written rig makes them exact.
    ///
    /// Framing follows the unit's own state rather than director events, so a run that goes
    /// run → hit → launch → recover → run again stays one continuous readable sequence instead of
    /// latching into a close result shot on the first knockdown.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ChallengeCameraRig : MonoBehaviour
    {
        public enum Shot
        {
            Establishing,
            Follow,
            Ragdoll,
            Recovering,
            Result
        }

        [SerializeField] private ChallengeDirector director;
        [SerializeField] private ChallengeLane lane;

        [Header("Establishing Shot")]
        [SerializeField] private Vector3 establishingPosition = new(-34f, 26f, -26f);
        [SerializeField] private Vector3 establishingLookAt = new(0f, 5f, 20f);
        [SerializeField] private float establishingFov = 55f;

        [Header("Side-On Follow")]
        [Tooltip("Angle away from the lane direction. 90 is a pure side view; 70-80 keeps a little " +
                 "of the run direction visible so the viewer can tell which way the unit is going.")]
        [Range(30f, 110f)]
        [SerializeField] private float sideAngle = 78f;
        [SerializeField] private float followDistance = 11f;
        [SerializeField] private float followHeight = 5.6f;
        [SerializeField] private float followFov = 50f;
        [Tooltip("Aim this far above the tracked point so the unit sits low in frame with the arm above.")]
        [SerializeField] private float lookHeightOffset = 1.2f;

        [Header("Ragdoll Shot")]
        [Tooltip("Wide enough to hold the launch, the wall and the arm — but small units still have " +
                 "to read, so this stays well short of a true wide shot.")]
        [SerializeField] private float ragdollDistance = 13f;
        [SerializeField] private float ragdollHeight = 7.2f;

        [Header("Recovery Shot")]
        [Tooltip("Slightly wider and lower than the run shot, so the stand-up and the lane ahead are " +
                 "both visible before the next attempt begins.")]
        [SerializeField] private float recoverDistance = 9f;
        [SerializeField] private float recoverHeight = 4.6f;

        [Header("Result Shot")]
        [SerializeField] private float resultDistance = 8f;
        [SerializeField] private float resultHeight = 4.2f;

        [Header("Feel")]
        [SerializeField] private float positionSharpness = 3.5f;
        [SerializeField] private float aimSharpness = 5f;
        [Tooltip("Slow horizontal drift around the subject, in degrees per second. 0 disables it.")]
        [SerializeField] private float orbitDriftSpeed = 4f;

        [Header("Side Selection")]
        [Tooltip("Alternate left/right each attempt instead of always filming from the same side.")]
        [SerializeField] private bool alternateSides = true;
        [SerializeField] private bool startOnRight = true;

        [Header("Obstruction")]
        [Tooltip("Geometry the camera will not sit inside. Leave as everything; units are excluded " +
                 "by the probe starting at the subject.")]
        [SerializeField] private LayerMask obstructionLayers = ~0;
        [SerializeField] private float obstructionProbeRadius;
        [Tooltip("Closest the camera may be pulled by an obstruction before it simply accepts it.")]
        [SerializeField] private float minObstructedDistance = 7.5f;

        [Tooltip("Units smaller than this get pulled closer so they stay readable at YouTube size.")]
        [SerializeField] private float smallUnitHeight = 1.5f;
        [Range(0.5f, 1f)]
        [SerializeField] private float smallUnitDistanceScale = 0.78f;

        private Camera cam;
        private ChallengeUnit tracked;
        private Shot shot = Shot.Establishing;
        private Vector3 smoothedTarget;
        private float sideSign = 1f;
        private float orbitDrift;
        private bool summonHold;

        public Shot CurrentShot => shot;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            sideSign = startOnRight ? 1f : -1f;
            smoothedTarget = lane != null ? lane.StartPosition : Vector3.zero;
        }

        private void OnEnable()
        {
            if (director == null) return;
            director.UnitSummoned += HandleSummoned;
            director.RunStarted += HandleRunStarted;
            director.AttemptResolved += HandleResolved;
        }

        private void OnDisable()
        {
            if (director == null) return;
            director.UnitSummoned -= HandleSummoned;
            director.RunStarted -= HandleRunStarted;
            director.AttemptResolved -= HandleResolved;
        }

        private void Start() => SnapToEstablishing();

        private void LateUpdate()
        {
            if (tracked != null && !summonHold) shot = ShotForState(tracked.State);

            if (shot == Shot.Establishing)
            {
                MoveTowardPose(establishingPosition, establishingLookAt, establishingFov);
                return;
            }
            if (tracked == null) return;

            smoothedTarget = Damp(smoothedTarget, tracked.TrackedPosition, positionSharpness * 2f);
            if (orbitDriftSpeed > 0f) orbitDrift += orbitDriftSpeed * Time.deltaTime;

            GetFraming(shot, out float distance, out float height);

            // Small monsters vanish at the wider distances; pull in proportionally.
            if (tracked.Definition != null && tracked.Definition.height < smallUnitHeight)
                distance *= smallUnitDistanceScale;

            Vector3 lookAt = smoothedTarget + Vector3.up * lookHeightOffset;
            Vector3 desired = ResolveObstruction(lookAt, smoothedTarget + SideOffset(distance, height));
            MoveTowardPose(desired, lookAt, followFov);
        }

        /// <summary>
        /// Framing is a function of the unit's state, so a knockdown mid-run does not need the
        /// director to announce anything and Result can only ever mean a real verdict.
        /// </summary>
        private static Shot ShotForState(ChallengeState state) => state switch
        {
            ChallengeState.Ragdoll => Shot.Ragdoll,
            ChallengeState.Recovering => Shot.Recovering,
            ChallengeState.Passed or ChallengeState.Failed => Shot.Result,
            _ => Shot.Follow
        };

        private void GetFraming(Shot s, out float distance, out float height)
        {
            switch (s)
            {
                case Shot.Ragdoll:    distance = ragdollDistance; height = ragdollHeight; break;
                case Shot.Recovering: distance = recoverDistance; height = recoverHeight; break;
                case Shot.Result:     distance = resultDistance;  height = resultHeight;  break;
                default:              distance = followDistance;  height = followHeight;  break;
            }
        }

        /// <summary>Camera offset placed to the side of the lane, on whichever side this attempt uses.</summary>
        private Vector3 SideOffset(float distance, float height)
        {
            Vector3 forward = lane != null ? lane.Forward : Vector3.forward;
            float yaw = sideSign * sideAngle + Mathf.Sin(orbitDrift * Mathf.Deg2Rad) * 6f;
            Vector3 flat = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
            return flat * distance + Vector3.up * height;
        }

        private void MoveTowardPose(Vector3 position, Vector3 lookAt, float fov)
        {
            transform.position = Damp(transform.position, position, positionSharpness);

            Vector3 toTarget = lookAt - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(toTarget, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want,
                                                      1f - Mathf.Exp(-aimSharpness * Time.deltaTime));
            }
            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-2f * Time.deltaTime));
        }

        /// <summary>
        /// Pull the camera in front of anything standing between it and the subject.
        ///
        /// Units are routinely launched into the crystal wall and settle right against it, and the
        /// lane is now framed by rock; without this the side-on shot ends up inside that geometry
        /// looking at the inside of a boulder.
        /// </summary>
        private Vector3 ResolveObstruction(Vector3 lookAt, Vector3 desired)
        {
            // Off by default. The arena is framed by low rock, and pulling the camera in every time
            // the probe clipped a rim boulder dragged it down INTO the rocks instead of clearing
            // them - the raised shot heights solve the real case (the crystal wall) on their own.
            if (obstructionProbeRadius <= 0f) return desired;

            Vector3 ray = desired - lookAt;
            float length = ray.magnitude;
            if (length < 0.01f) return desired;

            if (!Physics.SphereCast(lookAt, obstructionProbeRadius, ray / length, out RaycastHit hit,
                                    length, obstructionLayers, QueryTriggerInteraction.Ignore))
                return desired;

            // Never crowd the subject past a usable minimum, or small units become unreadable.
            float safe = Mathf.Max(hit.distance - obstructionProbeRadius, minObstructedDistance);
            return lookAt + ray / length * safe;
        }

        private static Vector3 Damp(Vector3 current, Vector3 target, float sharpness) =>
            Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

        public void SnapToEstablishing()
        {
            shot = Shot.Establishing;
            transform.position = establishingPosition;
            transform.rotation = Quaternion.LookRotation(establishingLookAt - establishingPosition, Vector3.up);
            if (cam != null) cam.fieldOfView = establishingFov;
        }

        private void HandleSummoned(ChallengeUnit unit)
        {
            tracked = unit;
            smoothedTarget = unit.TrackedPosition;
            summonHold = true;              // hold the wide shot until the run actually starts
            shot = Shot.Establishing;

            if (alternateSides) sideSign = -sideSign;
            orbitDrift = 0f;
        }

        private void HandleRunStarted(ChallengeUnit unit)
        {
            tracked = unit;
            summonHold = false;
            shot = Shot.Follow;
        }

        private void HandleResolved(ChallengeUnit unit)
        {
            tracked = unit;
            summonHold = false;
            shot = Shot.Result;
        }
    }
}
