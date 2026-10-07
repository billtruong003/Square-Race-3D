using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Combat;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Owns one running episode: the racer array, the pressure field, combat, goals and the clock.
    ///
    /// The whole simulation is a single flat loop. No per-racer MonoBehaviour, no GetComponent, no
    /// LINQ, no allocations - the only per-step work is one BoxCast chain per racer plus the
    /// constraint pass.
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly SimulationConfig _config;
        private readonly ArenaRuntime _arena;
        private readonly PressureField _pressure;
        private readonly CombatSystem _combat;
        private readonly GoalSystem _goals;
        private readonly FoodSystem _food;
        private readonly MovingObstacleSystem _obstacles;
        private readonly ArenaDeviceSystem _devices;
        private readonly BreakableWallSystem _breakableWalls;
        private readonly Racer[] _racers;
        private readonly RacerContactGrid _contactGrid;
        private PlanarMover _mover;
        private ConstraintSolver _solver;

        private readonly System.Action<Racer, Racer, float, DeathCause> _applyDamage;
        public System.Action<Racer, Racer, float, DeathCause> DamageDelegate => _applyDamage;
        private readonly System.Action<Racer, DeathCause> _killByDevice;

        public float ElapsedTime { get; private set; }
        public bool Finished { get; private set; }
        public Racer[] Racers => _racers;
        public ArenaRuntime Arena => _arena;
        public PressureField Pressure => _pressure;
        public CombatSystem Combat => _combat;
        public GoalSystem Goals => _goals;
        public FoodSystem Food => _food;
        public BreakableWallSystem BreakableWalls => _breakableWalls;
        public ArenaDeviceSystem Devices => _devices;

        /// <summary>Walking distance to the goal; null on maps without one. Drives race standings.</summary>
        public PathField GoalField
        {
            get => _goalField;
            set
            {
                _goalField = value;
                // Only a race leans its bounces toward the goal; a fight has nowhere to lean.
                bool race = _config.endRules.winCondition == WinCondition.ReachGoal ||
                            _config.endRules.winCondition == WinCondition.TeamFinishers;
                _mover.GoalField = race ? value : null;
            }
        }

        private PathField _goalField;
        public int RacerCount => _racers.Length;
        public int AliveCount { get; private set; }
        public int FinishedCount => _goals?.Finishers.Count ?? 0;
        public SimulationResult Result { get; } = new SimulationResult();

        public Modes.InfectionSystem Infection { get; private set; }
        public Modes.BombSystem Bomb { get; private set; }
        public Modes.PaintSystem Paint { get; private set; }
        public Modes.LootSystem Loot { get; private set; }

        /// <summary>Wires the round's mode systems in. Call once, before the first step.</summary>
        public void AttachModes(Modes.InfectionSystem infection, Modes.BombSystem bomb, Modes.PaintSystem paint, Modes.LootSystem loot = null)
        {
            Infection = infection;
            Bomb = bomb;
            Paint = paint;
            Loot = loot;
            if (_contactGrid == null) return;
            if (infection != null) _contactGrid.OnContact += infection.OnContact;
            if (bomb != null) _contactGrid.OnContact += bomb.OnContact;
        }

        /// <summary>(victim, killer or null, cause) - one signal for every elimination.</summary>
        public event System.Action<Racer, Racer, DeathCause> OnRacerKilled;

        /// <summary>(victim, attacker or null, amount) - every hit that cost health, lethal or not.</summary>
        public event System.Action<Racer, Racer, float> OnRacerDamaged;

        /// <summary>(victim, attacker or null) - a hit a Lucky Block shield swallowed.</summary>
        public event System.Action<Racer, Racer> OnShieldBlocked;

        /// <summary>Counts crush eliminations, so validation can tell them apart from combat deaths.</summary>
        public int CrushDeaths { get; private set; }
        public int HazardDeaths { get; private set; }

        /// <summary>Racer-vs-racer contacts resolved this episode. 0 when the mode is PassThrough.</summary>
        public int RacerContactCount => _contactGrid?.ContactCount ?? 0;

        /// <summary>Contacts that only the swept test caught - pairs that would have passed through.</summary>
        public int SweptContactCount => _contactGrid?.SweptContactCount ?? 0;

        /// <summary>Deepest racer-vs-racer overlap seen before separation, in metres.</summary>
        public float MaxRacerPenetration => _contactGrid?.MaxPenetration ?? 0f;

        /// <summary>True when this episode is resolving racer-vs-racer contact at all.</summary>
        public bool RacerCollisionEnabled => _contactGrid != null;

        public SimulationRunner(SimulationConfig config, ArenaRuntime arena,
            PressureField pressure, CombatSystem combat, GoalSystem goals,
            BreakableWallSystem breakableWalls, Racer[] racers, FoodSystem food = null,
            MovingObstacleSystem obstacles = null, ArenaDeviceSystem devices = null)
        {
            _food = food;
            _obstacles = obstacles;
            _devices = devices != null && devices.Any ? devices : null;
            const float MaxSpeedFactor = 3f;
            _breakableWalls = breakableWalls;
            _config = config;
            _arena = arena;
            _pressure = pressure;
            _combat = combat;
            _goals = goals;
            _racers = racers;

            _mover = new PlanarMover(
                SimulationLayers.BlockingMask,
                SimulationLayers.WallMask,
                config.simulation.skinWidth,
                config.simulation.maxCollisionIterations,
                config.simulation.groundY);

            _mover.RoughnessDegrees = config.racers.wallRoughness;
            _mover.GoalLeanDegrees = config.racers.goalLean;
            _mover.Seed = config.seed;

            _solver = new ConstraintSolver(_mover, pressure, config.simulation.skinWidth,
                config.simulation.groundY);
            _devices?.UseMover(_mover);

            if (config.racers.racerCollisionEnabled &&
                config.racers.racerCollision == RacerCollisionMode.Bounce && racers.Length > 1)
            {
                // A cell has to be wide enough that any pair which could have crossed during one step
                // still lands in adjacent cells - otherwise the broadphase would hide exactly the
                // fast approaches the swept test exists to catch.
                // Knockback and bumper boosts run well above cruise speed, so size for the fastest
                // a cube can actually go, not the configured cruise.
                float step = config.racers.speed * MaxSpeedFactor * Mathf.Max(config.simulation.fixedTimeStep, 1f / 60f);
                float cell = Mathf.Max(config.racers.cubeSize * 2f, config.racers.cubeSize + step) + 0.1f;

                _contactGrid = new RacerContactGrid(arena.PlayableRect, cell, racers.Length,
                    config.racers.racerCollisionSkin, config.racers.racerCollisionIterations);
            }

            AliveCount = racers.Length;
            _applyDamage = ApplyDamage;
            _killByDevice = (racer, cause) =>
            {
                if (!racer.Alive) return;
                // A piston is a trap, not the squeeze: a device kill counts as a hazard death.
                _deviceKill = true;
                Kill(racer, null, cause);
                _deviceKill = false;
            };

            Result.seed = config.seed;
            Result.racerCount = racers.Length;
            Result.aliveCount = AliveCount;

            // Place the field at t=0 before anything moves.
            _pressure.Tick(0f);
        }

        /// <summary>One deterministic simulation step. Call from FixedUpdate.</summary>
        public void Step(float deltaTime)
        {
            if (Finished || deltaTime <= 0f) return;

            ElapsedTime += deltaTime;

            // 1. Advance the pressure and the moving obstacles first, so the casts below see
            //    current geometry.
            _pressure.Tick(ElapsedTime);
            _obstacles?.Step(ElapsedTime);
            _devices?.PreMove(ElapsedTime, deltaTime, _racers, _arena, _killByDevice);

            // Auto-sync is off: without this, the casts below see every slab, door and crusher where
            // it stood last step, the cube overlaps it, and the solver has to clamp it every frame.
            Physics.SyncTransforms();

            // 2. Move every racer still in the race. Retired finishers hold their position.
            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.IsActive) continue;

                // Recorded before the move, so the contact pass can sweep the segment just travelled.
                racer.PreviousPosition = racer.Position;
                racer.StepStartHealth = racer.Health;
                _mover.Step(racer, deltaTime, _breakableWalls);
            }

            _breakableWalls?.Step(deltaTime);
            _breakableWalls?.RefreshEligibility(_racers);

            // 3. Racer-vs-racer contact. It runs before the constraint pass on purpose: separating a
            //    pair can push somebody toward a wall, and step 4 is what guarantees nobody ends the
            //    step inside one.
            _contactGrid?.ResolveContacts(_racers, _racers.Length, _solver);

            // 3b. A lethal zone swallows whoever stands on floor it just closed. Collected with the
            //     crushes so a zone that takes everyone at once still leaves a winner.
            if (_pressure.Enabled)
            {
                for (int i = 0; i < _racers.Length; i++)
                {
                    Racer racer = _racers[i];
                    if (racer.IsActive && _pressure.Engulfs(racer.Position)) _crushed.Add(racer);
                }
            }

            // 4. Enforce the hard constraints. A racer that cannot be placed legally is crushed
            //    rather than shoved through a wall.
            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.Alive) continue;

                if (racer.Retired)
                {
                    // A finisher is parked; keep its transform in sync and leave it alone.
                    racer.PushToTransform();
                    racer.Visual?.SetMoving(false);
                    racer.Visual?.SampleTrail(racer.Position, deltaTime);
                    continue;
                }

                if (_crushed.Contains(racer)) continue;

                if (_solver.Resolve(racer) == ConstraintOutcome.Crushed)
                {
                    _crushed.Add(racer);
                    continue;
                }

                if (GoalField != null) racer.GoalDistance = GoalField.Sample(racer.Position);

                racer.PushToTransform();
                racer.Visual?.SetMoving(true);
                racer.Visual?.FaceDirection(racer.Direction, deltaTime);
                racer.Visual?.SampleTrail(racer.Position, deltaTime);
            }

            // 4a. Crushes are applied together, after every racer was tested. Killing them in array
            //     order used to hand a fully closing arena to whichever cube had the highest index.
            if (_crushed.Count > 0)
            {
                if (_crushed.Count >= AliveCount - FinishedCount && IsSurvivalRule())
                {
                    // The squeeze would wipe the field: the best-placed of them is spared to win.
                    Racer spared = PickBest(_crushed);
                    _crushed.Remove(spared);
                    spared.PushToTransform();
                }

                for (int i = 0; i < _crushed.Count; i++) Kill(_crushed[i], null, DeathCause.Crushed);
                _crushed.Clear();
            }

            // 4b. Mode rules that ride on position and contact: infection seed, bomb fuse, paint.
            Infection?.Step(ElapsedTime, _racers);
            Bomb?.Step(ElapsedTime, deltaTime, _racers, _applyDamage, _killByDevice);
            Paint?.Step(_racers);
            Loot?.Step(ElapsedTime);

            // 5. Authored hazards, then weapons: both sit on top of movement.
            StepHazards(deltaTime);
            StepRotorBlades();
            _devices?.PostMove(ElapsedTime, deltaTime, _racers, _arena, _applyDamage, _killByDevice);
            _combat?.Step(deltaTime, _racers, _applyDamage);

            // 6. Feeding and goal detection are their own passes, deliberately outside the mover.
            _food?.Step(_racers);
            // A goal pad only means something in a race; in a fight it would park (and shield)
            // whoever wandered onto it.
            if (_config.endRules.winCondition == WinCondition.ReachGoal || _config.endRules.winCondition == WinCondition.TeamFinishers)
                _goals?.Step(_racers, ElapsedTime);

            EvaluateEndConditions();
        }

        private void StepHazards(float deltaTime)
        {
            if (_arena.Hazards == null || _arena.Hazards.Count == 0) return;

            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.IsActive) continue;

                for (int h = 0; h < _arena.Hazards.Count; h++)
                {
                    HazardArea hazard = _arena.Hazards[h];
                    if (hazard == null || !hazard.Contains(racer.Position)) continue;

                    if (hazard.IsLethal)
                    {
                        Kill(racer, null, DeathCause.Hazard);
                    }
                    else
                    {
                        ApplyDamage(racer, null, hazard.DamagePerSecond * deltaTime, DeathCause.Hazard);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// A rotor is a saw, not a turnstile: a bar that sweeps into a racer costs a heart, with a
        /// short per-racer grace so one sweep is one cut rather than a shredder.
        /// </summary>
        private void StepRotorBlades()
        {
            if (_obstacles == null || _obstacles.RotorCount == 0) return;

            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.IsActive) continue;

                RotorObstacle rotor = _obstacles.FindCuttingRotor(racer.Position, racer.Radius);
                if (rotor == null) continue;

                if (_lastRotorCut.TryGetValue(racer.Index, out float last) && ElapsedTime - last < rotor.HitCooldown)
                {
                    continue;
                }

                _lastRotorCut[racer.Index] = ElapsedTime;
                ApplyDamage(racer, null, rotor.DamagePerHit, DeathCause.Hazard);
            }
        }

        private readonly System.Collections.Generic.Dictionary<int, float> _lastRotorCut =
            new System.Collections.Generic.Dictionary<int, float>();

        private void ApplyDamage(Racer victim, Racer attacker, float amount, DeathCause cause)
        {
            if (victim == null || !victim.IsActive || amount <= 0f) return;

            if (victim.Shield > 0 && amount >= 0.99f)
            {
                victim.Shield--;
                if (victim.Shield == 0) victim.Badge = "";
                OnShieldBlocked?.Invoke(victim, attacker);
                return;
            }

            victim.Health -= amount;
            victim.Visual?.PlayHurt();
            OnRacerDamaged?.Invoke(victim, attacker, amount);
            if (victim.Health > 0f) return;

            Kill(victim, attacker, cause);
        }

        private bool _deviceKill;
        private readonly System.Collections.Generic.List<Racer> _crushed = new System.Collections.Generic.List<Racer>();
        private readonly System.Collections.Generic.List<Racer> _scratch = new System.Collections.Generic.List<Racer>();

        private void Kill(Racer racer, Racer killer, DeathCause cause)
        {
            if (!racer.Alive) return;

            // One place counts every death, whatever path dealt the blow (saw cuts and spike
            // damage used to slip past the tallies, so the lab under-reported traps).
            if (cause == DeathCause.Crushed && !_deviceKill) CrushDeaths++;
            else if (cause == DeathCause.Hazard || cause == DeathCause.Crushed) HazardDeaths++;

            racer.Alive = false;
            racer.Health = 0f;
            racer.Cause = cause;
            racer.DeathTime = ElapsedTime;
            AliveCount--;

            if (killer != null && killer != racer) killer.Kills++;

            // Owner death is one release reason among several, not the circulation mechanism.
            _combat?.ForceDrop(racer, DropReason.OwnerDeath);
            racer.Visual?.PlayDeath();

            OnRacerKilled?.Invoke(racer, killer, cause);
        }

        private void EvaluateEndConditions()
        {
            Result.elapsedTime = ElapsedTime;
            Result.aliveCount = AliveCount;
            Result.finishedCount = FinishedCount;

            switch (_config.endRules.winCondition)
            {
                case WinCondition.LastAlive:
                    if (AliveCount == 1) { RecordWinner(FindLastAlive()); Finished = true; return; }
                    if (AliveCount == 0) { RecordWinner(PickBest(LastToFall())); Finished = true; return; }
                    break;

                case WinCondition.LastTeamAlive:
                    if (AliveCount == 0) { RecordWinner(PickBest(LastToFall())); Finished = true; return; }
                    if (CountAliveTeams() <= 1)
                    {
                        RecordWinner(FindTeamChampion(-1));
                        Finished = true;
                        return;
                    }
                    break;

                case WinCondition.MostCoins:
                    // The clock decides; an empty field is the only early exit.
                    if (AliveCount == 0) { RecordWinner(FindTopCoins()); Finished = true; return; }
                    break;

                case WinCondition.MostTiles:
                    if (AliveCount == 0) { RecordWinner(FindTopScore()); Finished = true; return; }
                    break;

                case WinCondition.LastClean:
                    if (AliveCount == 1) { RecordWinner(FindLastAlive()); Finished = true; return; }
                    if (AliveCount == 0) { RecordWinner(PickBest(LastToFall())); Finished = true; return; }
                    if (Infection != null && Infection.InfectedCount > 0)
                    {
                        int clean = Infection.CleanAlive(_racers);
                        if (clean == 1) { RecordWinner(Infection.FirstCleanAlive(_racers)); Finished = true; return; }
                        // Everybody turned: the one that held out longest takes it.
                        if (clean == 0) { RecordWinner(Infection.LastInfected); Finished = true; return; }
                    }
                    break;

                case WinCondition.TeamFinishers:
                {
                    int need = Mathf.Max(1, _config.endRules.requiredFinishers);
                    Racer teamWinner = FindTeamWithFinishers(need);
                    if (teamWinner != null)
                    {
                        RecordWinner(teamWinner);
                        Result.outcome = SimulationOutcome.GoalReached;
                        Finished = true;
                        return;
                    }
                    if (AliveCount - FinishedCount <= 0)
                    {
                        FinishOnCourse();
                        return;
                    }
                    break;
                }

                case WinCondition.ReachGoal:
                    int required = Mathf.Max(1, _config.endRules.requiredFinishers);
                    if (_config.endRules.eliminateCount > 0)
                    {
                        // Knockout: the round is over the moment the cubes still on the course
                        // are exactly the ones going out (the dead already filled part of the quota).
                        int dead = RacerCount - AliveCount;
                        int racing = AliveCount - FinishedCount;
                        int quota = Mathf.Max(0, _config.endRules.eliminateCount - dead);
                        if (racing <= quota)
                        {
                            FinishOnCourse();
                            return;
                        }
                        break;
                    }
                    if (FinishedCount >= required)
                    {
                        RecordWinner(_goals.Finishers[0]);
                        Result.outcome = SimulationOutcome.GoalReached;
                        Finished = true;
                        return;
                    }

                    // Nobody left who could still finish, or too few left to ever fill the podium:
                    // finishers are parked and still count as alive, so subtract them.
                    int stillRacing = AliveCount - FinishedCount;
                    if (stillRacing <= 0 || FinishedCount + stillRacing < required)
                    {
                        FinishOnCourse();
                        return;
                    }

                    break;
            }

            float maxDuration = _config.endRules.maxDuration;
            if (maxDuration > 0f && ElapsedTime >= maxDuration)
            {
                if (_config.endRules.winCondition == WinCondition.LastTeamAlive && AliveCount > 0)
                {
                    // Clock ran out mid-war: the strongest team (survivors, then health) takes it.
                    RecordWinner(FindTeamChampion(LeadingTeam()));
                }
                else if (_config.endRules.winCondition == WinCondition.MostCoins)
                {
                    RecordWinner(FindTopCoins());
                }
                else if (_config.endRules.winCondition == WinCondition.MostTiles)
                {
                    RecordWinner(FindTopScore());
                }
                else if (FinishedCount > 0) { RecordWinner(_goals.Finishers[0]); Result.outcome = SimulationOutcome.GoalReached; }
                else
                {
                    // The watchdog is a safety net, never a result: whoever is best placed wins.
                    RecordWinner(PickBest(AliveCount > 0 ? Living() : LastToFall()));
                    Result.timedOut = true;
                }

                Finished = true;
            }
        }

        /// <summary>
        /// Coin Rush winner: most coins among the living, ties by kills then index. A wiped field
        /// falls back to the richest of the dead, so the round still has a name on the card.
        /// </summary>
        private Racer FindTopCoins()
        {
            Racer best = null;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                for (int i = 0; i < _racers.Length; i++)
                {
                    Racer racer = _racers[i];
                    if (pass == 0 && !racer.Alive) continue;

                    if (best == null || racer.Coins > best.Coins ||
                        (racer.Coins == best.Coins && racer.Kills > best.Kills))
                    {
                        best = racer;
                    }
                }
            }

            return best;
        }

        /// <summary>Highest Score among the living, ties by coins then kills; the dead as a fallback.</summary>
        private Racer FindTopScore()
        {
            Racer best = null;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                for (int i = 0; i < _racers.Length; i++)
                {
                    Racer racer = _racers[i];
                    if (pass == 0 && !racer.Alive) continue;
                    if (best == null || racer.Score > best.Score ||
                        (racer.Score == best.Score && racer.Coins > best.Coins) ||
                        (racer.Score == best.Score && racer.Coins == best.Coins && racer.Kills > best.Kills))
                    {
                        best = racer;
                    }
                }
            }
            return best;
        }

        /// <summary>The first finisher of the first team that has 'need' cubes home, or null.</summary>
        private Racer FindTeamWithFinishers(int need)
        {
            if (_goals == null) return null;
            var finishers = _goals.Finishers;
            for (int i = 0; i < finishers.Count; i++)
            {
                int team = finishers[i].Team;
                int count = 0;
                for (int j = 0; j <= i; j++) if (finishers[j].Team == team) count++;
                if (count >= need)
                {
                    for (int j = 0; j <= i; j++) if (finishers[j].Team == team) return finishers[j];
                }
            }
            return null;
        }

        private Racer FindLastAlive()
        {
            for (int i = 0; i < _racers.Length; i++)
            {
                if (_racers[i].Alive) return _racers[i];
            }

            return null;
        }

        private int CountAliveTeams()
        {
            int mask = 0, teams = 0;
            for (int i = 0; i < _racers.Length; i++)
            {
                if (!_racers[i].Alive) continue;

                int bit = 1 << Mathf.Clamp(_racers[i].Team, 0, 30);
                if ((mask & bit) != 0) continue;
                mask |= bit;
                teams++;
            }

            return teams;
        }

        /// <summary>
        /// The face of the winning team: its top killer, ties broken by health then by index -
        /// deterministic, so the same seed always crowns the same cube.
        /// </summary>
        private Racer FindTeamChampion(int team)
        {
            Racer best = null;
            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.Alive) continue;
                if (team >= 0 && racer.Team != team) continue;

                if (best == null ||
                    racer.Kills > best.Kills ||
                    (racer.Kills == best.Kills && racer.Health > best.Health))
                {
                    best = racer;
                }
            }

            return best;
        }

        /// <summary>Team with the most survivors, then the most total health, then the most kills.</summary>
        private int LeadingTeam()
        {
            int bestTeam = -1, bestAlive = -1, bestKills = -1;
            float bestHealth = -1f;
            for (int t = 0; t < 31; t++)
            {
                int alive = 0, kills = 0;
                float health = 0f;
                bool any = false;
                for (int i = 0; i < _racers.Length; i++)
                {
                    Racer r = _racers[i];
                    if (r.Team != t) continue;
                    any = true;
                    kills += r.Kills;
                    if (!r.Alive) continue;
                    alive++;
                    health += r.Health;
                }

                if (!any) continue;
                bool better = alive > bestAlive ||
                    (alive == bestAlive && health > bestHealth + 0.001f) ||
                    (alive == bestAlive && Mathf.Abs(health - bestHealth) <= 0.001f && kills > bestKills);
                if (!better) continue;
                bestTeam = t; bestAlive = alive; bestHealth = health; bestKills = kills;
            }

            return bestTeam;
        }

        private bool IsSurvivalRule()
        {
            switch (_config.endRules.winCondition)
            {
                case WinCondition.LastAlive:
                case WinCondition.LastTeamAlive:
                case WinCondition.LastClean:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A race that cannot go on: the first finisher if any, else the cube closest to the goal.</summary>
        private void FinishOnCourse()
        {
            if (FinishedCount > 0)
            {
                RecordWinner(_goals.Finishers[0]);
                Result.outcome = SimulationOutcome.GoalReached;
            }
            else
            {
                RecordWinner(PickBest(AliveCount > 0 ? Living() : LastToFall()));
            }

            Finished = true;
        }

        private System.Collections.Generic.List<Racer> Living()
        {
            _scratch.Clear();
            for (int i = 0; i < _racers.Length; i++) if (_racers[i].Alive) _scratch.Add(_racers[i]);
            return _scratch;
        }

        /// <summary>The racers that died at the latest death time - the ones that fell together last.</summary>
        private System.Collections.Generic.List<Racer> LastToFall()
        {
            _scratch.Clear();
            float latest = float.MinValue;
            for (int i = 0; i < _racers.Length; i++) latest = Mathf.Max(latest, _racers[i].DeathTime);
            for (int i = 0; i < _racers.Length; i++)
            {
                if (Mathf.Abs(_racers[i].DeathTime - latest) < 1e-4f) _scratch.Add(_racers[i]);
            }

            return _scratch;
        }

        /// <summary>
        /// The one tiebreak every rule uses, so no round ends without a name on the card: alive beats
        /// dead, then the later death, then closer to the goal, then healthier (at the start of the
        /// step for the dead), then more kills, then a seeded slot order - never a bare index.
        /// </summary>
        private Racer PickBest(System.Collections.Generic.List<Racer> candidates)
        {
            Racer best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (best == null || Beats(candidates[i], best)) best = candidates[i];
            }

            return best;
        }

        private bool Beats(Racer a, Racer b)
        {
            if (a.Alive != b.Alive) return a.Alive;
            if (!a.Alive && Mathf.Abs(a.DeathTime - b.DeathTime) >= 1e-4f) return a.DeathTime > b.DeathTime;
            if (Mathf.Abs(a.GoalDistance - b.GoalDistance) > 0.01f) return a.GoalDistance < b.GoalDistance;
            float ha = a.Alive ? a.Health : a.StepStartHealth;
            float hb = b.Alive ? b.Health : b.StepStartHealth;
            if (Mathf.Abs(ha - hb) > 0.001f) return ha > hb;
            if (a.Kills != b.Kills) return a.Kills > b.Kills;
            int n = Mathf.Max(1, _racers.Length);
            int salt = (int)((uint)_config.seed % (uint)n);
            return (a.Index + salt) % n < (b.Index + salt) % n;
        }

        private void RecordWinner(Racer racer)
        {
            if (racer == null) return;

            Result.outcome = SimulationOutcome.Winner;
            Result.winnerId = racer.Id;
            Result.winnerIndex = racer.Index;
            Result.winnerTeam = racer.Team;
            Result.winnerColor = racer.Color;
            Result.winnerKills = racer.Kills;
            Result.winnerHealth = racer.Health;
            Result.winnerWeaponId = racer.Armed ? racer.Weapon.id : "";
            Result.winnerName = racer.DisplayName;
            Result.winnerGoalTime = racer.GoalTime;

            var teams = _config.racers.teams;
            // A single-team list is a solo field: the card must not say "team".
            Result.winnerTeamName = teams.Count > 1 && racer.Team >= 0 && racer.Team < teams.Count ? teams[racer.Team].name : "";
        }
    }
}
