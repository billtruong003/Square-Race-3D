using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CubeSim.Arena;
using CubeSim.Combat;
using CubeSim.Core;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Authors the prototype episode from code. Keeping scene assembly in a rerunnable script - not
    /// in hand-placed scene objects - is what lets a future agent regenerate everything.
    /// </summary>
    public static class CubeSimSceneBuilder
    {
        public const string RootFolder = "Assets/CubeSim";
        public const string ScenePath = RootFolder + "/Scenes/CubeSimulation.unity";
        public const string ConfigPath = RootFolder + "/Data/DefaultSimulationConfig.asset";

        public const string CourseScenePath = RootFolder + "/Scenes/CubeSimulation_Serpentine.unity";
        public const string CourseConfigPath = RootFolder + "/Data/SerpentineSimulationConfig.asset";

        public const string ArenaScenePath = RootFolder + "/Scenes/CubeSimulation_Arena5v5.unity";
        public const string ArenaConfigPath = RootFolder + "/Data/Arena5v5SimulationConfig.asset";
        public const string EpisodeScenePath = RootFolder + "/Scenes/CubeSimulation_Episode.unity";

        [MenuItem("CubeSim/Build Prototype Scene", priority = 0)]
        public static void BuildPrototypeScene()
            => BuildScene(ScenePath, CreateOrUpdateConfigAsset(ConfigPath, BuildPrototypeConfig()), true);

        [MenuItem("CubeSim/Build Serpentine Course Scene", priority = 1)]
        public static void BuildCourseScene()
        {
            SerpentineMapBuilder.Build();
            BuildScene(CourseScenePath,
                CreateOrUpdateConfigAsset(CourseConfigPath, BuildSerpentineConfig()), false);
        }

        [MenuItem("CubeSim/Build 5v5 Arena Scene", priority = 2)]
        public static void BuildArenaScene()
        {
            // The serpentine map is rebuilt first so both stay registered in the shared library.
            SerpentineMapBuilder.Build();
            GameObject arena = Arena5v5MapBuilder.Build();
            Arena5v5MapBuilder.RegisterInLibrary(arena);
            GameObject blocks = BlockBreakMapBuilder.Build();
            BlockBreakMapBuilder.RegisterInLibrary(blocks);

            BuildScene(ArenaScenePath,
                CreateOrUpdateConfigAsset(ArenaConfigPath, BuildArena5v5Config()), false);
        }

        /// <summary>
        /// The full upload shape: several rounds across the authored maps, one winner per round,
        /// stitched with the intro / round / winner / podium cards by the EpisodeDirector.
        /// </summary>
        [MenuItem("CubeSim/Build Episode Scene (Multi-Round)", priority = 3)]
        public static void BuildEpisodeScene()
        {
            SerpentineMapBuilder.Build();
            GameObject arena = Arena5v5MapBuilder.Build();
            Arena5v5MapBuilder.RegisterInLibrary(arena);
            GameObject blocks = BlockBreakMapBuilder.Build();
            BlockBreakMapBuilder.RegisterInLibrary(blocks);

            BuildScene(EpisodeScenePath,
                CreateOrUpdateConfigAsset(ArenaConfigPath, BuildArena5v5Config()), false);

            // Reopen the freshly saved scene and attach the director with the default three-round
            // card: blocks, the 5v5 comb, then a blocks final on a fresh seed.
            Scene scene = EditorSceneManager.OpenScene(EpisodeScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[CubeSim] Episode scene has no SimulationBootstrap to direct.");
                return;
            }

            var director = bootstrap.gameObject.GetComponent<EpisodeDirector>();
            if (director == null) director = bootstrap.gameObject.AddComponent<EpisodeDirector>();

            director.SetRounds(new List<EpisodeDirector.RoundSpec>
            {
                // The chamber's outer face sits 8.5m from centre; stopping the squeeze at inset 22
                // leaves a 3.5m band, so the field ends up pinned against the doors, hammering the
                // counters down - instead of dying outside a sealed room.
                new EpisodeDirector.RoundSpec
                {
                    arenaId = BlockBreakMapBuilder.ArenaId, seed = 90101,
                    winCondition = WinCondition.ReachGoal, maxDuration = 150f,
                    pressureTargetInset = 22f, pressureStartDelay = 10f, pressureSpeed = 0.25f
                },
                new EpisodeDirector.RoundSpec
                {
                    arenaId = Arena5v5MapBuilder.ArenaId, seed = 90202,
                    winCondition = WinCondition.LastAlive, maxDuration = 165f
                },
                new EpisodeDirector.RoundSpec
                {
                    arenaId = BlockBreakMapBuilder.ArenaId, seed = 90303,
                    winCondition = WinCondition.ReachGoal, maxDuration = 150f,
                    pressureTargetInset = 22f, pressureStartDelay = 10f, pressureSpeed = 0.25f
                },
            });

            EditorUtility.SetDirty(director);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CubeSim] Episode scene built at {EpisodeScenePath} (3 rounds).");
        }

        /// <summary>
        /// The 5v5 arena episode: authored comb map, slabs closing from left and right, last one
        /// standing. Same runtime as the course - only the data differs.
        /// </summary>
        public static SimulationConfig BuildArena5v5Config()
        {
            SimulationConfig config = BuildSerpentineConfig();

            config.seed = 20260831;
            config.arena.arenaId = Arena5v5MapBuilder.ArenaId;

            config.racers.count = 10;

            // Five a side, lined up in the outer channels - the "5 vs 5" of the reference.
            config.racers.placement = SpawnPlacement.SpawnSlots;
            config.racers.startDirectionMode = StartDirectionMode.Random;

            // Proportions measured off the video: its cubes are ~2.05m against a 68m course and
            // nearly fill the narrow lanes (~2.8m). The old 1.4 racer read as a dot lost in the map.
            config.racers.cubeSize = 2.0f;
            config.racers.racerVisualScale = 1.0f;
            config.racers.trail.baseWidth = 1.25f;
            config.racers.trail.length = 7f;

            // Eye-cube racers: one authored cube model, identity carried by the tint colour and
            // the leaderboard colour names, direction told by the eyes.
            config.racers.visual = "EyeCube";
            config.racers.tintModels = true;
            config.racers.speed = 10f;

            // Pet Survival health: three hearts, one per hit, gone on the third. The whole combat
            // catalog deals exactly 1 so the hearts read literally.
            config.racers.maxHealth = 3f;

            config.weapons.count = 2;

            config.pressure.mode = PressureMode.LinearSlabs;
            config.pressure.overhang = 1.5f;
            config.pressure.height = 2.2f;
            config.pressure.slabs = new List<PressureSlabConfig>
            {
                // Timed off the reference video, not guessed. Tracking the slab edge there gives an
                // inset of 1.0 units at t=2s rising to 26.9 at t=130s - it starts moving immediately
                // and creeps at ~0.20 units/s, ending with the arena closed down to the corridor.
                // A previous pass here set a 24 second delay, which left the opening third of an
                // episode with nothing happening at all.
                // The start channel is 4.25m and a racer is 2.0m, so the channel turns lethal once
                // the slab has eaten ~2.25m. A 6 second delay puts that moment near t=15 - racers
                // that stream out on time all make it, stragglers become the first story beat.
                new PressureSlabConfig
                {
                    side = PressureSide.Left, startInset = 0.5f, targetInset = 32f,
                    startDelay = 6f, speed = 0.2f
                },
                new PressureSlabConfig
                {
                    side = PressureSide.Right, startInset = 0.5f, targetInset = 32f,
                    startDelay = 6f, speed = 0.2f
                }
            };

            config.camera.tiltDegrees = 3f;

            // The arena bounds are now the course exactly, so the margin has to supply the breathing
            // room the old ring of dead floor used to. It also puts a band of the surrounding rock on
            // screen, which is what the padded fill masses are for.
            config.camera.margin = 1.18f;

            config.endRules.winCondition = WinCondition.LastAlive;

            // The reference runs 143 seconds and ends with the squeeze essentially complete.
            config.endRules.maxDuration = 165f;

            return config;
        }

        private static void BuildScene(string scenePath, SimulationConfigAsset config, bool setAsStartup)
        {
            if (config == null)
            {
                Debug.LogError("[CubeSim] Config asset could not be resolved; aborting so the scene " +
                               "does not silently fall back to inline values.");
                return;
            }

            RacerVisualLibrary visuals = SkeletonAssetBuilder.BuildAll();
            AuthoredArenaLibrary arenas =
                AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            WeaponVisualLibrary weaponModels = WeaponAssetBuilder.BuildLibrary();
            VfxLibrary effects = VfxAssetBuilder.BuildLibrary();
            AudioLibrary sounds = AudioAssetBuilder.BuildLibrary();
            VolumeProfile outlineProfile = OutlineVolumeSetup.CreateProfile(config.Config.visuals.post);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("SimulationCamera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.Config.camera.backgroundColor;
            cameraGo.AddComponent<AudioListener>();

            // The screen-space outline is a renderer feature gated by a volume override, so the scene
            // needs a global volume for skeletons, weapons and trails to be outlined at all.
            OutlineVolumeSetup.CreateSceneVolume(outlineProfile);

            var bootstrapGo = new GameObject("CubeSimulation");
            SimulationBootstrap bootstrap = bootstrapGo.AddComponent<SimulationBootstrap>();
            bootstrap.SetConfigAsset(config);
            bootstrap.SetTargetCamera(camera);
            bootstrap.SetVisualLibrary(visuals);
            bootstrap.SetArenaLibrary(arenas);
            bootstrap.SetWeaponLibrary(weaponModels);
            bootstrap.SetVfxLibrary(effects);
            bootstrap.SetAudioLibrary(sounds);
            bootstrapGo.AddComponent<SimulationValidator>();
            EditorUtility.SetDirty(bootstrap);

            // Frame the camera now so the editor scene view shows the arena before entering play mode.
            CameraRig.SimulationCamera.Frame(camera, config.Config.camera,
                config.Config.arena.PlayableRect, config.Config.simulation.groundY);

            Directory.CreateDirectory(RootFolder + "/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            if (setAsStartup) SetBuildSettings();

            Debug.Log($"[CubeSim] Scene built at {scenePath}");
        }

        [MenuItem("CubeSim/Create Default Config Asset", priority = 20)]
        public static void CreateDefaultConfigAsset() => CreateOrUpdateConfigAsset(ConfigPath, BuildPrototypeConfig());

        public static SimulationConfigAsset CreateOrUpdateConfigAsset(string path, SimulationConfig config)
        {
            Directory.CreateDirectory(RootFolder + "/Data");

            var asset = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<SimulationConfigAsset>();

            asset.LoadFromJson(config.ToJson(false));

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-load through the database: the freshly created instance is not yet a resolvable
            // asset reference, so serializing it into a scene would write fileID 0.
            return AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(path);
        }

        /// <summary>The V1 last-survivor episode. Every value here is data an agent can override.</summary>
        public static SimulationConfig BuildPrototypeConfig()
        {
            var config = new SimulationConfig { seed = 20260831 };

            config.simulation.fixedTimeStep = 1f / 60f;
            config.simulation.skinWidth = 0.02f;
            config.simulation.maxCollisionIterations = 6;

            config.arena.size = new Vector2(48f, 28f);
            config.arena.wallHeight = 2.4f;
            config.arena.wallThickness = 1.2f;
            config.arena.generateBorder = true;
            config.arena.layout = ArenaLayoutMode.Generated;
            config.arena.generation = new ArenaGenerationSettings
            {
                profile = ArenaGenerationProfile.Mixed,
                corridorWidthMultiplier = 2.6f,
                corridorSafetyMargin = 0.4f,
                maxPlacementAttempts = 120,
                centralClearing = new CentralClearing
                {
                    enabled = true,
                    halfExtents = new Vector2(6.5f, 5f),
                    margin = 1.5f
                }
            };
            config.arena.generation.ApplyProfile();

            config.racers.count = 10;
            config.racers.cubeSize = 1.4f;
            config.racers.speed = 8f;
            config.racers.maxHealth = 100f;   // prototype scene keeps the old scale
            config.racers.visual = "Fox";
            config.racers.visualHeightRatio = 1.7f;
            config.racers.racerVisualScale = 1.3f;
            config.racers.weaponPickupScale = 2.4f;
            config.racers.equippedWeaponScale = 2.0f;
            config.racers.startDirectionMode = StartDirectionMode.Random;
            config.racers.minAxisAngle = 22f;
            config.racers.placement = SpawnPlacement.OpenPlayfield;
            config.racers.spawnClearance = 0.35f;
            // Racers collide with each other and bounce off the contact normal, at constant speed.
            config.racers.racerCollision = RacerCollisionMode.Bounce;
            config.racers.racerCollisionEnabled = true;
            config.racers.racerCollisionSkin = 0.02f;
            config.racers.racerCollisionIterations = 3;
            config.racers.trail = new TrailSettings
            {
                enabled = true, length = 5.2f, baseWidth = 0.85f,
                minPointDistance = 0.28f, heightOffset = 0.06f, lifetime = 0f,
                disappearMode = TrailDisappearMode.RetractAndFade,
                deathRetractDuration = 0.5f, deathFadeDuration = 0.4f,
                rootCapEnabled = true, rootCapRadius = 1.2f, rootCapHeightOffset = 0.075f,
                rootCapUsesOutline = true
            };
            config.racers.colorSource = RacerColorSource.Palette;
            config.racers.teamAssignment = TeamAssignment.Blocks;
            config.racers.teams = new List<TeamDefinition>
            {
                new TeamDefinition("Team A", new Color(0.95f, 0.24f, 0.22f)),
                new TeamDefinition("Team B", new Color(0.24f, 0.52f, 0.98f))
            };

            config.weapons.enabled = true;
            config.weapons.count = 3;
            config.weapons.pickupRadius = 0.6f;
            config.weapons.releaseMode = WeaponReleaseMode.TimeBased;
            config.weapons.holdDuration = 12f;
            config.weapons.ammo = 6;
            config.weapons.dropRearmDelay = 0.5f;
            config.weapons.repickupCooldown = 1.5f;
            config.weapons.catalog = WeaponAssetBuilder.BuildCatalog();

            config.pressure.enabled = true;
            config.pressure.mode = PressureMode.LinearSlabs;
            config.pressure.overhang = 1.5f;
            config.pressure.height = 1.8f;
            config.pressure.slabs = new List<PressureSlabConfig>
            {
                new PressureSlabConfig
                {
                    side = PressureSide.Left, startInset = 1.3f, targetInset = 22.4f,
                    startDelay = 12f, speed = 0.42f
                },
                new PressureSlabConfig
                {
                    side = PressureSide.Right, startInset = 1.3f, targetInset = 22.4f,
                    startDelay = 12f, speed = 0.42f
                },
                // The second stage closes the remaining channel top and bottom, so the survivors are
                // forced into contact instead of orbiting each other until the time limit.
                new PressureSlabConfig
                {
                    side = PressureSide.Back, startInset = 1.3f, targetInset = 12.4f,
                    startDelay = 70f, speed = 0.45f
                },
                new PressureSlabConfig
                {
                    side = PressureSide.Front, startInset = 1.3f, targetInset = 12.4f,
                    startDelay = 70f, speed = 0.45f
                }
            };

            config.camera.orthographic = false;
            config.camera.fieldOfView = 42f;
            config.camera.tiltDegrees = 6f;
            config.camera.margin = 1.1f;

            config.endRules.winCondition = WinCondition.LastAlive;
            config.endRules.maxDuration = 240f;
            config.endRules.loopOnEnd = false;
            config.endRules.resultHoldTime = 3f;

            return config;
        }

        /// <summary>
        /// The authored course episode: hand-built map, route-following pressure, race to the goal.
        /// Nothing about it needs map-specific code - only different data.
        /// </summary>
        public static SimulationConfig BuildSerpentineConfig()
        {
            var config = new SimulationConfig { seed = 4242 };

            config.simulation.fixedTimeStep = 1f / 60f;
            config.simulation.skinWidth = 0.02f;
            config.simulation.maxCollisionIterations = 6;

            config.arena.mode = ArenaMode.Authored;
            config.arena.arenaId = SerpentineMapBuilder.ArenaId;

            config.racers.count = 10;
            config.racers.cubeSize = 1.4f;
            config.racers.speed = 9f;
            config.racers.maxHealth = 100f;
            config.racers.visual = "Fox";
            config.racers.visualHeightRatio = 1.7f;
            config.racers.racerVisualScale = 1.3f;
            config.racers.weaponPickupScale = 2.4f;
            config.racers.equippedWeaponScale = 2.0f;
            config.racers.startDirectionMode = StartDirectionMode.Random;
            config.racers.minAxisAngle = 25f;
            config.racers.placement = SpawnPlacement.SpawnRegions;
            config.racers.spawnClearance = 0.35f;
            // Racers collide with each other and bounce off the contact normal, at constant speed.
            config.racers.racerCollision = RacerCollisionMode.Bounce;
            config.racers.racerCollisionEnabled = true;
            config.racers.racerCollisionSkin = 0.02f;
            config.racers.racerCollisionIterations = 3;
            config.racers.colorSource = RacerColorSource.Palette;
            config.racers.trail = new TrailSettings
            {
                enabled = true, length = 5.6f, baseWidth = 0.9f,
                minPointDistance = 0.28f, heightOffset = 0.06f, lifetime = 0f,
                disappearMode = TrailDisappearMode.RetractAndFade,
                deathRetractDuration = 0.5f, deathFadeDuration = 0.4f,
                rootCapEnabled = true, rootCapRadius = 1.2f, rootCapHeightOffset = 0.075f,
                rootCapUsesOutline = true
            };
            config.racers.teams = new List<TeamDefinition>
            {
                new TeamDefinition("Runners", new Color(0.95f, 0.24f, 0.22f))
            };

            config.weapons.enabled = true;
            config.weapons.count = 1;
            config.weapons.pickupRadius = 0.6f;
            config.weapons.releaseMode = WeaponReleaseMode.TimeBased;
            config.weapons.holdDuration = 6f;
            config.weapons.ammo = 4;
            config.weapons.dropRearmDelay = 0.5f;
            config.weapons.repickupCooldown = 2f;
            config.weapons.catalog = WeaponAssetBuilder.BuildCatalog();

            config.pressure.enabled = true;
            config.pressure.mode = PressureMode.AuthoredTrack;
            config.pressure.height = 2.2f;

            config.camera.orthographic = false;
            config.camera.fieldOfView = 44f;
            config.camera.tiltDegrees = 4f;
            config.camera.margin = 1.02f;

            config.endRules.winCondition = WinCondition.ReachGoal;
            config.endRules.requiredFinishers = 1;
            config.endRules.maxDuration = 180f;
            config.endRules.loopOnEnd = false;
            config.endRules.resultHoldTime = 3f;

            return config;
        }

        /// <summary>
        /// Puts the new scene at the front of the build list and parks the old ChallengeArena entry
        /// as disabled. The old scene file and every asset it uses stay untouched.
        /// </summary>
        [MenuItem("CubeSim/Set Build Settings To Cube Simulation", priority = 21)]
        public static void SetBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[CubeSim] Build settings updated. Older scenes kept but disabled.");
        }

        /// <summary>
        /// Writes both episode shapes to JSON. These are the reference documents for an automated
        /// pipeline: one procedural survival episode, one authored course episode.
        /// </summary>
        [MenuItem("CubeSim/Export Config JSON", priority = 40)]
        public static void ExportConfigJson()
        {
            Directory.CreateDirectory(RootFolder + "/Data");

            string proceduralPath = RootFolder + "/Data/DefaultSimulationConfig.json";
            string coursePath = RootFolder + "/Data/SerpentineSimulationConfig.json";

            File.WriteAllText(proceduralPath, BuildPrototypeConfig().ToJson());
            File.WriteAllText(coursePath, BuildSerpentineConfig().ToJson());

            AssetDatabase.Refresh();
            Debug.Log($"[CubeSim] Config JSON written to {proceduralPath} and {coursePath}");
        }
    }
}
