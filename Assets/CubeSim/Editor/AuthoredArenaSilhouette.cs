using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena.Authored;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Renders a built authored map back out as ASCII, and diffs it against the template the map was
    /// authored from.
    ///
    /// The ASCII template is the authoritative macro geometry for these maps - '#' is solid mass,
    /// '.' is floor that is open on purpose. Reading a prefab and eyeballing it in the Scene view is
    /// not a check: a stray blocker dropped into an open region, or a tooth that grew across a gap,
    /// looks perfectly reasonable from above and still breaks the layout. So the map is sampled back
    /// into the same grid the template is written in and compared cell by cell.
    ///
    /// Sampling is by wall coverage, not by racer clearance: the question here is "is this cell
    /// solid?", which is a different question from "can a racer stand here?" that the reachability
    /// pass already answers.
    /// </summary>
    public static class AuthoredArenaSilhouette
    {
        public const char Solid = '#';
        public const char Open = '.';

        /// <summary>Coverage between these two is neither clearly wall nor clearly floor.</summary>
        private const float AmbiguousLow = 0.2f;
        private const float AmbiguousHigh = 0.8f;

        /// <summary>
        /// Samples the map into a <paramref name="columns"/> x <paramref name="rows"/> character grid.
        /// Row 0 is the top of the map (+Z), matching how a template reads on screen.
        ///
        /// <paramref name="borderCells"/> rings of cells outside the playable bounds are included, so
        /// the outer masses show up as the solid border a template is written with. The boundary-fill
        /// walls have their inner faces exactly on the course edge, so without this they would sample
        /// as nothing at all.
        /// </summary>
        public static string[] Render(AuthoredArena arena, int columns, int rows, int borderCells = 1)
        {
            arena.Collect();

            List<Rect> walls = arena.ResolveWalls(0f, false);

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            borderCells = Mathf.Max(0, borderCells);

            Rect playable = arena.Bounds;
            float cellWidth = playable.width / Mathf.Max(1, columns - 2 * borderCells);
            float cellHeight = playable.height / Mathf.Max(1, rows - 2 * borderCells);

            Rect bounds = Rect.MinMaxRect(
                playable.xMin - cellWidth * borderCells,
                playable.yMin - cellHeight * borderCells,
                playable.xMax + cellWidth * borderCells,
                playable.yMax + cellHeight * borderCells);

            var lines = new string[rows];
            var row = new char[columns];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    // Row 0 is +Z, so the z index counts down as the row index counts up.
                    var cell = Rect.MinMaxRect(
                        bounds.xMin + bounds.width * x / columns,
                        bounds.yMax - bounds.height * (y + 1) / rows,
                        bounds.xMin + bounds.width * (x + 1) / columns,
                        bounds.yMax - bounds.height * y / rows);

                    row[x] = Coverage(cell, walls) >= 0.5f ? Solid : Open;
                }

                lines[y] = new string(row);
            }

            return lines;
        }

        /// <summary>
        /// Compares a built map against its template and returns a report. Any cell the template calls
        /// open but the map fills is geometry that was never specified - the exact failure the map
        /// contract exists to catch.
        /// </summary>
        public static string Compare(AuthoredArena arena, string[] template)
        {
            var sb = new StringBuilder($"[CubeSim] Silhouette check for '{arena.ArenaId}'\n");

            if (template == null || template.Length == 0)
            {
                sb.AppendLine("  ERROR template is empty.");
                return sb.ToString();
            }

            int rows = template.Length;
            int columns = template[0].Length;

            for (int y = 0; y < rows; y++)
            {
                if (template[y].Length == columns) continue;

                sb.AppendLine($"  ERROR template row {y} is {template[y].Length} wide, expected {columns}.");
                return sb.ToString();
            }

            arena.Collect();
            List<Rect> walls = arena.ResolveWalls(0f, false);
            Rect[,] cells = Grid(arena, columns, rows, 1);

            string[] built = Render(arena, columns, rows);

            int extraSolid = 0, missingSolid = 0, ambiguous = 0;
            var firstExtras = new List<string>();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    bool wantSolid = template[y][x] == Solid;
                    bool isSolid = built[y][x] == Solid;
                    if (wantSolid == isSolid) continue;

                    // A wall thinner than a cell straddles two of them and fills neither. That is the
                    // grid disagreeing with the geometry, not geometry that was never specified, so it
                    // is reported separately rather than called a violation.
                    float coverage = Coverage(cells[x, y], walls);
                    if (coverage > AmbiguousLow && coverage < AmbiguousHigh) { ambiguous++; continue; }

                    if (isSolid)
                    {
                        extraSolid++;
                        if (firstExtras.Count < 12) firstExtras.Add($"({x},{y})");
                    }
                    else
                    {
                        missingSolid++;
                    }
                }
            }

            sb.AppendLine($"  grid {columns}x{rows}");
            sb.AppendLine($"  unspecified solid cells: {extraSolid}" +
                          (firstExtras.Count > 0 ? "  at " + string.Join(" ", firstExtras) : ""));
            sb.AppendLine($"  missing solid cells:     {missingSolid}");
            sb.AppendLine($"  partly covered cells:    {ambiguous} (grid does not line up with the geometry)");
            sb.AppendLine("  built silhouette:");
            for (int y = 0; y < rows; y++) sb.AppendLine("    " + built[y]);

            sb.AppendLine(extraSolid == 0 && missingSolid == 0
                ? "  RESULT silhouette matches the template."
                : "  RESULT silhouette does NOT match the template.");

            return sb.ToString();
        }

        /// <summary>The cell rects a silhouette of this size samples, in the same order as Render.</summary>
        private static Rect[,] Grid(AuthoredArena arena, int columns, int rows, int borderCells)
        {
            Rect playable = arena.Bounds;
            float cellWidth = playable.width / Mathf.Max(1, columns - 2 * borderCells);
            float cellHeight = playable.height / Mathf.Max(1, rows - 2 * borderCells);

            Rect bounds = Rect.MinMaxRect(
                playable.xMin - cellWidth * borderCells,
                playable.yMin - cellHeight * borderCells,
                playable.xMax + cellWidth * borderCells,
                playable.yMax + cellHeight * borderCells);

            var cells = new Rect[columns, rows];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    cells[x, y] = Rect.MinMaxRect(
                        bounds.xMin + bounds.width * x / columns,
                        bounds.yMax - bounds.height * (y + 1) / rows,
                        bounds.xMin + bounds.width * (x + 1) / columns,
                        bounds.yMax - bounds.height * y / rows);
                }
            }

            return cells;
        }

        /// <summary>
        /// The fraction of a cell covered by wall.
        ///
        /// Computed exactly rather than point sampled - every wall in these maps is an axis-aligned
        /// box, so the intersection area is arithmetic. Point sampling missed columns thinner than a
        /// cell entirely when they straddled a cell boundary, which is the one thing a silhouette
        /// check must never do.
        /// </summary>
        private static float Coverage(Rect cell, List<Rect> walls)
        {
            float cellArea = cell.width * cell.height;
            if (cellArea <= 0f) return 0f;

            float covered = 0f;

            for (int i = 0; i < walls.Count; i++)
            {
                Rect w = walls[i];

                float overlapX = Mathf.Min(cell.xMax, w.xMax) - Mathf.Max(cell.xMin, w.xMin);
                if (overlapX <= 0f) continue;

                float overlapZ = Mathf.Min(cell.yMax, w.yMax) - Mathf.Max(cell.yMin, w.yMin);
                if (overlapZ <= 0f) continue;

                covered += overlapX * overlapZ;

                // Overlapping walls would otherwise double count; a full cell is as solid as it gets.
                if (covered >= cellArea) return 1f;
            }

            return covered / cellArea;
        }

        [MenuItem("CubeSim/Authoring/Print Arena Silhouette", priority = 130)]
        private static void PrintSelected()
        {
            AuthoredArena arena = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<AuthoredArena>()
                : Object.FindFirstObjectByType<AuthoredArena>();

            if (arena == null)
            {
                Debug.LogWarning("[CubeSim] No AuthoredArena selected or in the scene.");
                return;
            }

            // A cell roughly two metres across: fine enough to show a corridor, coarse enough that a
            // template stays writable by hand.
            int columns = Mathf.Clamp(Mathf.RoundToInt(arena.Bounds.width / 2f) + 2, 8, 120);
            int rows = Mathf.Clamp(Mathf.RoundToInt(arena.Bounds.height / 2f) + 2, 8, 120);

            var sb = new StringBuilder($"[CubeSim] Silhouette of '{arena.ArenaId}' ({columns}x{rows})\n");
            foreach (string line in Render(arena, columns, rows)) sb.AppendLine("    " + line);
            Debug.Log(sb.ToString());
        }
    }
}
