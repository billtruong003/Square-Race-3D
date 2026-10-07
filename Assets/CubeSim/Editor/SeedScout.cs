using System.Collections.Generic;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Racers;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Picks a recording seed by watching candidates play out headless and keeping the most
    /// watchable one. Physics is not touched: the sim is deterministic, so the seed that scored
    /// best here is exactly the match the camera will see.
    ///
    /// What scores: a real winner (never the watchdog), a length that fits a Short, lead changes
    /// (more if late), events arriving steadily instead of long lulls, and a close finish.
    /// </summary>
    public static class SeedScout
    {
        public sealed class Result
        {
            public int Seed;
            public float Score;
            public float Seconds;
            public int LeadChanges;
            public int LateLeadChanges;
            public float LongestLull;
            public float Margin;
            public bool TimedOut;

            public string Summary =>
                $"score {Score:F1} | {Seconds:F0}s lead x{LeadChanges} (late {LateLeadChanges}) lull {LongestLull:F0}s margin {Margin:F1}{(TimedOut ? " TIMEOUT" : "")}";
        }

        private sealed class Track
        {
            public int Leader = -1;
            public int Pending = -1;
            public int PendingSteps;
            public readonly List<float> LeadTimes = new List<float>();
            public readonly List<float> Events = new List<float>();
            public int LastAlive = -1;
            public int LastFinished;
        }

        public static Result Pick(string arenaId, ShortsPlanBuilder.Tuning tune, int baseSeed, int candidates)
        {
            List<Result> results = Evaluate(arenaId, tune, baseSeed, candidates);
            if (results == null) return null;
            Result best = null;
            foreach (Result r in results) if (best == null || r.Score > best.Score) best = r;
            return best;
        }

        /// <summary>Plays every candidate seed headless and scores each one.</summary>
        public static List<Result> Evaluate(string arenaId, ShortsPlanBuilder.Tuning tune, int baseSeed, int candidates)
        {
            var spec = new ModeLab.Spec
            {
                ArenaId = arenaId,
                Mode = tune.Mode,
                Win = tune.Win,
                Racers = tune.Racers,
                Teams = tune.Teams,
                Weapons = tune.Weapons,
                MaxHealth = tune.MaxHealth > 0f ? tune.MaxHealth : 3f,
                MaxDuration = Mathf.Min(tune.Duration, 150f),
                RequiredFinishers = tune.Finishers,
                Pressure = tune.Pressure,
                PressureDelay = tune.PressureDelay,
                PressureSpeed = tune.PressureSpeed,
                InsetX = tune.InsetX,
                InsetZ = tune.InsetZ,
            };

            bool race = tune.Win == WinCondition.ReachGoal || tune.Win == WinCondition.TeamFinishers;
            var results = new List<Result>(candidates);
            Track track = null;

            try
            {
                ModeLab.Run(spec, baseSeed, candidates, null,
                    runner =>
                    {
                        if (track == null) track = new Track();
                        Observe(runner, track, race);
                    },
                    (runner, stat) =>
                    {
                        results.Add(Score(runner, stat, track ?? new Track(), race, tune.Win == WinCondition.MostCoins));
                        track = null;
                    });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CubeSim] Seed scout failed on {arenaId}: {e.Message}");
                return null;
            }

            return results;
        }

        private static void Observe(SimulationRunner runner, Track t, bool race)
        {
            float now = runner.ElapsedTime;
            Racer[] racers = runner.Racers;

            if (t.LastAlive >= 0 && runner.AliveCount < t.LastAlive) t.Events.Add(now);
            if (runner.FinishedCount > t.LastFinished) t.Events.Add(now);
            t.LastAlive = runner.AliveCount;
            t.LastFinished = runner.FinishedCount;

            // Leader: races by walking distance left; fights by health then kills.
            int leader = -1;
            float best = float.MaxValue;
            for (int i = 0; i < racers.Length; i++)
            {
                Racer r = racers[i];
                if (!r.IsActive) continue;
                float key = race ? r.GoalDistance : -(r.Health * 100f + r.Kills * 10f);
                if (key < best) { best = key; leader = i; }
            }

            if (leader < 0) return;
            if (leader == t.Leader) { t.Pending = -1; return; }
            if (leader != t.Pending) { t.Pending = leader; t.PendingSteps = 0; return; }

            // A lead only counts once held for half a second, like the HUD banner.
            if (++t.PendingSteps < 30) return;
            if (t.Leader >= 0)
            {
                t.LeadTimes.Add(now);
                t.Events.Add(now);
            }

            t.Leader = leader;
            t.Pending = -1;
        }

        private static Result Score(SimulationRunner runner, ModeLab.RoundStat stat, Track t, bool race, bool clock)
        {
            float seconds = runner.ElapsedTime;
            var r = new Result
            {
                Seed = stat.Seed,
                Seconds = seconds,
                TimedOut = runner.Result.timedOut,
                LeadChanges = t.LeadTimes.Count,
            };

            foreach (float lt in t.LeadTimes) if (lt > seconds * 0.75f) r.LateLeadChanges++;

            float prev = 0f;
            foreach (float e in t.Events) { r.LongestLull = Mathf.Max(r.LongestLull, e - prev); prev = e; }
            r.LongestLull = Mathf.Max(r.LongestLull, seconds - prev);

            // Closeness: races by the gap behind the winner, fights by how little health it kept.
            Racer[] racers = runner.Racers;
            if (race)
            {
                float second = float.MaxValue;
                foreach (Racer x in racers)
                {
                    float d = x.ReachedGoal ? 0f : x.GoalDistance;
                    if (x.ReachedGoal && x.Placement == 1) continue;
                    if (d < second) second = d;
                }
                r.Margin = second < float.MaxValue * 0.5f ? second : 30f;
            }
            else
            {
                float winnerHealth = runner.Result.winnerHealth;
                float max = 1f;
                foreach (Racer x in racers) max = Mathf.Max(max, x.MaxHealth);
                r.Margin = winnerHealth / max * 10f;
            }

            float score = 0f;
            if (r.TimedOut) score -= 100f;
            if (runner.Result.outcome == SimulationOutcome.Draw) score -= 100f;

            // A Short wants 22-45 s of play; long rounds lose a little per second over.
            float ideal = clock ? seconds : Mathf.Clamp(seconds, 22f, 45f);
            score -= Mathf.Abs(seconds - ideal) * 0.6f;

            score += r.LeadChanges * 1.5f + r.LateLeadChanges * 4f;
            score -= Mathf.Max(0f, r.LongestLull - 6f) * 0.8f;
            score -= r.Margin * 0.4f;
            r.Score = score;
            return r;
        }
    }
}
