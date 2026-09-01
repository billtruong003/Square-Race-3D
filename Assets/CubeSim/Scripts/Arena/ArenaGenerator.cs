using System.Collections.Generic;
using UnityEngine;
using CubeSim.Core;

namespace CubeSim.Arena
{
    /// <summary>
    /// Composes an arena out of structures - long walls, L corners, partial rooms, corridor pairs and
    /// isolated blocks - instead of stamping one repeating pattern.
    ///
    /// Two hard rules are enforced for every placement:
    ///   1. separate walls always keep at least minimumCorridorWidth between them, so no useless slot
    ///      that a racer cannot enter is ever generated (and walls never accidentally overlap);
    ///   2. nothing is placed inside the reserved central clearing.
    ///
    /// Everything is drawn from the run seed, so a profile plus a seed reproduces the same arena.
    /// </summary>
    public static class ArenaGenerator
    {
        public static List<Rect> Generate(ArenaDefinition definition, SimulationRandom random,
            float racerDiameter, List<Rect> borderRects, out float minCorridorWidth,
            out List<Rect> openRegions)
            => Generate(definition, random, racerDiameter, borderRects, out minCorridorWidth,
                out openRegions, out _);

        /// <summary>
        /// <paramref name="structureIds"/> runs parallel to the returned rects: parts sharing an id
        /// belong to the same structure and are allowed to touch or join at a corner.
        /// </summary>
        public static List<Rect> Generate(ArenaDefinition definition, SimulationRandom random,
            float racerDiameter, List<Rect> borderRects, out float minCorridorWidth,
            out List<Rect> openRegions, out List<int> structureIds)
        {
            structureIds = new List<int>(64);
            ArenaGenerationSettings settings = definition.generation;
            minCorridorWidth = settings.ResolveMinimumCorridorWidth(racerDiameter);

            var placed = new List<Rect>(64);
            var committed = new List<Rect>(64);

            // Border walls take part in the spacing test so nothing crowds the arena edge.
            placed.AddRange(borderRects);

            Rect play = definition.PlayableRect;
            openRegions = BuildOpenRegions(settings, random, play);

            var keepOut = new List<Rect>(openRegions);
            if (settings.centralClearing.enabled) keepOut.Add(settings.centralClearing.KeepOutRect);

            List<Vector2> clusters = BuildClusters(settings, random, play);

            var parts = new List<Rect>(4);
            int budget = Mathf.Max(0, settings.wallBudget);

            for (int i = 0; i < budget; i++)
            {
                bool committedOne = false;

                // Choose the type once per slot. Re-rolling on every retry would quietly bias the
                // whole arena toward whichever structure is smallest and therefore easiest to fit.
                StructureKind kind = PickKind(settings, random);

                int attempts = Mathf.Max(1, settings.maxPlacementAttempts);
                for (int attempt = 0; attempt < attempts && !committedOne; attempt++)
                {
                    // Clusters get the first pass. If they are saturated, fall back to spreading, so
                    // a clustered profile still fills the rest of the arena instead of giving up.
                    bool allowCluster = attempt < attempts / 2;
                    Vector2 anchor = PickAnchor(settings, random, play, clusters, placed, allowCluster);
                    BuildStructure(settings, random, kind, anchor, parts);

                    if (IsPlaceable(parts, placed, keepOut, play, minCorridorWidth))
                    {
                        for (int p = 0; p < parts.Count; p++)
                        {
                            placed.Add(parts[p]);
                            committed.Add(parts[p]);
                            structureIds.Add(i);
                        }

                        committedOne = true;
                    }
                }
            }

            return committed;
        }

        /// <summary>Large rectangles the generator deliberately leaves empty, for readable open space.</summary>
        private static List<Rect> BuildOpenRegions(ArenaGenerationSettings settings, SimulationRandom random, Rect play)
        {
            var regions = new List<Rect>(4);
            if (settings.openAreaBias <= 0.01f) return regions;

            // Split the requested empty share into two or three blobs so it does not read as one hole.
            int count = settings.openAreaBias > 0.35f ? 3 : 2;
            float areaShare = settings.openAreaBias / count;

            for (int i = 0; i < count; i++)
            {
                float w = Mathf.Sqrt(play.width * play.height * areaShare) * random.Range(0.8f, 1.3f);
                float h = w * random.Range(0.55f, 1.1f);
                w = Mathf.Min(w, play.width * 0.32f);
                h = Mathf.Min(h, play.height * 0.5f);

                float x = random.Range(play.xMin + w * 0.5f, play.xMax - w * 0.5f);
                float y = random.Range(play.yMin + h * 0.5f, play.yMax - h * 0.5f);
                regions.Add(new Rect(x - w * 0.5f, y - h * 0.5f, w, h));
            }

            return regions;
        }

