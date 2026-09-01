using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;

namespace CubeSim.Arena
{
    /// <summary>
    /// Everything the runtime needs to know about a built arena, whether it was generated or hand
    /// authored. Downstream systems read this and never branch on arena mode.
    /// </summary>
    public sealed class ArenaRuntime
    {
        public Transform Root { get; }
        public ArenaDefinition Definition { get; }

        /// <summary>Playable rectangle in world XZ, i.e. inside the border walls.</summary>
        public Rect PlayableRect { get; }

        /// <summary>Axis-aligned boxes of every static wall, in world XZ. Used for spawn rejection.</summary>
        public List<Rect> WallRects { get; }

        /// <summary>The reserved open region at the centre. Procedural arenas only.</summary>
        public Rect ClearingRect { get; }

        public bool HasClearing { get; }

        public float GroundY { get; }

        /// <summary>The gap the generator guaranteed between separate walls.</summary>
        public float MinCorridorWidth { get; }

        /// <summary>Set only for authored arenas. Null for procedural ones.</summary>
        public AuthoredArena Authored { get; set; }

        public List<SpawnArea> SpawnAreas { get; set; }
        public List<GoalArea> GoalAreas { get; set; }
        public List<WeaponSpawnArea> WeaponAreas { get; set; }
        public List<HazardArea> Hazards { get; set; }
        public List<FoodArea> FoodAreas { get; set; }
        public PressureTrack Track { get; set; }

        public bool IsAuthored => Authored != null;

        public ArenaRuntime(Transform root, ArenaDefinition definition, Rect playableRect,
            List<Rect> wallRects, float groundY, Rect clearingRect, bool hasClearing, float minCorridorWidth)
        {
            Root = root;
            Definition = definition;
            PlayableRect = playableRect;
            WallRects = wallRects;
            GroundY = groundY;
            ClearingRect = clearingRect;
            HasClearing = hasClearing;
            MinCorridorWidth = minCorridorWidth;
        }

        public Vector3 Center => new Vector3(PlayableRect.center.x, GroundY, PlayableRect.center.y);

        /// <summary>
        /// Drops a wall from the analytic footprint list. Called when a breakable wall opens, so
        /// spawn and weapon-drop validation stop treating the gap as solid.
        /// </summary>
        public void RemoveWallRect(Rect rect)
        {
            for (int i = 0; i < WallRects.Count; i++)
            {
                Rect r = WallRects[i];
                if (Mathf.Abs(r.x - rect.x) > 0.01f || Mathf.Abs(r.y - rect.y) > 0.01f ||
                    Mathf.Abs(r.width - rect.width) > 0.01f || Mathf.Abs(r.height - rect.height) > 0.01f)
                {
                    continue;
                }

                WallRects.RemoveAt(i);
                return;
            }
        }

        /// <summary>True when an XZ box of the given half extent overlaps any static wall.</summary>
        public bool OverlapsWall(Vector2 position, float halfExtent)
        {
            for (int i = 0; i < WallRects.Count; i++)
            {
                Rect r = WallRects[i];
                if (position.x + halfExtent > r.xMin && position.x - halfExtent < r.xMax &&
                    position.y + halfExtent > r.yMin && position.y - halfExtent < r.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the box fits inside the playable rectangle.</summary>
        public bool InsidePlayable(Vector2 position, float halfExtent)
            => position.x - halfExtent >= PlayableRect.xMin && position.x + halfExtent <= PlayableRect.xMax &&
               position.y - halfExtent >= PlayableRect.yMin && position.y + halfExtent <= PlayableRect.yMax;
    }
}
