using System.Text;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The one line that tells a viewer what this round is: the rule, and a live counter that
    /// shows how close it is to ending. Derived from the active config, so every format gets
    /// the right words without the scene author writing any.
    /// </summary>
    public static class RuleText
    {
        /// <summary>The rule line: shown on the round card and above the live counter.</summary>
        public static string Describe(SimulationConfig config)
        {
            if (config == null) return "";
            if (!string.IsNullOrEmpty(config.mode.ruleLabel)) return config.mode.ruleLabel;
            int teams = config.racers.teams != null ? config.racers.teams.Count : 0;

            switch (config.mode.kind)
            {
                case ModeKind.Infection: return "INFECTION  ·  last clean cube survives";
                case ModeKind.HotPotato: return "HOT POTATO  ·  holder explodes at 0";
                case ModeKind.LuckyBlock: return "LUCKY BLOCKS  ·  smash crates for loot";
                case ModeKind.PaintWar: return $"PAINT WAR  ·  most floor in {Mathf.RoundToInt(config.endRules.maxDuration)}s";
            }

            switch (config.endRules.winCondition)
            {
                case WinCondition.ReachGoal:
                    int need = Mathf.Max(1, config.endRules.requiredFinishers);
                    return need > 1 ? $"RACE TO THE GOAL  ·  first {need} home" : "RACE TO THE GOAL  ·  first home wins";

                case WinCondition.LastTeamAlive:
                    return teams > 2 ? $"{teams} TEAMS  ·  last team standing" : "TEAM WAR  ·  last team standing";

                case WinCondition.TeamFinishers:
                    return $"TEAM RACE  ·  first team with {Mathf.Max(1, config.endRules.requiredFinishers)} home";

                case WinCondition.LastClean:
                    return "INFECTION  ·  last clean cube survives";

                case WinCondition.MostTiles:
                    return $"PAINT WAR  ·  most floor in {Mathf.RoundToInt(config.endRules.maxDuration)}s";

                case WinCondition.MostCoins:
                    return $"COIN RUSH  ·  most coins in {Mathf.RoundToInt(config.endRules.maxDuration)}s";

                case WinCondition.LastAlive:
                    return config.racers.maxHealth <= 1.01f
                        ? "SUDDEN DEATH  ·  1 heart, last one standing"
                        : "LAST ONE STANDING";

                default:
                    return "";
            }
        }

        /// <summary>The counter under the rule: alive count, podium, team tally or the clock.</summary>
        public static string Counter(SimulationRunner runner, SimulationConfig config)
        {
            if (runner == null || config == null) return "";
            Racer[] racers = runner.Racers;

            if (runner.Infection != null)
            {
                int clean = runner.Infection.CleanAlive(racers);
                return $"CLEAN  {clean}      INFECTED  {runner.AliveCount - clean}";
            }
            if (runner.Bomb != null)
            {
                Racer holder = runner.Bomb.Holder;
                string bomb = holder != null ? $"BOMB ON  {holder.DisplayName}  {runner.Bomb.FuseRemaining:0.0}s" : "BOMB  incoming";
                return $"{bomb}      ALIVE  {runner.AliveCount}";
            }
            if (runner.Loot != null)
            {
                return $"ALIVE  {runner.AliveCount}      CRATES OPENED  {runner.Loot.Drops}      KNIVES  {runner.Loot.KnivesDropped}";
            }
            if (runner.Paint != null)
            {
                float left = Mathf.Max(0f, config.endRules.maxDuration - runner.ElapsedTime);
                Racer top = null;
                for (int i = 0; i < racers.Length; i++)
                    if (racers[i].IsActive && (top == null || racers[i].Score > top.Score)) top = racers[i];
                string lead = top != null && top.Score > 0 ? $"      LEAD  {top.DisplayName} {top.Score}" : "";
                return $"TIME  {Mathf.CeilToInt(left)}s{lead}";
            }

            switch (config.endRules.winCondition)
            {
                case WinCondition.TeamFinishers:
                {
                    int need = Mathf.Max(1, config.endRules.requiredFinishers);
                    var sb = new StringBuilder();
                    int teams = config.racers.teams.Count;
                    var finishers = runner.Goals != null ? runner.Goals.Finishers : null;
                    for (int t = 0; t < teams; t++)
                    {
                        int home = 0;
                        if (finishers != null) for (int i = 0; i < finishers.Count; i++) if (finishers[i].Team == t) home++;
                        if (t > 0) sb.Append("     ");
                        sb.Append(config.racers.teams[t].name.ToUpperInvariant()).Append("  ").Append(home).Append('/').Append(need);
                    }
                    return sb.ToString();
                }
                case WinCondition.ReachGoal:
                {
                    int need = Mathf.Max(1, config.endRules.requiredFinishers);
                    return $"FINISHED  {runner.FinishedCount} / {need}      ALIVE  {runner.AliveCount}";
                }

                case WinCondition.LastTeamAlive:
                {
                    var sb = new StringBuilder();
                    int teams = config.racers.teams.Count;
                    for (int t = 0; t < teams; t++)
                    {
                        int alive = 0;
                        for (int i = 0; i < racers.Length; i++)
                            if (racers[i].Team == t && racers[i].IsActive) alive++;
                        if (t > 0) sb.Append("     ");
                        sb.Append(config.racers.teams[t].name.ToUpperInvariant()).Append("  ").Append(alive);
                    }
                    return sb.ToString();
                }

                case WinCondition.MostCoins:
                {
                    float left = Mathf.Max(0f, config.endRules.maxDuration - runner.ElapsedTime);
                    Racer top = null;
                    for (int i = 0; i < racers.Length; i++)
                        if (racers[i].IsActive && (top == null || racers[i].Coins > top.Coins)) top = racers[i];
                    string lead = top != null && top.Coins > 0 ? $"      LEAD  {top.DisplayName} ${top.Coins}" : "";
                    return $"TIME  {Mathf.CeilToInt(left)}s{lead}";
                }

                default:
                    return $"ALIVE  {runner.AliveCount} / {runner.RacerCount}";
            }
        }

        /// <summary>Coin Rush turns red in its last five seconds; everything else stays white.</summary>
        public static bool Urgent(SimulationRunner runner, SimulationConfig config)
        {
            if (runner == null || config == null) return false;
            if (config.endRules.winCondition != WinCondition.MostCoins && config.endRules.winCondition != WinCondition.MostTiles) return false;
            return config.endRules.maxDuration - runner.ElapsedTime <= 5f;
        }
    }
}
