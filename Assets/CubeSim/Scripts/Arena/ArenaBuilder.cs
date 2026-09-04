using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.Arena
{
    /// <summary>
    /// Turns an <see cref="ArenaDefinition"/> into scene geometry, from either the procedural
    /// generator or an authored prefab. Runs once per episode; nothing here executes during the
    /// simulation loop.
    /// </summary>
    public static class ArenaBuilder
    {
        public static ArenaRuntime Build(ArenaDefinition definition, SimulationRandom random,
            MaterialLibrary materials, float groundY, float racerDiameter,
            AuthoredArenaLibrary arenaLibrary, Transform parent)
        {
            return definition.mode == ArenaMode.Authored
                ? BuildAuthored(definition, materials, groundY, arenaLibrary, parent)
                : BuildProcedural(definition, random, materials, groundY, racerDiameter, parent);
        }

        // ---------------------------------------------------------------- authored

        private static ArenaRuntime BuildAuthored(ArenaDefinition definition, MaterialLibrary materials,
            float groundY, AuthoredArenaLibrary library, Transform parent)
        {
            GameObject prefab = library != null ? library.Find(definition.arenaId) : null;
            if (prefab == null)
            {
                Debug.LogError($"[CubeSim] Authored arena '{definition.arenaId}' not found. " +
                               $"Known ids: {(library != null ? library.DescribeIds() : "no library assigned")}. " +
                               "Falling back to the procedural generator.");

                return BuildProcedural(definition, new SimulationRandom(0), materials, groundY, 1f, parent);
            }

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = "Arena_" + definition.arenaId;

            var authored = instance.GetComponent<AuthoredArena>();
            if (authored == null)
            {
                Debug.LogError($"[CubeSim] Prefab for '{definition.arenaId}' has no AuthoredArena component.");
                authored = instance.AddComponent<AuthoredArena>();
            }

            authored.Collect();

            // Boundary-fill walls swallow their dead space here. The inner faces stay put, so the
            // course the designer drew is exactly the course racers get.
            List<Rect> wallRects = authored.ResolveWalls(groundY, true);

            for (int i = 0; i < authored.Walls.Count; i++)
            {
                ArenaWall wall = authored.Walls[i];
                if (wall != null) wall.PrepareForPlay(materials.Wall);
            }

            // Rotor bars are not ArenaWalls (they rotate, so the fill/resolve pipeline must not
            // touch them) but they still need the wall look and the wall layer to block racers.
            foreach (RotorObstacle rotor in authored.GetComponentsInChildren<RotorObstacle>(true))
            {
                foreach (Transform bar in rotor.transform)
                {
                    bar.gameObject.layer = SimulationLayers.Wall;
                    // A bar that was baked with its own look (the red blade) keeps it; only a
                    // bare primitive falls back to the wall material.
                    var renderer = bar.GetComponent<MeshRenderer>();
                    if (renderer != null &&
                        (renderer.sharedMaterial == null || renderer.sharedMaterial.name.StartsWith("Default")))
                    {
                        renderer.sharedMaterial = materials.Wall;
                    }
                }
            }

            // Crusher slabs move, so they are not ArenaWalls either - but racers must bounce off
            // them, so they sit on the wall layer with whatever look the builder baked.
            foreach (Crusher crusher in authored.GetComponentsInChildren<Crusher>(true))
            {
                crusher.gameObject.layer = SimulationLayers.Wall;
            }

            if (authored.FloorMode == AuthoredFloorMode.FullArena)
            {
                // The floor follows the fill bounds, not the playable bounds: otherwise the padded
                // wall masses would overhang empty space and the camera would see under them.
                BuildFloor(authored.VisualFillBounds, authored.FloorThickness, materials, groundY,
                    instance.transform);
            }

            // Route maps need an obvious destination, so every goal area gets its declared dressing.
            var goalVisuals = new GameObject("GoalVisuals").transform;
            goalVisuals.SetParent(instance.transform, false);

            for (int i = 0; i < authored.GoalAreas.Count; i++)
            {
                GoalVisualFactory.Build(authored.GoalAreas[i], materials, groundY,
                    authored.WallHeight, goalVisuals);
            }

            for (int i = 0; i < authored.Hazards.Count; i++)
            {
                GoalVisualFactory.BuildHazard(authored.Hazards[i], materials, groundY, goalVisuals);
            }

            Rect bounds = authored.Bounds;
            var runtime = new ArenaRuntime(instance.transform, definition, bounds, wallRects, groundY,
                new Rect(), false, authored.DesignedCorridorWidth)
            {
                Authored = authored,
                SpawnAreas = authored.SpawnAreas,
                GoalAreas = authored.GoalAreas,
                WeaponAreas = authored.WeaponAreas,
                Hazards = authored.Hazards,
                FoodAreas = authored.FoodAreas,
                Track = authored.Track
            };

            return runtime;
        }

        // ---------------------------------------------------------------- procedural

        private static ArenaRuntime BuildProcedural(ArenaDefinition definition, SimulationRandom random,
            MaterialLibrary materials, float groundY, float racerDiameter, Transform parent)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            var wallHolder = new GameObject("Walls").transform;
            wallHolder.SetParent(root, false);

            BuildFloor(new Rect(-definition.HalfWidth, -definition.HalfDepth, definition.size.x, definition.size.y),
                definition.floorThickness, materials, groundY, root);

            var borderRects = new List<Rect>(4);
            if (definition.generateBorder) AddBorderWalls(definition, borderRects);

            var wallRects = new List<Rect>(borderRects);
            float minCorridorWidth = definition.generation.ResolveMinimumCorridorWidth(racerDiameter);

            if (definition.layout == ArenaLayoutMode.Generated)
            {
                definition.generation.ApplyProfile();

                List<Rect> generated = ArenaGenerator.Generate(definition, random, racerDiameter,
                    borderRects, out minCorridorWidth, out _);

                wallRects.AddRange(generated);
            }

            for (int i = 0; i < definition.extraWalls.Count; i++)
            {
                WallDefinition w = definition.extraWalls[i];
                wallRects.Add(ToRect(w.center, w.size));
            }

            for (int i = 0; i < wallRects.Count; i++)
            {
                CreateWall(wallRects[i], definition.wallHeight, groundY, materials, wallHolder, i);
            }

            BuildZones(definition, materials, groundY, root);

            CentralClearing clearing = definition.generation.centralClearing;
            bool hasClearing = definition.layout == ArenaLayoutMode.Generated && clearing.enabled;

            return new ArenaRuntime(root, definition, definition.PlayableRect, wallRects, groundY,
                hasClearing ? clearing.Rect : new Rect(), hasClearing, minCorridorWidth);
        }

        // ---------------------------------------------------------------- shared pieces

        private static void BuildFloor(Rect bounds, float thickness, MaterialLibrary materials,
            float groundY, Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            DestroyComponent(floor.GetComponent<Collider>());
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(bounds.center.x, groundY - thickness * 0.5f, bounds.center.y);
            floor.transform.localScale = new Vector3(bounds.width, thickness, bounds.height);
            floor.GetComponent<MeshRenderer>().sharedMaterial = materials.Floor;
        }

        private static void AddBorderWalls(ArenaDefinition d, List<Rect> rects)
        {
            float t = d.wallThickness;
            float hw = d.HalfWidth;
            float hd = d.HalfDepth;

            rects.Add(ToRect(new Vector2(-hw + t * 0.5f, 0f), new Vector2(t, d.size.y)));
            rects.Add(ToRect(new Vector2(hw - t * 0.5f, 0f), new Vector2(t, d.size.y)));
            rects.Add(ToRect(new Vector2(0f, -hd + t * 0.5f), new Vector2(d.size.x - t * 2f, t)));
            rects.Add(ToRect(new Vector2(0f, hd - t * 0.5f), new Vector2(d.size.x - t * 2f, t)));
        }

        private static void CreateWall(Rect rect, float height, float groundY,
            MaterialLibrary materials, Transform parent, int index)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_" + index.ToString("D3");
            wall.layer = SimulationLayers.Wall;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(rect.center.x, groundY + height * 0.5f, rect.center.y);
            wall.transform.localScale = new Vector3(rect.width, height, rect.height);
            wall.GetComponent<MeshRenderer>().sharedMaterial = materials.Wall;
        }

        private static void BuildZones(ArenaDefinition d, MaterialLibrary materials, float groundY, Transform parent)
        {
            if (d.zones.Count == 0) return;

            var holder = new GameObject("Zones").transform;
            holder.SetParent(parent, false);

            for (int i = 0; i < d.zones.Count; i++)
            {
                ZoneDefinition zone = d.zones[i];
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Zone_" + zone.id;
                DestroyComponent(go.GetComponent<Collider>());
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(zone.center.x, groundY + 0.02f, zone.center.y);
                go.transform.localScale = new Vector3(zone.size.x, 0.04f, zone.size.y);
                go.GetComponent<MeshRenderer>().sharedMaterial = materials.GetZoneMaterial(zone);
            }
        }

        private static void DestroyComponent(Object component)
        {
            if (component == null) return;
            if (Application.isPlaying) Object.Destroy(component);
            else Object.DestroyImmediate(component);
        }

        private static Rect ToRect(Vector2 center, Vector2 size)
            => new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
    }
}
