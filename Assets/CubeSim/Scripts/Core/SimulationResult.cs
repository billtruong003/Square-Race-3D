using System;
using UnityEngine;

namespace CubeSim.Core
{
    public enum SimulationOutcome
    {
        Running = 0,
        Winner = 1,
        Draw = 2,
        TimeLimit = 3,
        GoalReached = 4
    }

    /// <summary>
    /// What an episode produced. Structured so a recording pipeline can read it straight off the
    /// runner without parsing a log line.
    /// </summary>
    [Serializable]
    public class SimulationResult
    {
        public SimulationOutcome outcome = SimulationOutcome.Running;
        public int seed;
        public float elapsedTime;
        public int racerCount;
        public int aliveCount;
        public int finishedCount;

        public string winnerId = "";
        public int winnerIndex = -1;
        public int winnerTeam = -1;
        public string winnerTeamName = "";
        public Color winnerColor = Color.clear;
        public string winnerWeaponId = "";
        public string winnerName = "";
        public int winnerKills;
        public float winnerHealth;
        public float winnerGoalTime = -1f;

        public override string ToString()
        {
            switch (outcome)
            {
                case SimulationOutcome.GoalReached:
                    return $"GOAL {winnerId} (team {winnerTeam} '{winnerTeamName}') " +
                           $"reached the destination at t={winnerGoalTime:F1}s, " +
                           $"{finishedCount} finisher(s), {aliveCount} still racing, seed={seed}";
                case SimulationOutcome.Winner:
                    return $"WINNER {winnerId} (team {winnerTeam} '{winnerTeamName}') " +
                           $"hp={winnerHealth:F0} kills={winnerKills} " +
                           $"weapon={(string.IsNullOrEmpty(winnerWeaponId) ? "none" : winnerWeaponId)} " +
                           $"t={elapsedTime:F1}s seed={seed}";
                case SimulationOutcome.Draw:
                    return $"DRAW - no survivors, t={elapsedTime:F1}s seed={seed}";
                case SimulationOutcome.TimeLimit:
                    return $"TIME LIMIT - {aliveCount} still alive, t={elapsedTime:F1}s seed={seed}";
                default:
                    return $"running t={elapsedTime:F1}s alive={aliveCount}/{racerCount}";
            }
        }
    }
}
