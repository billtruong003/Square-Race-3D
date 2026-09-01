using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// The ordered route the pressure takes through an authored map. Segments are its direct
    /// children, in sibling order; reordering them in the Hierarchy reorders the route.
    /// </summary>
    [ExecuteAlways]
    public class PressureTrack : MonoBehaviour
    {
        [Tooltip("Seconds before the first segment starts filling.")]
        [SerializeField] private float startDelay = 8f;

        [Tooltip("Multiplies every segment's speed. One dial to pace the whole route.")]
        [SerializeField] private float speedScale = 1f;

        private readonly List<PressureSegment> _segments = new List<PressureSegment>();

        public float StartDelay => Mathf.Max(0f, startDelay);
        public float SpeedScale => Mathf.Max(0.01f, speedScale);

        /// <summary>Segments in route order. Rebuilt on demand so editor edits are picked up.</summary>
        public IReadOnlyList<PressureSegment> Segments
        {
            get
            {
                _segments.Clear();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var segment = transform.GetChild(i).GetComponent<PressureSegment>();
                    if (segment != null) _segments.Add(segment);
                }

                return _segments;
            }
        }

        /// <summary>Total seconds the route takes to fill, ignoring pauses.</summary>
        public float EstimateDuration()
        {
            float total = StartDelay;
            IReadOnlyList<PressureSegment> segments = Segments;

            for (int i = 0; i < segments.Count; i++)
            {
                PressureSegment s = segments[i];
                total += s.StartDelay + s.FillLength / (s.Speed * SpeedScale);
            }

            return total;
        }

        private void OnDrawGizmos()
        {
            IReadOnlyList<PressureSegment> segments = Segments;
            if (segments.Count < 2) return;

            // Order line, so the route reads at a glance in the Scene view.
            Gizmos.color = new Color(1f, 0.35f, 0.05f, 1f);
            for (int i = 1; i < segments.Count; i++)
            {
                Gizmos.DrawLine(segments[i - 1].transform.position, segments[i].transform.position);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                Gizmos.DrawWireSphere(segments[i].transform.position, 0.6f + i * 0.05f);
            }
        }
    }
}
