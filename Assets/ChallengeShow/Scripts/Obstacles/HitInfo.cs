using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// One obstacle strike, described once at the point of impact.
    ///
    /// Everything downstream — durability, ragdoll launch, the episode log, the camera — reads the
    /// same struct instead of re-deriving severity from an impulse vector after the fact.
    /// </summary>
    public readonly struct HitInfo
    {
        /// <summary>Speed change this strike would impart, in m/s. The measure of severity.</summary>
        public readonly float DeltaV;
        public readonly Vector3 Impulse;
        public readonly Vector3 Point;
        /// <summary>Unit-length launch direction.</summary>
        public readonly Vector3 Direction;
        /// <summary>Obstacle phase in degrees when contact happened, for editing telemetry.</summary>
        public readonly float ObstacleAngle;

        public HitInfo(float deltaV, Vector3 impulse, Vector3 point, Vector3 direction, float obstacleAngle)
        {
            DeltaV = deltaV;
            Impulse = impulse;
            Point = point;
            Direction = direction;
            ObstacleAngle = obstacleAngle;
        }
    }
}
