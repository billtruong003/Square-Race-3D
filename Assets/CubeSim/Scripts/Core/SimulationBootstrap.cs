using System.IO;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.CameraRig;
using CubeSim.Combat;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Core
{
    /// <summary>
    /// The single scene entry point. Everything else in the scene is spawned from config at runtime,
    /// so the hierarchy stays shallow whether there are 10 racers or 300.
    ///
    /// Config resolution order: JSON file (if a path is set) > config asset > inline config.
    /// That is the hook for an automated pipeline: write a JSON file, press play, record.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimulationBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private SimulationConfigAsset configAsset;

        [Tooltip("Optional. Absolute path, or relative to the project folder. Overrides the asset.")]
        [SerializeField] private string jsonConfigPath = string.Empty;

        [SerializeField] private SimulationConfig inlineConfig = new SimulationConfig();

        [Header("Assets")]
        [Tooltip("Maps the config's racer visual id to a model prefab and animator controller.")]
        [SerializeField] private RacerVisualLibrary visualLibrary;

        [Tooltip("Maps the config's arena.arenaId to an authored map prefab.")]
        [SerializeField] private AuthoredArenaLibrary arenaLibrary;

        [Tooltip("Maps a weapon id to its model prefab.")]
        [SerializeField] private WeaponVisualLibrary weaponLibrary;

        [Tooltip("Maps a simulation event to an Epic Toon FX prefab. Purely cosmetic.")]
        [SerializeField] private VfxLibrary vfxLibrary;

        [Tooltip("Maps a simulation event to a sound. Purely cosmetic.")]
        [SerializeField] private AudioLibrary audioLibrary;

        [Tooltip("Live standings panel on the left edge of the screen.")]
        [SerializeField] private bool showLeaderboard = true;

        [Header("Seed")]
        [SerializeField] private bool overrideSeed;
        [SerializeField] private int seedOverride = 1;

        [Header("Run")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private Camera targetCamera;

        [Header("Readout")]
        [SerializeField] private bool logSummary = true;

        private SimulationConfig _activeConfig;
        private SimulationRunner _runner;
        private MaterialLibrary _materials;
        private Transform _sceneRoot;
        private Light _light;
        private VfxSystem _vfx;
        private SimAudioSystem _audio;
        private DamagePopupSystem _popups;
        private LeaderboardOverlay _leaderboard;
        private bool _resultLogged;
        private float _resultHoldTimer;

        public SimulationRunner Runner => _runner;
        public SimulationConfig ActiveConfig => _activeConfig;

        /// <summary>
        /// Freezes simulation stepping while a story card is on screen. Purely a gate on the
        /// stepping loop: nothing inside the simulation ever reads it, so a paused-then-resumed
        /// episode is byte-identical to an uninterrupted one.
        /// </summary>
        public bool Paused { get; set; }

        /// <summary>Wiring hooks for scene authoring tools and automated setup.</summary>
        public void SetConfigAsset(SimulationConfigAsset asset) => configAsset = asset;

        public void SetTargetCamera(Camera camera) => targetCamera = camera;

        public void SetJsonConfigPath(string path) => jsonConfigPath = path;

        public void SetBuildOnStart(bool value) => buildOnStart = value;

        /// <summary>A fresh runtime copy of whatever config source this bootstrap resolves to.</summary>
        public SimulationConfig ResolveConfigTemplate() => ResolveConfig();

        public void SetVisualLibrary(RacerVisualLibrary library) => visualLibrary = library;

        public void SetArenaLibrary(AuthoredArenaLibrary library) => arenaLibrary = library;

        public void SetWeaponLibrary(WeaponVisualLibrary library) => weaponLibrary = library;

        public void SetVfxLibrary(VfxLibrary library) => vfxLibrary = library;

        public void SetAudioLibrary(AudioLibrary library) => audioLibrary = library;

        private void Start()
        {
            if (buildOnStart) Build();
        }

        /// <summary>
        /// Effects are retired on the render clock, never on the simulation clock. Nothing here can
        /// reach the simulation, so a dropped frame changes what is on screen and nothing else.
        /// </summary>
        private void Update()
        {
            _vfx?.Tick(Time.deltaTime);
            _audio?.Tick(Time.deltaTime, _runner?.AliveCount ?? 0);
            _popups?.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (_runner == null || Paused) return;

            _runner.Step(Time.fixedDeltaTime);

            if (!_runner.Finished) return;

            if (!_resultLogged)
            {
                _resultLogged = true;
                _resultHoldTimer = Mathf.Max(0f, _activeConfig.endRules.resultHoldTime);
                Debug.Log("[CubeSim] " + _runner.Result);
            }

            if (!_activeConfig.endRules.loopOnEnd) return;

            _resultHoldTimer -= Time.fixedDeltaTime;
            if (_resultHoldTimer > 0f) return;

            int nextSeed = _activeConfig.endRules.advanceSeedOnLoop
                ? _activeConfig.seed + 1
                : _activeConfig.seed;

            Rebuild(nextSeed);
        }

        /// <summary>Builds an episode from the resolved config.</summary>
        [ContextMenu("Build / Restart")]
        public void Build()
        {
            Teardown();

            _activeConfig = ResolveConfig();
            if (overrideSeed) _activeConfig.seed = seedOverride;

            if (_activeConfig.simulation.applyFixedTimeStep && _activeConfig.simulation.fixedTimeStep > 0f)
            {
                Time.fixedDeltaTime = _activeConfig.simulation.fixedTimeStep;
            }

            var random = new SimulationRandom(_activeConfig.seed);

            _sceneRoot = new GameObject("SimulationRoot").transform;
            _sceneRoot.SetParent(transform, false);

            _materials = new MaterialLibrary(_activeConfig.visuals);
            ApplyLighting(_activeConfig.visuals);

            float racerDiameter = Mathf.Max(0.1f, _activeConfig.racers.cubeSize);

            ArenaRuntime arena = ArenaBuilder.Build(_activeConfig.arena, random, _materials,
                _activeConfig.simulation.groundY, racerDiameter, arenaLibrary, _sceneRoot);

            PressureField pressure = BuildPressure(arena);

            Racer[] racers = RacerFactory.Build(_activeConfig.racers, arena, random,
                _activeConfig.visuals, _materials, visualLibrary, _sceneRoot);

            var combat = new CombatSystem(_activeConfig.weapons, arena, pressure, random,
                _materials, _activeConfig.simulation.groundY, _sceneRoot,
                _activeConfig.racers.weaponPickupScale, _activeConfig.racers.equippedWeaponScale,
                weaponLibrary);

            var goals = new GoalSystem(arena.GoalAreas);
            var breakableWalls = new BreakableWallSystem(arena, _materials, _activeConfig.visuals);
            var food = new FoodSystem(arena, _materials, _activeConfig.simulation.groundY, _sceneRoot);
            var obstacles = new MovingObstacleSystem(arena);

            // Colliders created this frame are not in the physics scene until a sync. Without this,
            // the first queries of the episode see every wall still sitting at the origin.
            Physics.SyncTransforms();

            _runner = new SimulationRunner(_activeConfig, arena, pressure, combat, goals,
                breakableWalls, racers, food, obstacles);
            _resultLogged = false;

            _vfx = new VfxSystem(vfxLibrary, _activeConfig.simulation.groundY, _sceneRoot);
            _popups = new DamagePopupSystem(_sceneRoot);
            _audio = new SimAudioSystem(audioLibrary, _sceneRoot);
            _audio.Bind(racers);
            SubscribePresentation(combat, goals, breakableWalls);

            // Eating is small sparkle + the eat sfx + a melody note - grazing plays the song.
            food.OnEaten += (racer, position) =>
            {
                _vfx?.Play(VfxId.WeaponDrop, position, Vector3.forward, racer.Color);
                _audio?.Play(SimSoundId.FoodEaten);
                _audio?.Melody?.PlayNextNote(0.5f);
            };

            if (showLeaderboard)
            {
                if (_leaderboard == null) _leaderboard = LeaderboardOverlay.Create(transform);
                _leaderboard.Bind(racers);
            }

            FrameCamera(arena);

            if (logSummary)
            {
                string arenaLabel = arena.IsAuthored
                    ? $"authored '{_activeConfig.arena.arenaId}'"
                    : $"procedural {_activeConfig.arena.generation.profile}";

                Debug.Log($"[CubeSim] Episode built. seed={_activeConfig.seed} racers={racers.Length} " +
                          $"arena={arenaLabel} walls={arena.WallRects.Count} " +
                          $"corridor={arena.MinCorridorWidth:F2} weapons={combat.Pickups.Count} " +
                          $"goals={(arena.GoalAreas?.Count ?? 0)} breakables={breakableWalls.WallCount} " +
                          $"pressure={pressure.Describe()} " +
                          $"win={_activeConfig.endRules.winCondition}");
            }
        }

        /// <summary>
        /// Hooks the effect player onto signals the simulation already raises. Every handler only
        /// reads simulation state and spawns cosmetics - no handler writes back, so an episode plays
        /// out identically with the library unassigned.
        /// </summary>
        private void SubscribePresentation(CombatSystem combat, GoalSystem goals, BreakableWallSystem walls)
        {
            VfxSystem vfx = _vfx != null && _vfx.Enabled ? _vfx : null;
            SimAudioSystem audio = _audio != null && _audio.Enabled ? _audio : null;
            if (vfx == null && audio == null) return;

            // The sound palette is deliberately tiny: the collision piano carries everything, and
            // the only one-shots left are the hitmarker on a lost heart, the kill sting on the
            // final heart, the eat pop, and the win sparkle. Everything else stays visual-only.
            if (combat != null)
            {
                combat.OnRangedShot += (racer, origin, direction) =>
                    vfx?.Play(VfxId.MuzzleFlash, origin, direction, racer.Color);

                combat.OnMeleeSwing += (racer, direction) =>
                    vfx?.Play(VfxId.MeleeSlash, racer.Position + direction * racer.HalfExtent, direction, racer.Color);

                combat.OnMeleeHit += (victim, attacker, contact) =>
                    vfx?.Play(VfxId.MeleeHit, contact, victim.Position - attacker.Position, attacker.Color);

                combat.OnEquipped += (racer, weapon) =>
                    vfx?.Play(VfxId.WeaponPickup, racer.Position, racer.Direction, racer.Color);

                combat.OnDropped += (racer, weapon, reason) =>
                    vfx?.Play(VfxId.WeaponDrop, racer.Position, racer.Direction, racer.Color);

                if (combat.Projectiles != null)
                {
                    combat.Projectiles.OnHitWall += (point, direction) =>
                        vfx?.Play(VfxId.ProjectileHitWall, point, -direction);

                    combat.Projectiles.OnHitRacer += (victim, shooter, point, direction) =>
                        vfx?.Play(VfxId.ProjectileHitRacer, point, -direction, victim.Color);
                }
            }

            if (_runner != null)
            {
                // The lost-heart pop plus the hitmarker; a hit that kills gets the kill sting
                // instead, so the two never stack on one impact. Hazard zones drip damage every
                // simulation tick in sub-heart amounts - reacting to those would fire sixty
                // hitmarkers a second and turn the deadzone into a buzz - so only discrete
                // whole-heart hits sound and pop.
                _runner.OnRacerDamaged += (victim, attacker, amount) =>
                {
                    if (amount < 0.99f) return;

                    _popups?.Show(victim.Position, new Color(1f, 0.25f, 0.3f));
                    if (victim.Health > 0f) audio?.Play(SimSoundId.RacerHit);
                };

                _runner.OnRacerKilled += (victim, killer, cause) =>
                {
                    vfx?.Play(cause == DeathCause.Crushed ? VfxId.CrushDeath : VfxId.RacerDeath,
                        victim.Position, victim.Direction, victim.Color);
                    audio?.Play(SimSoundId.RacerDeath);
                };
            }

            if (goals != null)
            {
                goals.OnRacerReachedGoal += (racer, goal, placement, time) =>
                {
                    vfx?.Play(VfxId.GoalReached, racer.Position, Vector3.forward, racer.Color);
                    audio?.Play(SimSoundId.GoalReached);
                    audio?.Melody?.PlayGoalChord();
                };
            }

            if (walls != null)
            {
                walls.OnWallBroken += (id, racer) =>
                {
                    if (racer != null) vfx?.Play(VfxId.WallBreak, racer.Position, racer.Direction);
                };

                // A door hit performs the melody too - the swarm bashing a counter down IS the
                // accelerando.
                walls.OnWallHit += (id, racer, remaining) =>
                    audio?.Melody?.PlayNextNote(0.45f);
            }
        }

        /// <summary>
        /// Chooses the pressure implementation. Authored maps that declare a track use it; everything
        /// else falls back to the linear slabs, so existing procedural episodes are untouched.
        /// </summary>
        private PressureField BuildPressure(ArenaRuntime arena)
        {
            if (!_activeConfig.pressure.enabled) return new NullPressureField();

            if (_activeConfig.pressure.mode == PressureMode.AuthoredTrack)
            {
                if (arena.Track != null)
                {
                    return new AuthoredTrackPressure(arena.Track, _activeConfig.pressure,
                        _activeConfig.simulation.groundY, _materials, _sceneRoot);
                }

                Debug.LogWarning("[CubeSim] Pressure mode is AuthoredTrack but the arena declares no " +
                                 "PressureTrack; falling back to linear slabs.");
            }

            // Real arena extents, so an authored map's slabs start at its actual edges.
            return new LinearSlabPressure(_activeConfig.pressure, arena.PlayableRect,
                _activeConfig.simulation.groundY, _materials, _sceneRoot);
        }

        /// <summary>
        /// Builds an episode straight from a config object, bypassing the asset and JSON path. This is
        /// the entry point for batch runs and for an automated pipeline holding configs in memory.
        /// </summary>
        public void RunConfig(SimulationConfig config)
        {
            inlineConfig = config;
            configAsset = null;
            jsonConfigPath = string.Empty;
            overrideSeed = false;
            Build();
        }

        /// <summary>Restarts with an explicit seed. The main hook for generating episode variations.</summary>
        public void Rebuild(int seed)
        {
            overrideSeed = true;
            seedOverride = seed;
            Build();
        }

        [ContextMenu("Restart With Next Seed")]
        public void RestartWithNextSeed()
        {
            int current = _activeConfig?.seed ?? (configAsset != null ? configAsset.Config.seed : inlineConfig.seed);
            Rebuild(current + 1);
        }

        [ContextMenu("Restart With Random Seed")]
        public void RestartWithRandomSeed() => Rebuild(System.Environment.TickCount);

        public void Teardown()
        {
            _runner = null;
            _vfx = null;
            _audio = null;
            _popups = null;
            _leaderboard?.Bind(null);

            if (_sceneRoot != null)
            {
                // Destroy is deferred to end of frame, so the old arena's colliders would still be in
                // the physics scene while the next episode spawns into it. Deactivating first pulls
                // them out immediately.
                _sceneRoot.gameObject.SetActive(false);

                if (Application.isPlaying) Destroy(_sceneRoot.gameObject);
                else DestroyImmediate(_sceneRoot.gameObject);
                _sceneRoot = null;
            }

            _materials?.Dispose();
            _materials = null;

            // Weapon materials are remapped copies of pack materials; drop them with the episode.
            Combat.WeaponVisualFactory.ResetCache();
        }

        private void OnDestroy() => Teardown();

        private SimulationConfig ResolveConfig()
        {
            if (!string.IsNullOrWhiteSpace(jsonConfigPath))
            {
                string path = Path.IsPathRooted(jsonConfigPath)
                    ? jsonConfigPath
                    : Path.Combine(Directory.GetCurrentDirectory(), jsonConfigPath);

                if (File.Exists(path))
                {
                    SimulationConfig parsed = SimulationConfig.FromJson(File.ReadAllText(path));
                    if (parsed != null) return parsed;
                    Debug.LogError($"[CubeSim] Could not parse config JSON at {path}.");
                }
                else
                {
                    Debug.LogWarning($"[CubeSim] Config JSON not found at {path}; falling back to the asset.");
                }
            }

            if (configAsset != null) return configAsset.CreateRuntimeCopy();
            return inlineConfig.Clone();
        }

        private void ApplyLighting(VisualTheme theme)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambientColor;

            if (_light == null)
            {
                var go = new GameObject("SimulationLight");
                go.transform.SetParent(transform, false);
                _light = go.AddComponent<Light>();
                _light.type = LightType.Directional;
                _light.shadows = LightShadows.Soft;
            }

            _light.color = theme.lightColor;
            _light.intensity = theme.lightIntensity;
            _light.transform.rotation = Quaternion.Euler(theme.lightEuler);
        }

        private void FrameCamera(ArenaRuntime arena)
        {
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[CubeSim] No camera assigned and no MainCamera in the scene.");
                return;
            }

            SimulationCamera.Frame(camera, _activeConfig.camera, arena.PlayableRect,
                _activeConfig.simulation.groundY, _activeConfig.visuals.post);
        }

        /// <summary>Writes the active config to disk - handy when tuning by hand before automating.</summary>
        public void ExportActiveConfig(string path)
        {
            SimulationConfig config = _activeConfig ?? ResolveConfig();
            File.WriteAllText(path, config.ToJson());
            Debug.Log($"[CubeSim] Config exported to {path}");
        }
    }
}
