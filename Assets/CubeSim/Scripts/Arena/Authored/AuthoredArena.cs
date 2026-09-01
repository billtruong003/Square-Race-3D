using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Arena.Authored
{
    public enum AuthoredFloorMode
    {
        /// <summary>One slab under the whole arena. Non-playable space is hidden by fill walls.</summary>
        FullArena = 0,

        /// <summary>No floor is generated; the map supplies its own.</summary>
        None = 1
    }

    /// <summary>
    /// Root of a hand-built map. Everything else - walls, spawn areas, goal, weapon areas, hazards,
    /// the pressure track - is a child component this collects at build time.
    ///
    /// Adding a new map means duplicating this prefab and moving objects. No new runtime code.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AuthoredArena : MonoBehaviour
    {
        [Tooltip("Id an episode config references via arena.arenaId.")]
        [SerializeField] private string arenaId = "Authored01";

        [Tooltip("Full extent of the map in metres: x = width (X), y = depth (Z).")]
        [SerializeField] private Vector2 size = new Vector2(60f, 40f);

        [SerializeField] private float wallHeight = 2.6f;
        [SerializeField] private float floorThickness = 0.5f;
        [SerializeField] private AuthoredFloorMode floorMode = AuthoredFloorMode.FullArena;

        [Tooltip("Reported as the corridor width this map was designed around. Used by validation.")]
        [SerializeField] private float designedCorridorWidth = 4f;

        [Tooltip("Metres the boundary-fill masses run past the arena edge, on every side. This only " +
                 "grows walls outward into dead space - the inner playable faces never move - so the " +
                 "camera sees solid rock instead of the horizon behind the map.")]
        [SerializeField] private float visualFillPadding = 0f;

        public string ArenaId => arenaId;
        public Vector2 Size => size;
        public float WallHeight => wallHeight;
        public float FloorThickness => floorThickness;
        public AuthoredFloorMode FloorMode => floorMode;
        public float DesignedCorridorWidth => designedCorridorWidth;

        public float VisualFillPadding => visualFillPadding;

        /// <summary>The playable extent. Pressure, spawns and validation all measure against this.</summary>
        public Rect Bounds => new Rect(-size.x * 0.5f, -size.y * 0.5f, size.x, size.y);

        /// <summary>
        /// How far boundary-fill masses are allowed to grow. Padding is purely presentational: it
        /// only ever adds thickness on the dead-space side of a wall, so <see cref="Bounds"/> stays
        /// the authoritative playfield.
        /// </summary>
        public Rect VisualFillBounds
        {
            get
            {
                float pad = Mathf.Max(0f, visualFillPadding);
                Rect b = Bounds;
                return Rect.MinMaxRect(b.xMin - pad, b.yMin - pad, b.xMax + pad, b.yMax + pad);
            }
        }

        public void SetVisualFillPadding(float value) => visualFillPadding = Mathf.Max(0f, value);

        public List<ArenaWall> Walls { get; } = new List<ArenaWall>();
        public List<SpawnArea> SpawnAreas { get; } = new List<SpawnArea>();
        public List<GoalArea> GoalAreas { get; } = new List<GoalArea>();
        public List<WeaponSpawnArea> WeaponAreas { get; } = new List<WeaponSpawnArea>();
        public List<HazardArea> Hazards { get; } = new List<HazardArea>();
        public List<FoodArea> FoodAreas { get; } = new List<FoodArea>();
        public PressureTrack Track { get; private set; }

        /// <summary>Gathers every authored component under this root, in hierarchy order.</summary>
        public void Collect()
        {
            Walls.Clear();
            SpawnAreas.Clear();
            GoalAreas.Clear();
            WeaponAreas.Clear();
            Hazards.Clear();
            FoodAreas.Clear();

            GetComponentsInChildren(true, Walls);
            GetComponentsInChildren(true, SpawnAreas);
            GetComponentsInChildren(true, GoalAreas);
            GetComponentsInChildren(true, WeaponAreas);
            GetComponentsInChildren(true, Hazards);
            GetComponentsInChildren(true, FoodAreas);

            Track = GetComponentInChildren<PressureTrack>(true);
        }

        /// <summary>
        /// Resolves every wall's final footprint. Boundary-fill walls swallow the dead space behind
        /// them here, which is what turns a set of thin bars into solid mass.
        /// </summary>
        public List<Rect> ResolveWalls(float groundY, bool applyToTransform)
        {
            var rects = new List<Rect>(Walls.Count);
            Rect fillBounds = VisualFillBounds;

            for (int i = 0; i < Walls.Count; i++)
            {
                ArenaWall wall = Walls[i];
                if (wall == null || !wall.gameObject.activeSelf) continue;

                rects.Add(wall.Resolve(fillBounds, wallHeight, groundY, applyToTransform));
            }

            return rects;
        }

        private void OnDrawGizmos()
        {
            Rect b = Bounds;
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(b.center.x, 0f, b.center.y), new Vector3(b.width, 0.1f, b.height));
        }
    }
}
