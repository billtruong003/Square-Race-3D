using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Watches for racers entering a goal region.
    ///
    /// Deliberately separate from the mover: movement stays "go straight, reflect", and goals are a
    /// rule layered on top. An episode without goal areas simply has nothing to do here.
    /// </summary>
    public sealed class GoalSystem
    {
        private readonly List<GoalArea> _goals;
        private readonly List<Racer> _finishers = new List<Racer>();

        /// <summary>Fired the step a racer first enters a goal. (racer, goal, placement, time)</summary>
        public event Action<Racer, GoalArea, int, float> OnRacerReachedGoal;

        public IReadOnlyList<Racer> Finishers => _finishers;
        public bool HasGoals => _goals != null && _goals.Count > 0;

        public GoalSystem(List<GoalArea> goals)
        {
            _goals = goals ?? new List<GoalArea>();
        }

        public void Step(Racer[] racers, float elapsedTime)
        {
            if (_goals.Count == 0) return;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.Alive || racer.ReachedGoal) continue;

                for (int g = 0; g < _goals.Count; g++)
                {
                    GoalArea goal = _goals[g];
                    if (goal == null || !goal.HasEntered(racer.Position, racer.HalfExtent)) continue;

                    Register(racer, goal, elapsedTime);
                    break;
                }
            }
        }

        private void Register(Racer racer, GoalArea goal, float elapsedTime)
        {
            racer.ReachedGoal = true;
            racer.GoalTime = elapsedTime;
            racer.Placement = _finishers.Count + 1;

            // Retired racers stop moving and stop being crushable, so a finisher parked in the goal
            // is not killed by pressure that later sweeps over it.
            if (goal.RetireOnReach) racer.Retired = true;

            racer.Visual?.PlayCelebrate();
            _finishers.Add(racer);
            OnRacerReachedGoal?.Invoke(racer, goal, racer.Placement, elapsedTime);
        }
    }
}
