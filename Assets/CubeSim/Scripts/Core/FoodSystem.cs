using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Core
{
    /// <summary>
    /// The Pet Survival feeding grounds: food areas fill with pellets on a fixed grid, and a pet
    /// that wanders over one eats it. Eating only moves a per-racer counter - it never touches
    /// movement, health or combat - so the system layers onto the simulation the same way goals do,
    /// and the pellet layout is a pure function of the map, identical every run.
    ///
    /// On screen it is the reference channels' fruit fields: a carpet of collectibles that visibly
    /// thins as the swarm grazes through it, with the leaderboard keeping score.
    /// </summary>
    public sealed class FoodSystem
    {
        private sealed class Pellet
        {
            public Vector3 Position;
            public GameObject Visual;
            public bool Eaten;
        }

        private static readonly Color[] PelletColors =
        {
            new Color(0.95f, 0.25f, 0.2f),   // apple red
            new Color(0.45f, 0.85f, 0.25f),  // pear green
            new Color(1f, 0.62f, 0.15f),     // orange
        };

        private readonly List<Pellet> _pellets = new List<Pellet>();
        private readonly float _groundY;

        /// <summary>(racer, pellet position) - fired as it is eaten. Presentation hangs off this.</summary>
        public event Action<Racer, Vector3> OnEaten;

        public int PelletCount => _pellets.Count;
        public int EatenCount { get; private set; }

        public FoodSystem(ArenaRuntime arena, MaterialLibrary materials, float groundY, Transform parent)
        {
            _groundY = groundY;

            List<FoodArea> areas = arena.FoodAreas;
            if (areas == null || areas.Count == 0) return;

            var root = new GameObject("Food").transform;
            root.SetParent(parent, false);

            int colorIndex = 0;
            foreach (FoodArea area in areas)
            {
                if (area == null) continue;

                Rect r = area.Footprint;
                float spacing = area.Spacing;

                // A fixed grid walked in a fixed order: the layout is part of the map, not the run.
                for (float x = r.xMin + spacing * 0.5f; x <= r.xMax - spacing * 0.4f; x += spacing)
                {
                    for (float z = r.yMin + spacing * 0.5f; z <= r.yMax - spacing * 0.4f; z += spacing)
                    {
                        var position = new Vector3(x, groundY, z);
                        if (arena.OverlapsWall(new Vector2(x, z), 0.4f)) continue;

                        Color color = PelletColors[colorIndex++ % PelletColors.Length];
                        _pellets.Add(new Pellet
                        {
                            Position = position,
                            Visual = BuildPellet(position, color, materials, root)
                        });
                    }
                }
            }
        }

        /// <summary>One pass over living racers. Called from the runner's step, after movement.</summary>
        public void Step(Racer[] racers)
        {
            if (_pellets.Count == 0 || EatenCount >= _pellets.Count) return;

            for (int p = 0; p < _pellets.Count; p++)
            {
                Pellet pellet = _pellets[p];
                if (pellet.Eaten) continue;

                for (int i = 0; i < racers.Length; i++)
                {
                    Racer racer = racers[i];
                    if (!racer.IsActive) continue;

                    float reach = racer.HalfExtent + 0.3f;
                    if (Mathf.Abs(racer.Position.x - pellet.Position.x) > reach) continue;
                    if (Mathf.Abs(racer.Position.z - pellet.Position.z) > reach) continue;

                    pellet.Eaten = true;
                    EatenCount++;
                    racer.FoodEaten++;

                    if (pellet.Visual != null) pellet.Visual.SetActive(false);
                    OnEaten?.Invoke(racer, pellet.Position);
                    break;
                }
            }
        }

        private GameObject BuildPellet(Vector3 position, Color color, MaterialLibrary materials,
            Transform parent)
        {
            GameObject pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pellet.name = "Pellet";

            var collider = pellet.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }

            pellet.transform.SetParent(parent, false);
            pellet.transform.localPosition = position + new Vector3(0f, 0.42f, 0f);
            pellet.transform.localScale = Vector3.one * 0.85f;

            pellet.GetComponent<MeshRenderer>().sharedMaterial =
                materials.GetGoalMaterial("food_" + ColorUtility.ToHtmlStringRGB(color), color, 0.75f);

            return pellet;
        }
    }
}
