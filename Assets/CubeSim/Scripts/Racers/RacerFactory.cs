using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.Racers
{
    /// <summary>
    /// Resolves the racer roster from config plus the run seed and spawns each racer as a simulation
    /// root with a cosmetic model child. Runs once per episode - none of this touches the loop.
    ///
    /// Hierarchy per racer:
    ///   Racer_XX          (simulation root: position, collision extent, direction, health)
    ///     Visual          (RacerVisual: animator, facing, weapon socket)
    ///       Skeleton      (the model instance)
    /// </summary>
    public static class RacerFactory
    {
        private const int MaxSpawnAttempts = 240;

        public static Racer[] Build(RacerSetup setup, ArenaRuntime arena, SimulationRandom random,
            VisualTheme theme, MaterialLibrary materials, RacerVisualLibrary visuals, Transform parent)
        {
            List<RacerDefinition> definitions = ResolveDefinitions(setup, arena, random);

            var holder = new GameObject("Racers").transform;
            holder.SetParent(parent, false);

            Rect spawnBounds = arena.PlayableRect;
            var block = new MaterialPropertyBlock();
            var racers = new Racer[definitions.Count];

            for (int i = 0; i < definitions.Count; i++)
            {
                // Resolved per racer: the "Pets" roster deals a different creature to each index.
                RacerVisualLibrary.Entry entry = visuals != null ? visuals.FindForIndex(setup.visual, i) : null;
                RacerDefinition definition = definitions[i];
                float size = definition.size > 0f ? definition.size : setup.cubeSize;
                float half = size * 0.5f;
                Color color = ResolveColor(definition, setup, theme, i);

                var root = new GameObject(definition.id);
                root.layer = SimulationLayers.Racer;
                root.transform.SetParent(holder, false);

                RacerVisual visual = BuildVisual(root.transform, entry, setup, size, color, materials, block);

                if (setup.trail != null && setup.trail.enabled)
                {
                    // The trail hangs off the simulation root, never off a bone, so it follows the
                    // mover's history rather than the animation.
                    RacerTrail trail = RacerTrail.Create(holder, setup.trail, materials.Trail, color, size,
                        SimulationLayers.Racer);
                    visual.AttachTrail(trail);
                }

                // The weapon rides beside the racer rather than in a hand, so it stays readable.
                visual.AttachAnchor(WeaponAnchor.Create(root.transform, setup.weaponAnchor, size,
                    SimulationLayers.Racer));

                // One call sets the model tint and the trail together - no independent colour config.
                visual.SetColor(color, theme.racerEmission, setup.tintModels);

                // The eye cube is one model for everyone, so identity comes from the colour: the
                // leaderboard shouts RED and BLUE the way the reference channels do.
                string displayName = entry != null && string.Equals(entry.id, "EyeCube",
                        System.StringComparison.OrdinalIgnoreCase)
                    ? ColorNames.NameFor(color)
                    : entry != null && !string.IsNullOrEmpty(entry.displayName)
                        ? entry.displayName
                        : entry != null && entry.id != null && entry.id.StartsWith("Pet_")
                            ? entry.id.Substring(4).ToUpperInvariant()
                            : definition.id;

                var racer = new Racer(i, definition.id, root.transform, visual)
                {
                    DisplayName = displayName,
                    Portrait = entry != null ? entry.portrait : null,
                    Position = new Vector3(definition.spawnPosition.x, arena.GroundY + half, definition.spawnPosition.y),
                    Direction = PlanarMath.DirectionFromAngle(definition.startAngle),
                    Speed = definition.speed > 0f ? definition.speed : setup.speed,
                    HalfExtent = half,
                    Team = definition.team,
                    Color = color,
                    PaletteIndex = definition.paletteIndex >= 0 ? definition.paletteIndex : i,
                    MaxHealth = Mathf.Max(1f, setup.maxHealth),
                    Health = Mathf.Max(1f, setup.maxHealth)
                };

                racer.PushToTransform();
                visual?.SnapToDirection(racer.Direction);
                racers[i] = racer;
            }

            return racers;
        }

        /// <summary>
        /// Builds the cosmetic child. The model instance gets its own animator controller, so the
        /// source prefab and the asset pack's controller are never touched.
        /// </summary>
        private static RacerVisual BuildVisual(Transform root, RacerVisualLibrary.Entry entry,
            RacerSetup setup, float size, Color color, MaterialLibrary materials, MaterialPropertyBlock block)
        {
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root, false);
            var visual = visualGo.AddComponent<RacerVisual>();

            if (entry == null || entry.prefab == null)
            {
                Transform cube = BuildCubeFallback(visualGo.transform, size, color, materials, block);
                visual.Bind(null, cube, null);
                return visual;
            }

            GameObject model = Object.Instantiate(entry.prefab, visualGo.transform);
            model.name = "Model";

            // Asset-pack models can carry their own cameras, listeners and lights (the Kenney fox
            // FBX ships a camera node). Ten racers each contributing a camera and an audio listener
            // is a broken frame and a console flood, so anything scene-level is stripped here.
            foreach (Camera stray in model.GetComponentsInChildren<Camera>(true)) Object.Destroy(stray.gameObject);
            foreach (AudioListener stray in model.GetComponentsInChildren<AudioListener>(true)) Object.Destroy(stray);
            foreach (Light stray in model.GetComponentsInChildren<Light>(true)) Object.Destroy(stray);

            if (model.GetComponentInChildren<EyeCubeVisual>(true) != null)
            {
                // The eye cube is a centre-pivot unit cube: fit its edge to the collision box and
                // sit it exactly at the box centre. The authored proportions inside the prefab
                // (the hand-tuned eye plane) only ever scale uniformly with it.
                float edge = size * Mathf.Max(0.05f, setup.racerVisualScale)
                             * Mathf.Max(0.01f, entry.scaleMultiplier);
                model.transform.localScale = Vector3.one * edge;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Visual size is deliberately decoupled from the collision box: making racers read
                // bigger must never change corridor legality or the crush rules.
                float targetHeight = size * Mathf.Max(0.1f, setup.visualHeightRatio)
                                     * Mathf.Max(0.05f, setup.racerVisualScale);

                float nativeHeight = Mathf.Max(0.01f, entry.nativeHeight);
                float scale = targetHeight / nativeHeight * Mathf.Max(0.01f, entry.scaleMultiplier);

                model.transform.localScale = Vector3.one * scale;
                // The simulation root sits at box centre; the model's feet belong on the ground.
                model.transform.localPosition = new Vector3(0f, -size * 0.5f + entry.yOffset, 0f);
            }

            var animator = model.GetComponent<Animator>();
            if (animator != null && entry.animatorController != null)
            {
                animator.runtimeAnimatorController = entry.animatorController;
            }

            Transform hand = ResolveHandBone(animator, model.transform, entry.handBoneName);
            visual.Bind(animator, model.transform, hand);

            return visual;
        }

        private static Transform ResolveHandBone(Animator animator, Transform model, string boneName)
        {
            if (!string.IsNullOrEmpty(boneName))
            {
                Transform named = FindDeep(model, boneName);
                if (named != null) return named;
            }

            if (animator != null && animator.isHuman)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) return hand;
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform BuildCubeFallback(Transform parent, float size, Color color,
            MaterialLibrary materials, MaterialPropertyBlock block)
        {
            var go = new GameObject("Cube");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;

            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshCache.Cube;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Racer;
            materials.ApplyRacerColor(renderer, block, color);

            return go.transform;
        }

        private static List<RacerDefinition> ResolveDefinitions(RacerSetup setup, ArenaRuntime arena,
            SimulationRandom random)
        {
            if (setup.explicitRacers != null && setup.explicitRacers.Count > 0)
            {
                return new List<RacerDefinition>(setup.explicitRacers);
            }

            bool bySlot = setup.paletteIndices != null && setup.paletteIndices.Count > 0;
            int count = bySlot ? setup.paletteIndices.Count : Mathf.Max(0, setup.count);
            var definitions = new List<RacerDefinition>(count);
            float half = setup.cubeSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                definitions.Add(new RacerDefinition
                {
                    id = "Racer_" + i.ToString("D2"),
                    team = ResolveTeam(setup, i, count, random),
                    spawnPosition = setup.placement == SpawnPlacement.SpawnSlots
                        ? ResolveSlotSpawn(arena, i, count, half)
                        : ResolveSpawn(setup, arena, random, i, half),
                    startAngle = ResolveAngle(setup, random, i, count),
                    paletteIndex = bySlot ? setup.paletteIndices[i] : -1
                });
            }

            return definitions;
        }

        private static int ResolveTeam(RacerSetup setup, int index, int count, SimulationRandom random)
        {
            int teamCount = Mathf.Max(1, setup.teams.Count);
            switch (setup.teamAssignment)
            {
                case TeamAssignment.Blocks:
                    return Mathf.Min(teamCount - 1, index * teamCount / Mathf.Max(1, count));
                case TeamAssignment.Random:
                    return random.Range(0, teamCount);
                default:
                    return index % teamCount;
            }
        }

        private static float ResolveAngle(RacerSetup setup, SimulationRandom random, int index, int count)
        {
            switch (setup.startDirectionMode)
            {
                case StartDirectionMode.Explicit:
                    if (setup.startAngles.Count > 0) return setup.startAngles[index % setup.startAngles.Count];
                    break;
                case StartDirectionMode.Fan:
                    return 360f * index / Mathf.Max(1, count);
            }

            return PlanarMath.AngleFromDirection(random.NextPlanarDirectionBiased(setup.minAxisAngle));
        }

        /// <summary>
        /// Seeded rejection sampling. Falls back to the last candidate if the arena is so dense that
        /// no clear point turns up, which keeps a bad config from hanging the build.
        /// </summary>
        private static Vector2 ResolveSpawn(RacerSetup setup, ArenaRuntime arena, SimulationRandom random,
            int index, float halfExtent)
        {
            float clearance = halfExtent + Mathf.Max(0f, setup.spawnClearance);
            Rect area = ResolveSpawnArea(setup, arena, index);

            Vector2 candidate = area.center;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                candidate = new Vector2(
                    random.Range(area.xMin + clearance, area.xMax - clearance),
                    random.Range(area.yMin + clearance, area.yMax - clearance));

                if (!arena.OverlapsWall(candidate, clearance)) return candidate;
            }

            Debug.LogWarning($"[CubeSim] No clear spawn found for racer {index}; using a fallback point.");
            return candidate;
        }

        /// <summary>
        /// Deals racers round robin to the spawn areas and spaces them evenly along each area's long
        /// axis, so both sides line up as a starting grid rather than scattering.
        /// </summary>
        private static Vector2 ResolveSlotSpawn(ArenaRuntime arena, int index, int count, float halfExtent)
        {
            List<CubeSim.Arena.Authored.SpawnArea> areas = arena.SpawnAreas;
            if (areas == null || areas.Count == 0) return arena.PlayableRect.center;

            int areaIndex = index % areas.Count;
            Rect area = areas[areaIndex].Footprint;

            // How many racers land in this area, and which slot this one takes.
            int perArea = count / areas.Count + (areaIndex < count % areas.Count ? 1 : 0);
            int slot = index / areas.Count;
            if (perArea <= 0) perArea = 1;

            bool tallArea = area.height >= area.width;
            float span = tallArea ? area.height : area.width;
            float inset = Mathf.Min(halfExtent + 0.3f, span * 0.4f);
            float lo = (tallArea ? area.yMin : area.xMin) + inset;
            float hi = (tallArea ? area.yMax : area.xMax) - inset;

            float t = perArea == 1 ? 0.5f : slot / (float)(perArea - 1);
            float along = Mathf.Lerp(lo, hi, Mathf.Clamp01(t));

            return tallArea
                ? new Vector2(area.center.x, along)
                : new Vector2(along, area.center.y);
        }

        private static Rect ResolveSpawnArea(RacerSetup setup, ArenaRuntime arena, int index)
        {
            // An authored map's own spawn areas always win - that is where the designer put the start.
            if (arena.SpawnAreas != null && arena.SpawnAreas.Count > 0)
            {
                return arena.SpawnAreas[index % arena.SpawnAreas.Count].Footprint;
            }

            List<SpawnRegion> regions = arena.Definition.spawnRegions;
            if (setup.placement == SpawnPlacement.SpawnRegions && regions.Count > 0)
            {
                SpawnRegion region = regions[index % regions.Count];
                return new Rect(
                    region.center.x - region.size.x * 0.5f,
                    region.center.y - region.size.y * 0.5f,
                    region.size.x, region.size.y);
            }

            return arena.PlayableRect;
        }

        private static Color ResolveColor(RacerDefinition definition, RacerSetup setup, VisualTheme theme, int index)
        {
            if (definition.colorOverride.a > 0f) return definition.colorOverride;

            if (setup.colorSource == RacerColorSource.Team && setup.teams.Count > 0)
            {
                return setup.teams[Mathf.Clamp(definition.team, 0, setup.teams.Count - 1)].color;
            }

            if (theme.palette.Count > 0)
            {
                int slot = definition.paletteIndex >= 0 ? definition.paletteIndex : index;
                return theme.palette[slot % theme.palette.Count];
            }
            if (setup.teams.Count > 0) return setup.teams[Mathf.Clamp(definition.team, 0, setup.teams.Count - 1)].color;
            return Color.white;
        }
    }

    /// <summary>Grabs Unity's built-in cube mesh once instead of instantiating a primitive per racer.</summary>
    internal static class PrimitiveMeshCache
    {
        private static Mesh _cube;

        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;

                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cube = temp.GetComponent<MeshFilter>().sharedMesh;

                if (Application.isPlaying) Object.Destroy(temp);
                else Object.DestroyImmediate(temp);

                return _cube;
            }
        }
    }
}