        private static List<Vector2> BuildClusters(ArenaGenerationSettings settings, SimulationRandom random, Rect play)
        {
            var clusters = new List<Vector2>(Mathf.Max(0, settings.clusterCount));
            for (int i = 0; i < settings.clusterCount; i++)
            {
                clusters.Add(new Vector2(
                    random.Range(play.xMin + 3f, play.xMax - 3f),
                    random.Range(play.yMin + 3f, play.yMax - 3f)));
            }

            return clusters;
        }

        private const int AnchorCandidates = 3;

        private static Vector2 PickAnchor(ArenaGenerationSettings settings, SimulationRandom random,
            Rect play, List<Vector2> clusters, List<Rect> placed, bool allowCluster)
        {
            if (allowCluster && clusters.Count > 0 && random.NextFloat() < settings.clusterShare)
            {
                Vector2 c = clusters[random.Range(0, clusters.Count)];
                float r = settings.clusterRadius;
                return new Vector2(
                    Mathf.Clamp(c.x + random.Range(-r, r), play.xMin, play.xMax),
                    Mathf.Clamp(c.y + random.Range(-r, r), play.yMin, play.yMax));
            }

            // Sample a few points and keep the emptiest. Without this, uniform sampling clumps and
            // whole quadrants of the arena end up bare.
            Vector2 best = Vector2.zero;
            float bestDistance = -1f;

            for (int i = 0; i < AnchorCandidates; i++)
            {
                var candidate = new Vector2(random.Range(play.xMin, play.xMax), random.Range(play.yMin, play.yMax));
                float nearest = NearestWallDistance(candidate, placed);
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }

            return best;
        }

        private static float NearestWallDistance(Vector2 point, List<Rect> placed)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < placed.Count; i++)
            {
                Vector2 c = placed[i].center;
                float d = (c - point).sqrMagnitude;
                if (d < nearest) nearest = d;
            }

