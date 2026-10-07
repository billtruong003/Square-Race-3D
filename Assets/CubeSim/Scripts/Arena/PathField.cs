using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Core;

namespace CubeSim.Arena
{
    /// <summary>
    /// Walking distance over the arena floor, on a grid of cells a racer could actually stand in.
    ///
    /// A cell is open when a racer-sized box centred on it clears every solid wall, so corridors too
    /// narrow for a cube are closed and the field never routes through them. Breakable walls (rocks,
    /// glass gates) are passable at a cost: they open during the round, and a route through them is
    /// still a route. Built once per round from the physics scene, so it sees exactly the walls the
    /// racers collide with.
    ///
    /// One field feeds everything that needs to know "how far from X": race standings, the zone that
    /// chases along the course, timeout tiebreaks and the map validator.
    /// </summary>
    public sealed class PathField
    {
        public const float Unreachable = float.MaxValue;

        private const float BreakableCostCells = 6f;

        public readonly float CellSize;
        public readonly int Columns;
        public readonly int Rows;
        public readonly Vector2 Origin;

        private readonly bool[] _open;
        private readonly bool[] _breakable;
        private float[] _distance;

        public float MaxDistance { get; private set; }
        public int OpenCount { get; private set; }

        public float[] Distances => _distance;

        private PathField(Rect bounds, float cellSize)
        {
            CellSize = cellSize;
            Origin = bounds.min;
            Columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cellSize));
            Rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cellSize));
            _open = new bool[Columns * Rows];
            _breakable = new bool[Columns * Rows];
            _distance = new float[Columns * Rows];
        }

        /// <summary>Rasterises the arena's walls for a racer of this half extent.</summary>
        public static PathField Build(ArenaRuntime arena, float racerHalfExtent, float cellSize)
        {
            var field = new PathField(arena.PlayableRect, Mathf.Max(0.2f, cellSize));
            float y = arena.GroundY + racerHalfExtent;
            var half = new Vector3(racerHalfExtent * 0.95f, racerHalfExtent * 0.5f, racerHalfExtent * 0.95f);
            var hits = new Collider[8];

            for (int r = 0; r < field.Rows; r++)
            {
                for (int c = 0; c < field.Columns; c++)
                {
                    int i = r * field.Columns + c;
                    Vector3 centre = field.CellCentre(c, r, y);
                    int count = Physics.OverlapBoxNonAlloc(centre, half, hits, Quaternion.identity,
                        SimulationLayers.WallMask, QueryTriggerInteraction.Ignore);

                    bool solid = false, breakable = false;
                    for (int h = 0; h < count; h++)
                    {
                        if (hits[h].GetComponentInParent<BreakableWall>() != null) breakable = true;
                        else solid = true;
                        hits[h] = null;
                    }

                    field._open[i] = !solid;
                    field._breakable[i] = !solid && breakable;
                    if (!solid) field.OpenCount++;
                }
            }

            return field;
        }

        public Vector3 CellCentre(int column, int row, float y) =>
            new Vector3(Origin.x + (column + 0.5f) * CellSize, y, Origin.y + (row + 0.5f) * CellSize);

        public bool IsOpen(int index) => index >= 0 && index < _open.Length && _open[index];

        public int IndexOf(Vector3 position)
        {
            int c = Mathf.FloorToInt((position.x - Origin.x) / CellSize);
            int r = Mathf.FloorToInt((position.z - Origin.y) / CellSize);
            if (c < 0 || r < 0 || c >= Columns || r >= Rows) return -1;
            return r * Columns + c;
        }

        /// <summary>Distance from the sources at a world position; Unreachable outside the open floor.</summary>
        public float Sample(Vector3 position)
        {
            int i = IndexOf(position);
            if (i < 0) return Unreachable;
            if (_open[i]) return _distance[i];

            // A cube hugging a wall can sit over a closed cell; read its best open neighbour.
            float best = Unreachable;
            int c = i % Columns, r = i / Columns;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int nc = c + dc, nr = r + dr;
                    if (nc < 0 || nr < 0 || nc >= Columns || nr >= Rows) continue;
                    int n = nr * Columns + nc;
                    if (_open[n] && _distance[n] < best) best = _distance[n];
                }
            }

            return best;
        }

        /// <summary>Dijkstra from every open cell inside the given footprints. Returns false when none is open.</summary>
        public bool ComputeFrom(IReadOnlyList<Rect> sources)
        {
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
            var heap = new MinHeap(Columns * Rows / 4 + 16);
            bool any = false;

            for (int s = 0; s < sources.Count; s++)
            {
                Rect src = sources[s];
                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < Columns; c++)
                    {
                        int i = r * Columns + c;
                        if (!_open[i]) continue;
                        Vector3 p = CellCentre(c, r, 0f);
                        if (!src.Contains(new Vector2(p.x, p.z))) continue;
                        _distance[i] = 0f;
                        heap.Push(i, 0f);
                        any = true;
                    }
                }
            }

            if (!any) return false;
            Relax(heap);
            return true;
        }

        /// <summary>Dijkstra from the open cell nearest a world point.</summary>
        public bool ComputeFrom(Vector3 point)
        {
            int start = NearestOpen(point);
            if (start < 0) return false;
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
            var heap = new MinHeap(Columns * Rows / 4 + 16);
            _distance[start] = 0f;
            heap.Push(start, 0f);
            Relax(heap);
            return true;
        }

        public int NearestOpen(Vector3 point)
        {
            int i = IndexOf(point);
            if (i >= 0 && _open[i]) return i;

            int best = -1;
            float bestSq = float.MaxValue;
            for (int k = 0; k < _open.Length; k++)
            {
                if (!_open[k]) continue;
                Vector3 c = CellCentre(k % Columns, k / Columns, 0f);
                float sq = (c.x - point.x) * (c.x - point.x) + (c.z - point.z) * (c.z - point.z);
                if (sq < bestSq) { bestSq = sq; best = k; }
            }

            return best;
        }

        /// <summary>Copies the current distances so a second ComputeFrom does not overwrite them.</summary>
        public float[] Snapshot() => (float[])_distance.Clone();

        private void Relax(MinHeap heap)
        {
            float diagonal = Mathf.Sqrt(2f);
            MaxDistance = 0f;

            while (heap.Count > 0)
            {
                heap.Pop(out int i, out float d);
                if (d > _distance[i]) continue;
                if (d > MaxDistance) MaxDistance = d;

                int c = i % Columns, r = i / Columns;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nc = c + dc, nr = r + dr;
                        if (nc < 0 || nr < 0 || nc >= Columns || nr >= Rows) continue;
                        int n = nr * Columns + nc;
                        if (!_open[n]) continue;

                        // No corner cutting: a diagonal step needs both orthogonal cells open.
                        if (dr != 0 && dc != 0 && (!_open[r * Columns + nc] || !_open[nr * Columns + c])) continue;

                        float step = (dr != 0 && dc != 0 ? diagonal : 1f) * CellSize;
                        if (_breakable[n]) step += BreakableCostCells * CellSize;
                        float nd = d + step;
                        if (nd >= _distance[n]) continue;
                        _distance[n] = nd;
                        heap.Push(n, nd);
                    }
                }
            }
        }

        /// <summary>Plain binary heap on (cell, distance); the field is rebuilt rarely, so no pooling.</summary>
        private sealed class MinHeap
        {
            private int[] _items;
            private float[] _keys;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new int[capacity];
                _keys = new float[capacity];
            }

            public void Push(int item, float key)
            {
                if (Count == _items.Length)
                {
                    System.Array.Resize(ref _items, Count * 2);
                    System.Array.Resize(ref _keys, Count * 2);
                }

                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= key) break;
                    _items[i] = _items[parent];
                    _keys[i] = _keys[parent];
                    i = parent;
                }

                _items[i] = item;
                _keys[i] = key;
            }

            public void Pop(out int item, out float key)
            {
                item = _items[0];
                key = _keys[0];
                int lastItem = _items[--Count];
                float lastKey = _keys[Count];

                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count) break;
                    if (child + 1 < Count && _keys[child + 1] < _keys[child]) child++;
                    if (_keys[child] >= lastKey) break;
                    _items[i] = _items[child];
                    _keys[i] = _keys[child];
                    i = child;
                }

                if (Count > 0)
                {
                    _items[i] = lastItem;
                    _keys[i] = lastKey;
                }
            }
        }
    }
}
