using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Headless check that the four generation profiles really produce different compositions, and
    /// that no arena violates the minimum corridor rule. Runs the generator directly - no scene, no
    /// play mode - so a whole sweep costs milliseconds.
    /// </summary>
    public static class ArenaProfileReport
    {
        [MenuItem("CubeSim/Report Arena Profiles", priority = 41)]
        public static void Report()
        {
            var profiles = new[]
            {
                ArenaGenerationProfile.Sparse,
                ArenaGenerationProfile.Medium,
                ArenaGenerationProfile.Dense,
                ArenaGenerationProfile.Mixed
            };

            var sb = new StringBuilder("[CubeSim] Arena profile sweep (6 seeds each)\n");

            foreach (ArenaGenerationProfile profile in profiles)
            {
                int totalWalls = 0, minWalls = int.MaxValue, maxWalls = 0;
                float totalCoverage = 0f;
                float minGapSeen = float.MaxValue;
                int spacingViolations = 0, clearingViolations = 0, overlaps = 0;
                int uniqueSignatures = 0;
                var signatures = new HashSet<string>();

                for (int s = 0; s < 6; s++)
                {
                    SimulationConfig config = CubeSimSceneBuilder.BuildPrototypeConfig();
                    config.arena.generation.profile = profile;
                    config.arena.generation.ApplyProfile();

                    var random = new SimulationRandom(1000 + s);
                    var border = new List<Rect>();
                    AddBorder(config.arena, border);

                    List<Rect> walls = ArenaGenerator.Generate(config.arena, random,
                        config.racers.cubeSize, border, out float minCorridor, out _,
                        out List<int> structureIds);

                    totalWalls += walls.Count;
                    minWalls = Mathf.Min(minWalls, walls.Count);
                    maxWalls = Mathf.Max(maxWalls, walls.Count);
                    totalCoverage += Coverage(walls, config.arena.PlayableRect);
                    signatures.Add(Signature(walls));

                    Rect keepOut = config.arena.generation.centralClearing.KeepOutRect;
                    var all = new List<Rect>(border);
                    all.AddRange(walls);

                    // Border walls get id -1..-4 so they never count as sharing a structure.
                    var ids = new List<int>();
                    for (int b = 0; b < border.Count; b++) ids.Add(-1 - b);
                    ids.AddRange(structureIds);

                    for (int i = 0; i < walls.Count; i++)
                    {
                        if (config.arena.generation.centralClearing.enabled && Overlaps(walls[i], keepOut))
                            clearingViolations++;
                    }

                    // Any two rects from different structures must be either touching (same structure
                    // join) or at least minCorridor apart. Anything between is an unusable slot.
                    for (int i = 0; i < all.Count; i++)
                    {
                        for (int j = i + 1; j < all.Count; j++)
                        {
                            if (ids[i] == ids[j]) continue; // same structure: joins are intentional

                            float gap = Gap(all[i], all[j]);
                            if (gap < -1e-3f) { overlaps++; continue; }
                            if (gap <= 1e-3f) continue;              // touching: an intentional join
                            if (gap < minCorridor - 1e-3f) spacingViolations++;
                            minGapSeen = Mathf.Min(minGapSeen, gap);
                        }
                    }
                }

                uniqueSignatures = signatures.Count;
                sb.AppendLine(
                    $"  {profile,-7} walls avg={totalWalls / 6f:F1} range=[{minWalls}..{maxWalls}] " +
                    $"coverage={totalCoverage / 6f * 100f:F1}% uniqueLayouts={uniqueSignatures}/6 " +
                    $"minGap={minGapSeen:F2} spacingViolations={spacingViolations} " +
                    $"clearingViolations={clearingViolations} overlaps={overlaps}");
            }

            Debug.Log(sb.ToString());
        }

        private static void AddBorder(ArenaDefinition d, List<Rect> rects)
        {
            float t = d.wallThickness;
            float hw = d.HalfWidth;
            float hd = d.HalfDepth;

            rects.Add(Make(new Vector2(-hw + t * 0.5f, 0f), new Vector2(t, d.size.y)));
            rects.Add(Make(new Vector2(hw - t * 0.5f, 0f), new Vector2(t, d.size.y)));
            rects.Add(Make(new Vector2(0f, -hd + t * 0.5f), new Vector2(d.size.x - t * 2f, t)));
            rects.Add(Make(new Vector2(0f, hd - t * 0.5f), new Vector2(d.size.x - t * 2f, t)));
        }

        private static Rect Make(Vector2 c, Vector2 s)
            => new Rect(c.x - s.x * 0.5f, c.y - s.y * 0.5f, s.x, s.y);

        private static float Coverage(List<Rect> walls, Rect play)
        {
            float area = 0f;
            for (int i = 0; i < walls.Count; i++) area += walls[i].width * walls[i].height;
            return area / (play.width * play.height);
        }

        /// <summary>Separation between two rects. Negative means they overlap.</summary>
        private static float Gap(Rect a, Rect b)
        {
            float dx = Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax);
            float dz = Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax);

            if (dx >= 0f && dz >= 0f) return Mathf.Sqrt(dx * dx + dz * dz);
            if (dx >= 0f) return dx;
            if (dz >= 0f) return dz;
            return Mathf.Max(dx, dz); // both negative: overlapping
        }

        private static bool Overlaps(Rect a, Rect b)
            => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

        private static string Signature(List<Rect> walls)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < walls.Count; i++)
            {
                sb.Append(walls[i].xMin.ToString("F2")).Append(',')
                  .Append(walls[i].yMin.ToString("F2")).Append(';');
            }

            return sb.ToString();
        }
    }
}
