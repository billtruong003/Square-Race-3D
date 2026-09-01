using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Racers
{
    /// <summary>
    /// Racer-vs-racer collision: uniform-grid broadphase plus a swept disc narrow phase.
    ///
    /// The broadphase is a counting sort into pre-allocated arrays, so a step costs no allocations
    /// even with several hundred racers, and only racers in the 3x3 neighbourhood of a cell are ever
    /// narrow-phase tested. Cell size is chosen by the caller to cover a full step of travel, so a
    /// pair that could possibly have crossed is always in adjacent cells.
    ///
    /// Determinism: pairs are visited in ascending (lower index, higher index) order regardless of
    /// how the grid happens to bucket them, so the same seed produces the same contacts every run.
    /// Nothing here iterates a hash container or touches Unity object ordering.
    ///
    /// This runs before the constraint solver, which re-validates every racer against walls and
    /// pressure afterwards - so a separation can never leave a racer inside geometry.
    /// </summary>
    public sealed class RacerContactGrid
    {
        /// <summary>Neighbours considered for one racer in a single pass. Beyond this the extras are skipped.</summary>
        private const int MaxCandidates = 64;

        private readonly float _cellSize;
        private readonly int _columns;
        private readonly int _rows;
        private readonly Vector2 _origin;
        private readonly float _skin;
        private readonly int _iterations;

        private readonly int[] _cellStart;   // length = cells + 1
        private readonly int[] _entries;     // length = racer capacity
        private readonly int[] _cellOfRacer; // length = racer capacity
        private readonly int[] _candidates = new int[MaxCandidates];

        /// <summary>Contacts resolved since construction. Read by the validation harness.</summary>
        public int ContactCount { get; private set; }

        /// <summary>Deepest overlap seen before separation, in metres.</summary>
        public float MaxPenetration { get; private set; }

        /// <summary>Contacts that were found by the swept test rather than by end-of-step overlap.</summary>
        public int SweptContactCount { get; private set; }

        public RacerContactGrid(Rect playableRect, float cellSize, int racerCapacity,
            float skinWidth, int iterations)
        {
            _cellSize = Mathf.Max(0.25f, cellSize);
            _skin = Mathf.Max(0f, skinWidth);
            _iterations = Mathf.Clamp(iterations, 1, 8);

            _origin = new Vector2(playableRect.xMin, playableRect.yMin);
            _columns = Mathf.Max(1, Mathf.CeilToInt(playableRect.width / _cellSize));
            _rows = Mathf.Max(1, Mathf.CeilToInt(playableRect.height / _cellSize));

            _cellStart = new int[_columns * _rows + 1];
            _entries = new int[Mathf.Max(1, racerCapacity)];
            _cellOfRacer = new int[Mathf.Max(1, racerCapacity)];
        }

        /// <summary>
        /// Separates touching racers and bounces both off the contact normal.
        ///
        /// Several passes are run because separating one pair can push a racer into a third - a
        /// three-way pile settles over a couple of iterations instead of leaving somebody overlapped
        /// until the next step.
        /// </summary>
        public void ResolveContacts(Racer[] racers, int count, ConstraintSolver solver)
        {
            if (count < 2) return;

            for (int pass = 0; pass < _iterations; pass++)
            {
                Build(racers, count);

                bool any = false;
                for (int i = 0; i < count; i++)
                {
                    Racer a = racers[i];

                    // Dead racers are gone and finishers are parked in a goal; neither blocks anyone.
                    if (!a.IsActive) continue;

                    int found = GatherHigherNeighbours(racers, count, i);
                    for (int k = 0; k < found; k++)
                    {
                        // Candidates arrive sorted, so pairs resolve in (min index, max index) order.
                        if (Resolve(a, racers[_candidates[k]], solver, pass == 0)) any = true;
                    }
                }

                if (!any) return;
            }
        }

        /// <summary>
        /// Collects the racers in the 3x3 cell neighbourhood whose index is above this one, sorted
        /// ascending. Insertion sort on a handful of entries, into a pre-allocated buffer: the
        /// ordering is what makes the pass reproducible, and it costs nothing at these sizes.
        /// </summary>
        private int GatherHigherNeighbours(Racer[] racers, int count, int index)
        {
            int cell = _cellOfRacer[index];
            int column = cell % _columns;
            int row = cell / _columns;
            int found = 0;

            for (int dr = -1; dr <= 1; dr++)
            {
                int r = row + dr;
                if (r < 0 || r >= _rows) continue;

                for (int dc = -1; dc <= 1; dc++)
                {
                    int c = column + dc;
                    if (c < 0 || c >= _columns) continue;

                    int neighbour = r * _columns + c;
                    int start = _cellStart[neighbour];
                    int end = _cellStart[neighbour + 1];

                    for (int e = start; e < end; e++)
                    {
                        int j = _entries[e];
                        if (j <= index || j >= count) continue;
                        if (!racers[j].IsActive) continue;
                        if (found >= MaxCandidates) continue;

                        int slot = found++;
                        while (slot > 0 && _candidates[slot - 1] > j)
                        {
                            _candidates[slot] = _candidates[slot - 1];
                            slot--;
                        }

                        _candidates[slot] = j;
                    }
                }
            }

            return found;
        }

        private void Build(Racer[] racers, int count)
        {
            System.Array.Clear(_cellStart, 0, _cellStart.Length);

            for (int i = 0; i < count; i++)
            {
                int cell = CellIndex(racers[i].Position);
                _cellOfRacer[i] = cell;
                _cellStart[cell + 1]++;
            }

            for (int i = 1; i < _cellStart.Length; i++) _cellStart[i] += _cellStart[i - 1];

            // _cellStart[c] is now the write cursor for cell c; restore it after filling.
            for (int i = 0; i < count; i++)
            {
                int cell = _cellOfRacer[i];
                _entries[_cellStart[cell]++] = i;
            }

            for (int i = _cellStart.Length - 1; i > 0; i--) _cellStart[i] = _cellStart[i - 1];
            _cellStart[0] = 0;
        }

        private int CellIndex(Vector3 position)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((position.x - _origin.x) / _cellSize), 0, _columns - 1);
            int r = Mathf.Clamp(Mathf.FloorToInt((position.z - _origin.y) / _cellSize), 0, _rows - 1);
            return r * _columns + c;
        }

        /// <summary>
        /// One pair. On the first pass the swept test also runs, which catches a pair that crossed
        /// during the step without ever ending it overlapped; later passes only clean up overlaps the
        /// earlier separations created, and the racers have not moved since, so sweeping again would
        /// re-report the same crossing.
        /// </summary>
        private bool Resolve(Racer a, Racer b, ConstraintSolver solver, bool allowSweep)
        {
            float contactDistance = a.HalfExtent + b.HalfExtent;

            if (!RacerContactMath.TryOverlap(a.Position, b.Position, contactDistance,
                    out Vector3 normal, out float penetration))
            {
                if (!allowSweep) return false;
                return ResolveCrossing(a, b, contactDistance);
            }

            MaxPenetration = Mathf.Max(MaxPenetration, penetration);
            Separate(a, b, normal, penetration, solver);
            Bounce(a, b, normal);
            ContactCount++;
            return true;
        }

        /// <summary>
        /// A pair that swapped sides within one step. They are rewound to the moment they touched,
        /// which is where the bounce belongs - resolving it at the end of the step would send both
        /// racers off from the wrong place.
        /// </summary>
        private bool ResolveCrossing(Racer a, Racer b, float contactDistance)
        {
            if (!RacerContactMath.TrySweep(a.PreviousPosition, a.Position, b.PreviousPosition,
                    b.Position, contactDistance, out float toi))
            {
                return false;
            }

            a.Position = RacerContactMath.Lerp(a.PreviousPosition, a.Position, toi);
            b.Position = RacerContactMath.Lerp(b.PreviousPosition, b.Position, toi);

            if (!RacerContactMath.TryOverlap(a.Position, b.Position, contactDistance,
                    out Vector3 normal, out float penetration))
            {
                // Touching exactly, so the centre line is still the contact normal.
                if (!PlanarMath.TryNormalizePlanar(b.Position - a.Position, out normal)) return false;
                penetration = 0f;
            }

            MaxPenetration = Mathf.Max(MaxPenetration, penetration);

            // Nudge them apart by the skin so the next step does not start flush.
            a.Position -= normal * (_skin * 0.5f);
            b.Position += normal * (_skin * 0.5f);

            Bounce(a, b, normal);
            ContactCount++;
            SweptContactCount++;
            return true;
        }

        /// <summary>
        /// Pushes the pair apart along the contact normal, half each where both can take it. A racer
        /// whose half would land it inside a wall or outside the pressure gives its share to the
        /// other one.
        /// </summary>
        private void Separate(Racer a, Racer b, Vector3 normal, float penetration, ConstraintSolver solver)
        {
            float total = penetration + _skin;

            Vector3 targetA = a.Position - normal * (total * 0.5f);
            Vector3 targetB = b.Position + normal * (total * 0.5f);

            bool canMoveA = solver.IsLegal(targetA, a.HalfExtent);
            bool canMoveB = solver.IsLegal(targetB, b.HalfExtent);

            RacerContactMath.SplitCorrection(total, canMoveA, canMoveB,
                out float shareA, out float shareB);

            if (shareA > 0f)
            {
                Vector3 moved = a.Position - normal * shareA;

                // The full correction is a longer move than the half that was tested; if that lands
                // illegally, fall back to the half rather than shoving the racer into geometry.
                a.Position = solver.IsLegal(moved, a.HalfExtent) ? moved : targetA;
            }

            if (shareB > 0f)
            {
                Vector3 moved = b.Position + normal * shareB;
                b.Position = solver.IsLegal(moved, b.HalfExtent) ? moved : targetB;
            }
        }

        private static void Bounce(Racer a, Racer b, Vector3 normal)
        {
            Vector3 directionA = a.Direction;
            Vector3 directionB = b.Direction;

            RacerContactMath.Respond(ref directionA, ref directionB, normal,
                out bool changedA, out bool changedB);

            if (changedA)
            {
                a.Direction = directionA;
                a.BounceCount++;
                a.RacerBounceCount++;
            }

            if (changedB)
            {
                b.Direction = directionB;
                b.BounceCount++;
                b.RacerBounceCount++;
            }
        }
    }
}