            return nearest;
        }

        private enum StructureKind { LongWall, LShape, Room, CorridorPair, Block }

        private static StructureKind PickKind(ArenaGenerationSettings settings, SimulationRandom random)
        {
            StructureWeights w = settings.structureWeights;
            float total = w.Total;
            float roll = random.Range(0f, total <= 0f ? 1f : total);

            if ((roll -= Mathf.Max(0f, w.longWall)) < 0f) return StructureKind.LongWall;
            if ((roll -= Mathf.Max(0f, w.lShape)) < 0f) return StructureKind.LShape;
            if ((roll -= Mathf.Max(0f, w.room)) < 0f) return StructureKind.Room;
            if ((roll -= Mathf.Max(0f, w.corridorPair)) < 0f) return StructureKind.CorridorPair;
            return StructureKind.Block;
        }

        private static void BuildStructure(ArenaGenerationSettings settings, SimulationRandom random,
            StructureKind kind, Vector2 anchor, List<Rect> parts)
        {
            parts.Clear();

            float thickness = random.Range(settings.wallThicknessRange.x, settings.wallThicknessRange.y);
            bool vertical = random.NextFloat() < settings.orientationBias;

            switch (kind)
            {
                case StructureKind.LongWall: BuildLongWall(settings, random, anchor, thickness, vertical, parts); break;
                case StructureKind.LShape: BuildLShape(settings, random, anchor, thickness, vertical, parts); break;
                case StructureKind.Room: BuildRoom(settings, random, anchor, thickness, parts); break;
                case StructureKind.CorridorPair: BuildCorridorPair(settings, random, anchor, thickness, vertical, parts); break;
                default: BuildBlock(settings, random, anchor, thickness, parts); break;
            }
        }

        private static void BuildLongWall(ArenaGenerationSettings s, SimulationRandom random,
            Vector2 anchor, float thickness, bool vertical, List<Rect> parts)
        {
            float length = random.Range(s.wallLengthRange.x, s.wallLengthRange.y);
            parts.Add(MakeRect(anchor, vertical ? thickness : length, vertical ? length : thickness));
        }

        private static void BuildLShape(ArenaGenerationSettings s, SimulationRandom random,
            Vector2 anchor, float thickness, bool vertical, List<Rect> parts)
        {
            float armA = random.Range(s.wallLengthRange.x, s.wallLengthRange.y * 0.7f);
            float armB = random.Range(s.wallLengthRange.x, s.wallLengthRange.y * 0.7f);
            float signA = random.NextBool() ? 1f : -1f;
            float signB = random.NextBool() ? 1f : -1f;

            if (vertical)
            {
                parts.Add(MakeRect(anchor, thickness, armA));
                // Horizontal arm starting at one end of the vertical arm, sharing the corner.
                Vector2 corner = anchor + new Vector2(0f, signA * (armA - thickness) * 0.5f);
                parts.Add(MakeRect(corner + new Vector2(signB * (armB - thickness) * 0.5f, 0f), armB, thickness));
            }
            else
            {
                parts.Add(MakeRect(anchor, armA, thickness));
                Vector2 corner = anchor + new Vector2(signA * (armA - thickness) * 0.5f, 0f);
                parts.Add(MakeRect(corner + new Vector2(0f, signB * (armB - thickness) * 0.5f), thickness, armB));
            }
        }

        /// <summary>Three sides of a rectangle - a room with one open face.</summary>
        private static void BuildRoom(ArenaGenerationSettings s, SimulationRandom random,
            Vector2 anchor, float thickness, List<Rect> parts)
        {
            float width = random.Range(s.wallLengthRange.x + 2f, Mathf.Max(s.wallLengthRange.x + 3f, s.wallLengthRange.y));
            float height = random.Range(s.wallLengthRange.x + 2f, Mathf.Max(s.wallLengthRange.x + 3f, s.wallLengthRange.y));
            int openSide = random.Range(0, 4);

            float hw = width * 0.5f;
            float hh = height * 0.5f;

            if (openSide != 0) parts.Add(MakeRect(anchor + new Vector2(0f, hh), width, thickness));   // top
            if (openSide != 1) parts.Add(MakeRect(anchor + new Vector2(0f, -hh), width, thickness));  // bottom
            if (openSide != 2) parts.Add(MakeRect(anchor + new Vector2(-hw, 0f), thickness, height)); // left
            if (openSide != 3) parts.Add(MakeRect(anchor + new Vector2(hw, 0f), thickness, height));  // right
        }

        /// <summary>Two parallel walls whose gap is a deliberate, guaranteed-traversable corridor.</summary>
        private static void BuildCorridorPair(ArenaGenerationSettings s, SimulationRandom random,
            Vector2 anchor, float thickness, bool vertical, List<Rect> parts)
        {
            float length = random.Range(s.wallLengthRange.x, s.wallLengthRange.y);
            float gap = s.ResolveMinimumCorridorWidth(1f) * random.Range(1f, 1.6f);
            float offset = (gap + thickness) * 0.5f;

            if (vertical)
            {
                parts.Add(MakeRect(anchor + new Vector2(-offset, 0f), thickness, length));
                parts.Add(MakeRect(anchor + new Vector2(offset, 0f), thickness, length));
            }
            else
            {
                parts.Add(MakeRect(anchor + new Vector2(0f, -offset), length, thickness));
                parts.Add(MakeRect(anchor + new Vector2(0f, offset), length, thickness));
            }
        }

        private static void BuildBlock(ArenaGenerationSettings s, SimulationRandom random,
            Vector2 anchor, float thickness, List<Rect> parts)
        {
            float size = random.Range(thickness, Mathf.Max(thickness * 1.2f, s.wallLengthRange.x));
            parts.Add(MakeRect(anchor, size, size));
        }

        private static Rect MakeRect(Vector2 center, float width, float height)
            => new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);

        /// <summary>
        /// A structure is placeable when every part sits inside the playfield, avoids the reserved
        /// regions, and keeps the minimum corridor width from every previously placed wall. Parts of
        /// the same structure are exempt - those joins are intentional.
        /// </summary>
        private static bool IsPlaceable(List<Rect> parts, List<Rect> placed, List<Rect> keepOut,
            Rect play, float minCorridorWidth)
        {
            if (parts.Count == 0) return false;

            for (int i = 0; i < parts.Count; i++)
            {
                Rect part = parts[i];

                if (part.xMin < play.xMin || part.xMax > play.xMax ||
                    part.yMin < play.yMin || part.yMax > play.yMax)
                {
                    return false;
                }

                for (int k = 0; k < keepOut.Count; k++)
                {
                    if (Overlaps(part, keepOut[k])) return false;
                }

                for (int p = 0; p < placed.Count; p++)
                {
                    if (Overlaps(Inflate(part, minCorridorWidth), placed[p])) return false;
                }
            }

            return true;
        }

        private static Rect Inflate(Rect r, float amount)
            => new Rect(r.xMin - amount, r.yMin - amount, r.width + amount * 2f, r.height + amount * 2f);

        private static bool Overlaps(Rect a, Rect b)
            => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
    }
}
