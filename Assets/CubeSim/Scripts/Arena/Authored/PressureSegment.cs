using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// One leg of an authored pressure route. The transform is the region the segment will fill; the
    /// fill starts at the face opposite <see cref="fillDirection"/> and sweeps across.
    ///
    /// Segments run in the order they appear under the <see cref="PressureTrack"/>, each starting
    /// when the previous one is full. That is what makes a serpentine route possible without faking
    /// it with one giant moving collider.
    /// </summary>
    [ExecuteAlways]
    public class PressureSegment : MonoBehaviour
    {
        [Tooltip("Which way the fill sweeps. It starts at the opposite face.")]
        [SerializeField] private FillDirection fillDirection = FillDirection.PlusX;

        [Tooltip("Fill rate in metres per second.")]
        [SerializeField] private float speed = 3f;

        [Tooltip("Extra seconds before this segment starts, after the previous one completes.")]
        [SerializeField] private float startDelay = 0f;

        [Tooltip("Fraction filled that counts as complete and hands over to the next segment.")]
        [Range(0.5f, 1f)] [SerializeField] private float completionFraction = 1f;

        public FillDirection Direction => fillDirection;
        public float Speed => Mathf.Max(0.01f, speed);
        public float StartDelay => Mathf.Max(0f, startDelay);
        public float CompletionFraction => completionFraction;

        public Rect Footprint
        {
            get
            {
                Vector3 p = transform.position;
                Vector3 s = transform.lossyScale;
                return WallFillMath.FromCenterSize(new Vector2(p.x, p.z), new Vector2(s.x, s.z));
            }
        }

        /// <summary>Distance the fill front travels to cover the whole segment.</summary>
        public float FillLength
        {
            get
            {
                Rect r = Footprint;
                return WallFillMath.Axis(fillDirection) == 0 ? r.width : r.height;
            }
        }

        /// <summary>Coordinate the fill starts from, on the fill axis.</summary>
        public float StartCoordinate
        {
            get
            {
                Rect r = Footprint;
                switch (fillDirection)
                {
                    case FillDirection.PlusX: return r.xMin;
                    case FillDirection.MinusX: return r.xMax;
                    case FillDirection.PlusZ: return r.yMin;
                    default: return r.yMax;
                }
            }
        }

        /// <summary>The filled sub-rectangle after the front has travelled <paramref name="distance"/>.</summary>
        public Rect FilledRect(float distance)
        {
            Rect r = Footprint;
            float d = Mathf.Clamp(distance, 0f, FillLength);
            float start = StartCoordinate;
            float sign = WallFillMath.Sign(fillDirection);
            float end = start + sign * d;

            if (WallFillMath.Axis(fillDirection) == 0)
            {
                return Rect.MinMaxRect(Mathf.Min(start, end), r.yMin, Mathf.Max(start, end), r.yMax);
            }

            return Rect.MinMaxRect(r.xMin, Mathf.Min(start, end), r.xMax, Mathf.Max(start, end));
        }

        private void OnDrawGizmos()
        {
            Rect r = Footprint;
            var center = new Vector3(r.center.x, transform.position.y, r.center.y);
            var size = new Vector3(r.width, 0.4f, r.height);

            Gizmos.color = new Color(1f, 0.55f, 0.08f, 0.85f);
            Gizmos.DrawWireCube(center, size);

            // Start face, then an arrow showing the sweep.
            float start = StartCoordinate;
            int axis = WallFillMath.Axis(fillDirection);
            float sign = WallFillMath.Sign(fillDirection);

            Vector3 a, b, dir;
            if (axis == 0)
            {
                a = new Vector3(start, transform.position.y, r.yMin);
                b = new Vector3(start, transform.position.y, r.yMax);
                dir = new Vector3(sign, 0f, 0f);
            }
            else
            {
                a = new Vector3(r.xMin, transform.position.y, start);
                b = new Vector3(r.xMax, transform.position.y, start);
                dir = new Vector3(0f, 0f, sign);
            }

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
            Gizmos.DrawLine(a, b);

            Vector3 mid = (a + b) * 0.5f;
            float length = axis == 0 ? r.width : r.height;
            Vector3 tip = mid + dir * length;

            Gizmos.color = new Color(1f, 0.55f, 0.08f, 1f);
            Gizmos.DrawLine(mid, tip);

            Vector3 side = axis == 0 ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);
            Gizmos.DrawLine(tip, tip - dir * 1.2f + side * 0.8f);
            Gizmos.DrawLine(tip, tip - dir * 1.2f - side * 0.8f);
        }
    }
}
