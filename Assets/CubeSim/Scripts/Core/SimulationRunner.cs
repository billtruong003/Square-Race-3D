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
        private readonly BreakableWallSystem _breakableWalls;
        private readonly Racer[] _racers;
        private readonly RacerContactGrid _contactGrid;
        private PlanarMover _mover;
        private ConstraintSolver _solver;

        private readonly System.Action<Racer, Racer, float, DeathCause> _applyDamage;

        public float ElapsedTime { get; private set; }
        public bool Finished { get; private set; }
        public Racer[] Racers => _racers;
        public ArenaRuntime Arena => _arena;
        public PressureField Pressure => _pressure;
        public CombatSystem Combat => _combat;
        public GoalSystem Goals => _goals;
        public FoodSystem Food => _food;
        public BreakableWallSystem BreakableWalls => _breakableWalls;
        public int RacerCount => _racers.Length;
        public int AliveCount { get; private set; }
        public int FinishedCount => _goals?.Finishers.Count ?? 0;
        public SimulationResult Result { get; } = new SimulationResult();

        /// <summary>(victim, killer or null, cause) - one signal for every elimination.</summary>
        public event System.Action<Racer, Racer, DeathCause> OnRacerKilled;

        /// <summary>(victim, attacker or null, amount) - every hit that cost health, lethal or not.</summary>
        public event System.Action<Racer, Racer, float> OnRacerDamaged;

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
            MovingObstacleSystem obstacles = null)
        {
            _food = food;
            _obstacles = obstacles;
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

            _solver = new ConstraintSolver(_mover, pressure, config.simulation.skinWidth,
                config.simulation.groundY);

            if (config.racers.racerCollisionEnabled &&
                config.racers.racerCollision == RacerCollisionMode.Bounce && racers.Length > 1)
            {
                // A cell has to be wide enough that any pair which could have crossed during one step
                // still lands in adjacent cells - otherwise the broadphase would hide exactly the
                // fast approaches the swept test exists to catch.
                float step = config.racers.speed * Mathf.Max(config.simulation.fixedTimeStep, 1f / 60f);
                float cell = Mathf.Max(config.racers.cubeSize * 2f, config.racers.cubeSize + step) + 0.1f;

                _contactGrid = new RacerContactGrid(arena.PlayableRect, cell, racers.Length,
                    config.racers.racerCollisionSkin, config.racers.racerCollisionIterations);
            }

            AliveCount = racers.Length;
            _applyDamage = ApplyDamage;

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

            // 2. Move every racer still in the race. Retired finishers hold their position.
            for (int i = 0; i < _racers.Length; i++)
            {
                Racer racer = _racers[i];
                if (!racer.IsActive) continue;

                // Recorded before the move, so the contact pass can sweep the segment just travelled.
                racer.PreviousPosition = racer.Position;
                _mover.Step(racer, deltaTime, _breakableWalls);
            }

            _breakableWalls?.Step(deltaTime);

            // 3. Racer-vs-racer contact. It runs before the constraint pass on purpose: separating a
            //    pair can push somebody toward a wall, and step 4 is what guarantees nobody ends the
            //    step inside one.
            _contactGrid?.ResolveContacts(_racers, _racers.Length, _solver);

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

                if (_solver.Resolve(racer) == ConstraintOutcome.Crushed)
                {
                    Kill(racer, null, DeathCause.Crushed);
                    CrushDeaths++;

                    // A fully closing arena makes every remaining racer illegal on the same step.
                    // Stopping here leaves a survivor instead of collapsing the run into a draw.
                    EvaluateEndConditions();
                    if (Finished) break;
                    continue;
                }

                racer.PushToTransform();
                racer.Visual?.SetMoving(true);
                racer.Visual?.FaceDirection(racer.Direction, deltaTime);
                racer.Visual?.SampleTrail(racer.Position, deltaTime);
            }

            // 5. Authored hazards, then weapons: both sit on top of movement.
            StepHazards(deltaTime);
            _combat?.Step(deltaTime, _racers, _applyDamage);

            // 6. Feeding and goal detection are their own passes, deliberately outside the mover.
            _food?.Step(_racers);
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
                        HazardDeaths++;
                    }
                    else
                    {
                        ApplyDamage(racer, null, hazard.DamagePerSecond * deltaTime, DeathCause.Hazard);
                    }

                    break;
                }
            }
        }

        private void ApplyDamage(Racer victim, Racer attacker, float amount, DeathCause cause)
        {
            if (victim == null || !victim.IsActive || amount <= 0f) return;

            victim.Health -= amount;
            OnRacerDamaged?.Invoke(victim, attacker, amount);
            if (victim.Health > 0f) return;

            Kill(victim, attacker, cause);
        }

        private void Kill(Racer racer, Racer killer, DeathCause cause)
        {
            if (!racer.Alive) return;

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
                    if (AliveCount == 0) { Result.outcome = SimulationOutcome.Draw; Finished = true; return; }
                    break;

                case WinCondition.ReachGoal:
                    int required = Mathf.Max(1, _config.endRules.requiredFinishers);
                    if (FinishedCount >= required)
                    {
                        RecordWinner(_goals.Finishers[0]);
                        Result.outcome = SimulationOutcome.GoalReached;
                        Finished = true;
                        return;
                    }

                    // Nobody left who could still finish.
                    if (AliveCount == 0)
                    {
                        Result.outcome = FinishedCount > 0 ? SimulationOutcome.GoalReached : SimulationOutcome.Draw;
                        if (FinishedCount > 0) RecordWinner(_goals.Finishers[0]);
                        Finished = true;
                        return;
                    }

                    break;
            }

            float maxDuration = _config.endRules.maxDuration;
            if (maxDuration > 0f && ElapsedTime >= maxDuration)
            {
                if (AliveCount == 1) { RecordWinner(FindLastAlive()); }
                else if (FinishedCount > 0) { RecordWinner(_goals.Finishers[0]); Result.outcome = SimulationOutcome.GoalReached; }
                else { Result.outcome = SimulationOutcome.TimeLimit; }

                Finished = true;
            }
        }

        private Racer FindLastAlive()
        {
            for (int i = 0; i < _racers.Length; i++)
            {
                if (_racers[i].Alive) return _racers[i];
            }

            return null;
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
            Result.winnerTeamName = racer.Team >= 0 && racer.Team < teams.Count ? teams[racer.Team].name : "";
        }
    }
}
