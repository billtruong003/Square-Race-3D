using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Arena
{
    /// <summary>
    /// Turns a set of axis-aligned wall footprints into one smooth mesh per connected wall mass:
    /// rounded corners (convex and concave), a 45-degree chamfer around the top edge, vertical
    /// sides down to the floor. Visual only - the original box colliders keep doing the physics,
    /// so the ASCII layout and the simulation are untouched.
    ///
    /// Why a merged mesh: bevelling each scaled cube separately leaves notches and seams wherever
    /// two rectangles of one mass meet. Why a flat chamfer rather than a rounded edge: the
    /// screen-space outline inks any normal change, so a rounded edge would draw as a thick black
    /// band; a chamfer gives exactly two crisp lines and a lit facet in between.
    /// </summary>
    public static class WallMassMesher
    {
        private const int ArcSegments = 4;
        private const float Eps = 1e-3f;

        private struct Loop
        {
            public List<Vector2> Points;   // filleted outline, interior on the left
            public List<Vector2> Normals;  // outward unit normal per point (exact on arcs)
            public bool IsOuter;
        }

        public static List<Mesh> Build(IReadOnlyList<Rect> rects, float groundY, float height,
            float cornerRadius, float chamfer)
        {
            var meshes = new List<Mesh>();
            if (rects == null || rects.Count == 0) return meshes;

            // ---- compressed grid over every rectangle edge
            List<float> xs = Coordinates(rects, true);
            List<float> zs = Coordinates(rects, false);
            int nx = xs.Count - 1, nz = zs.Count - 1;
            if (nx <= 0 || nz <= 0) return meshes;

            var occ = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    var c = new Vector2((xs[i] + xs[i + 1]) * 0.5f, (zs[j] + zs[j + 1]) * 0.5f);
                    for (int r = 0; r < rects.Count; r++)
                    {
                        if (rects[r].Contains(c)) { occ[i, j] = true; break; }
                    }
                }
            }

            // ---- connected components (4-neighbour)
            var comp = new int[nx, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) comp[i, j] = -1;
            int count = 0;
            var stack = new Stack<Vector2Int>();
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!occ[i, j] || comp[i, j] >= 0) continue;
                    comp[i, j] = count;
                    stack.Push(new Vector2Int(i, j));
                    while (stack.Count > 0)
                    {
                        Vector2Int p = stack.Pop();
                        TryPush(p.x + 1, p.y); TryPush(p.x - 1, p.y); TryPush(p.x, p.y + 1); TryPush(p.x, p.y - 1);
                    }
                    count++;
                }
            }

            void TryPush(int a, int b)
            {
                if (a < 0 || b < 0 || a >= nx || b >= nz || !occ[a, b] || comp[a, b] >= 0) return;
                comp[a, b] = count;
                stack.Push(new Vector2Int(a, b));
            }

            for (int k = 0; k < count; k++)
            {
                List<List<Vector2>> raw = TraceLoops(occ, comp, k, xs, zs);
                var loops = new List<Loop>();
                foreach (List<Vector2> poly in raw)
                {
                    List<Vector2> simple = RemoveCollinear(poly);
                    if (simple.Count < 4) continue;
                    loops.Add(Fillet(simple, cornerRadius));
                }

                Mesh mesh = Extrude(loops, groundY, height, chamfer);
                if (mesh != null) meshes.Add(mesh);
            }

            return meshes;
        }

        // ------------------------------------------------------------------ outline tracing

        private static List<float> Coordinates(IReadOnlyList<Rect> rects, bool x)
        {
            var values = new List<float>(rects.Count * 2);
            for (int i = 0; i < rects.Count; i++)
            {
                values.Add(x ? rects[i].xMin : rects[i].yMin);
                values.Add(x ? rects[i].xMax : rects[i].yMax);
            }

            values.Sort();
            var unique = new List<float>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                if (unique.Count == 0 || values[i] - unique[unique.Count - 1] > Eps) unique.Add(values[i]);
            }

            return unique;
        }

        /// <summary>
        /// Boundary edges of one component, directed so the wall is on their left (outer loops come
        /// out counter-clockwise seen from above, holes clockwise), then chained into closed loops.
        /// </summary>
        private static List<List<Vector2>> TraceLoops(bool[,] occ, int[,] comp, int k, List<float> xs, List<float> zs)
        {
            int nx = occ.GetLength(0), nz = occ.GetLength(1);
            int vx = nx + 1;
            var outgoing = new Dictionary<int, List<int>>();
            var used = new HashSet<long>();

            bool In(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && comp[i, j] == k;
            int V(int i, int j) => j * vx + i;

            void AddEdge(int a, int b)
            {
                if (!outgoing.TryGetValue(a, out List<int> list)) outgoing[a] = list = new List<int>(2);
                list.Add(b);
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!In(i, j)) continue;
                    if (!In(i, j - 1)) AddEdge(V(i, j), V(i + 1, j));
                    if (!In(i + 1, j)) AddEdge(V(i + 1, j), V(i + 1, j + 1));
                    if (!In(i, j + 1)) AddEdge(V(i + 1, j + 1), V(i, j + 1));
                    if (!In(i - 1, j)) AddEdge(V(i, j + 1), V(i, j));
                }
            }

            Vector2 P(int v) => new Vector2(xs[v % vx], zs[v / vx]);

            var loops = new List<List<Vector2>>();
            foreach (KeyValuePair<int, List<int>> start in outgoing)
            {
                foreach (int firstTarget in start.Value)
                {
                    long firstKey = (long)start.Key * 1000003L + firstTarget;
                    if (used.Contains(firstKey)) continue;

                    var loop = new List<Vector2>();
                    int from = start.Key, to = firstTarget;
                    int guard = 0;
                    while (guard++ < 100000)
                    {
                        used.Add((long)from * 1000003L + to);
                        loop.Add(P(from));
                        if (to == start.Key) break;

                        // At a pinch vertex (two cells touching diagonally) take the left turn,
                        // which keeps each loop simple instead of figure-eighting through the pinch.
                        Vector2 dIn = (P(to) - P(from)).normalized;
                        List<int> next = outgoing[to];
                        int pick = -1;
                        float bestTurn = float.MinValue;
                        for (int n = 0; n < next.Count; n++)
                        {
                            if (used.Contains((long)to * 1000003L + next[n])) continue;
                            Vector2 dOut = (P(next[n]) - P(to)).normalized;
                            float turn = -dIn.y * dOut.x + dIn.x * dOut.y; // dot(dOut, left(dIn))
                            if (turn > bestTurn) { bestTurn = turn; pick = next[n]; }
                        }

                        if (pick < 0) break;
                        from = to;
                        to = pick;
                    }

                    if (loop.Count >= 4) loops.Add(loop);
                }
            }

            return loops;
        }

        private static List<Vector2> RemoveCollinear(List<Vector2> poly)
        {
            var result = new List<Vector2>(poly.Count);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[(i - 1 + n) % n], b = poly[i], c = poly[(i + 1) % n];
                Vector2 d1 = b - a, d2 = c - b;
                if (Mathf.Abs(d1.x * d2.y - d1.y * d2.x) < Eps * Eps) continue;
                result.Add(b);
            }

            return result;
        }

        // ------------------------------------------------------------------ rounding

        private static Loop Fillet(List<Vector2> poly, float radius)
        {
            int n = poly.Count;
            var points = new List<Vector2>(n * (ArcSegments + 1));
            var normals = new List<Vector2>(n * (ArcSegments + 1));

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i - 1 + n) % n], p = poly[i], next = poly[(i + 1) % n];
                Vector2 e1 = p - prev, e2 = next - p;
                Vector2 d1 = e1.normalized, d2 = e2.normalized;
                float r = Mathf.Min(radius, 0.45f * e1.magnitude, 0.45f * e2.magnitude);

                Vector2 outward1 = new Vector2(d1.y, -d1.x); // right of travel = outside the wall
                if (r <= Eps)
                {
                    points.Add(p);
                    normals.Add((outward1 + new Vector2(d2.y, -d2.x)).normalized);
                    continue;
                }

                // Tangent points P - d1 r and P + d2 r; the circle through both, tangent to both
                // edges, is centred at P - d1 r + d2 r for a left or a right turn alike.
                Vector2 centre = p - d1 * r + d2 * r;
                Vector2 start = p - d1 * r;
                float sign = Vector2.Dot(start - centre, outward1) >= 0f ? 1f : -1f;

                for (int s = 0; s <= ArcSegments; s++)
                {
                    float t = s * (Mathf.PI * 0.5f / ArcSegments);
                    Vector2 v = centre - d2 * (r * Mathf.Cos(t)) + d1 * (r * Mathf.Sin(t));
                    points.Add(v);
                    normals.Add(((v - centre) * sign).normalized);
                }
            }

            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return new Loop { Points = points, Normals = normals, IsOuter = area > 0f };
        }

        // ------------------------------------------------------------------ mesh

        private static Mesh Extrude(List<Loop> loops, float groundY, float height, float chamfer)
        {
            if (loops.Count == 0) return null;

            var vertices = new List<Vector3>();
            var meshNormals = new List<Vector3>();
            var triangles = new List<int>();
            float top = groundY + height;
            float c = Mathf.Clamp(chamfer, 0f, height * 0.45f);
            float shoulder = top - c;

            var insetLoops = new List<List<Vector2>>(loops.Count);

            foreach (Loop loop in loops)
            {
                int n = loop.Points.Count;
                var inset = new List<Vector2>(n);
                for (int i = 0; i < n; i++) inset.Add(loop.Points[i] - loop.Normals[i] * c);
                insetLoops.Add(inset);

                // Vertical side: shoulder down to the floor, smooth along arcs (exact normals).
                int baseSide = vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = loop.Points[i];
                    var nrm = new Vector3(loop.Normals[i].x, 0f, loop.Normals[i].y);
                    vertices.Add(new Vector3(p.x, groundY, p.y)); meshNormals.Add(nrm);
                    vertices.Add(new Vector3(p.x, shoulder, p.y)); meshNormals.Add(nrm);
                }

                for (int i = 0; i < n; i++)
                {
                    int a = baseSide + i * 2, b = baseSide + ((i + 1) % n) * 2;
                    Vector3 facing = (meshNormals[a] + meshNormals[b]).normalized;
                    Quad(triangles, vertices, a, b, b + 1, a + 1, facing);
                }

                if (c <= Eps) continue;

                // Chamfer band: outer edge at the shoulder, inner edge at the top.
                int baseBand = vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = loop.Points[i], q = inset[i];
                    var nrm = (new Vector3(loop.Normals[i].x, 0f, loop.Normals[i].y) + Vector3.up).normalized;
                    vertices.Add(new Vector3(p.x, shoulder, p.y)); meshNormals.Add(nrm);
                    vertices.Add(new Vector3(q.x, top, q.y)); meshNormals.Add(nrm);
                }

                for (int i = 0; i < n; i++)
                {
                    int a = baseBand + i * 2, b = baseBand + ((i + 1) % n) * 2;
                    Vector3 facing = (meshNormals[a] + meshNormals[b]).normalized;
                    Quad(triangles, vertices, a, b, b + 1, a + 1, facing);
                }
            }

            // Top cap: the inset outlines, holes bridged into their outer loop, ear-clipped.
            var outers = new List<List<Vector2>>();
            var holes = new List<List<Vector2>>();
            for (int i = 0; i < loops.Count; i++) (loops[i].IsOuter ? outers : holes).Add(insetLoops[i]);

            foreach (List<Vector2> outer in outers)
            {
                var mine = new List<List<Vector2>>();
                foreach (List<Vector2> hole in holes)
                {
                    if (hole.Count > 0 && PointInPolygon(hole[0], outer)) mine.Add(hole);
                }

                List<Vector2> merged = BridgeHoles(outer, mine);
                List<int> capTris = EarClip(merged);
                int baseCap = vertices.Count;
                for (int i = 0; i < merged.Count; i++)
                {
                    vertices.Add(new Vector3(merged[i].x, top, merged[i].y));
                    meshNormals.Add(Vector3.up);
                }

                for (int t = 0; t < capTris.Count; t += 3)
                {
                    Tri(triangles, vertices, baseCap + capTris[t], baseCap + capTris[t + 1], baseCap + capTris[t + 2], Vector3.up);
                }
            }

            var mesh = new Mesh { name = "WallMass" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(meshNormals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Quad(List<int> tris, List<Vector3> v, int a, int b, int c, int d, Vector3 facing)
        {
            Tri(tris, v, a, b, c, facing);
            Tri(tris, v, a, c, d, facing);
        }

        /// <summary>Adds a triangle wound so its front face points along 'facing'.</summary>
        private static void Tri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 facing)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        // ------------------------------------------------------------------ polygon helpers

        private static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }

            return inside;
        }

        /// <summary>
        /// Cuts each hole into the outer loop with a zero-width bridge from the hole's rightmost
        /// vertex to the nearest outer vertex it can see, so one ear-clip covers the whole cap.
        /// </summary>
        private static List<Vector2> BridgeHoles(List<Vector2> outer, List<List<Vector2>> holes)
        {
            var poly = new List<Vector2>(outer);
            holes.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));

            foreach (List<Vector2> hole in holes)
            {
                int m = 0;
                for (int i = 1; i < hole.Count; i++) if (hole[i].x > hole[m].x) m = i;
                Vector2 hm = hole[m];

                int best = -1;
                float bestSq = float.MaxValue;
                for (int i = 0; i < poly.Count; i++)
                {
                    float sq = (poly[i] - hm).sqrMagnitude;
                    if (sq >= bestSq) continue;
                    if (!SegmentClear(hm, poly[i], poly, holes)) continue;
                    bestSq = sq;
                    best = i;
                }

                if (best < 0) continue;

                var spliced = new List<Vector2>(poly.Count + hole.Count + 2);
                for (int i = 0; i <= best; i++) spliced.Add(poly[i]);
                for (int k = 0; k <= hole.Count; k++) spliced.Add(hole[(m + k) % hole.Count]);
                spliced.Add(poly[best]);
                for (int i = best + 1; i < poly.Count; i++) spliced.Add(poly[i]);
                poly = spliced;
            }

            return poly;
        }

        private static float MaxX(List<Vector2> p)
        {
            float m = float.MinValue;
            for (int i = 0; i < p.Count; i++) m = Mathf.Max(m, p[i].x);
            return m;
        }

        private static bool SegmentClear(Vector2 a, Vector2 b, List<Vector2> poly, List<List<Vector2>> holes)
        {
            if (Crosses(a, b, poly)) return false;
            for (int h = 0; h < holes.Count; h++) if (Crosses(a, b, holes[h])) return false;
            return true;
        }

        private static bool Crosses(Vector2 a, Vector2 b, List<Vector2> poly)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 c = poly[i], d = poly[(i + 1) % poly.Count];
                if (Near(c, a) || Near(c, b) || Near(d, a) || Near(d, b)) continue;
                if (SegmentsIntersect(a, b, c, d)) return true;
            }

            return false;
        }

        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-8f;

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        /// <summary>Plain ear clipping of a counter-clockwise polygon (bridged holes allowed).</summary>
        private static List<int> EarClip(List<Vector2> poly)
        {
            var result = new List<int>();
            var idx = new List<int>(poly.Count);
            for (int i = 0; i < poly.Count; i++) idx.Add(i);

            int guard = 0;
            while (idx.Count > 3 && guard++ < poly.Count * poly.Count)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(a, b, c) <= 1e-7f) continue; // reflex or degenerate

                    bool contains = false;
                    for (int j = 0; j < idx.Count && !contains; j++)
                    {
                        int ip = idx[j];
                        if (ip == ia || ip == ib || ip == ic) continue;
                        Vector2 p = poly[ip];
                        if (Near(p, a) || Near(p, b) || Near(p, c)) continue;
                        if (Cross(a, b, p) >= 0f && Cross(b, c, p) >= 0f && Cross(c, a, p) >= 0f) contains = true;
                    }

                    if (contains) continue;

                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped) break;
            }

            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }
    }
}
