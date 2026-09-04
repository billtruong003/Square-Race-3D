using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Racers;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// The feasibility gate for a mode: run it headless over many seeds and report how the rounds
    /// actually end. No rendering, no recorder - the runner is stepped in a tight loop, so twenty
    /// sixty-second rounds take seconds. Needs the episode scene (a SimulationBootstrap) open.
    /// </summary>
    public static class ModeLab
    {
        public sealed class Spec
        {
            public string ArenaId = "SD02";
            public ModeKind Mode = ModeKind.None;
            public WinCondition Win = WinCondition.LastAlive;
            public int Racers = 12;
            public int Teams = 0;
            public int Weapons = 0;
            public float MaxHealth = 2f;
            public float MaxDuration = 300f;
            public int RequiredFinishers = 1;
            public EpisodeDirector.PressureProfile Pressure = EpisodeDirector.PressureProfile.Collapse;
            public float PressureDelay = 15f;
            public float PressureSpeed = 0.5f;
            public float InsetX = 29f;
            public float InsetZ = 15f;
            public System.Action<SimulationConfig> Tweak;
        }

        public sealed class RoundStat
        {
            public int Seed;
            public float Seconds;
            public SimulationOutcome Outcome;
            public string Winner;
            public int Alive;
            public int Deaths;
            public int Hazard;
            public int Crush;
            public string Extra;
        }

        public static List<RoundStat> Run(Spec spec, int firstSeed, int seeds, System.Func<SimulationRunner, string> extra = null)
        {
            var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
            if (bootstrap == null) throw new System.InvalidOperationException("Open the episode scene first (needs a SimulationBootstrap).");

            var stats = new List<RoundStat>(seeds);
            for (int s = 0; s < seeds; s++)
            {
                int seed = firstSeed + s;
                SimulationConfig config = bootstrap.ResolveConfigTemplate();
                config.seed = seed;
                config.arena.mode = CubeSim.Arena.ArenaMode.Authored;
                config.arena.arenaId = spec.ArenaId;
                config.endRules.winCondition = spec.Win;
                config.endRules.loopOnEnd = false;
                config.endRules.maxDuration = spec.MaxDuration;
                config.endRules.requiredFinishers = spec.RequiredFinishers;
                config.endRules.eliminateCount = 0;
                config.racers.count = spec.Racers;
                config.racers.maxHealth = spec.MaxHealth;
                config.weapons.count = spec.Weapons;
                // Fresh defaults every run: the template is a JSON clone of the previous run, so
                // without this a tweak or an old class default would stick forever.
                config.mode = new ModeConfig { kind = spec.Mode };
                // The template is the last config that ran, so every field a previous lab run
                // touched has to be put back explicitly.
                config.racers.teams = new List<TeamDefinition>();
                config.racers.colorSource = RacerColorSource.Palette;
                config.racers.paletteIndices = new List<int>();
                config.weapons.friendlyFire = true;

                if (spec.Teams > 0)
                {
                    config.racers.teams = new List<TeamDefinition>();
                    string[] names = { "RED", "BLUE", "GREEN", "YELLOW" };
                    Color[] colors = { new Color(1f, 0.2f, 0.2f), new Color(0.25f, 0.45f, 1f), new Color(0.15f, 0.85f, 0.3f), new Color(1f, 0.9f, 0.2f) };
                    for (int t = 0; t < Mathf.Min(spec.Teams, 4); t++) config.racers.teams.Add(new TeamDefinition(names[t], colors[t]));
                    config.racers.teamAssignment = TeamAssignment.RoundRobin;
                    config.racers.colorSource = RacerColorSource.Team;
                    config.weapons.friendlyFire = false;
                }

                var round = new EpisodeDirector.RoundSpec
                {
                    pressureProfile = spec.Pressure,
                    pressureStartDelay = spec.PressureDelay,
                    pressureSpeed = spec.PressureSpeed,
                    pressureTargetInset = spec.InsetX,
                    pressureTargetInsetZ = spec.InsetZ,
                };
                EpisodeDirector.ApplyPressureProfile(config, round);
                spec.Tweak?.Invoke(config);

                bootstrap.RunConfig(config);
                bootstrap.Paused = true;
                SimulationRunner runner = bootstrap.Runner;

                float dt = 1f / 60f;
                int maxSteps = Mathf.CeilToInt(spec.MaxDuration / dt) + 10;
                for (int i = 0; i < maxSteps && !runner.Finished; i++) runner.Step(dt);

                int deaths = 0;
                for (int i = 0; i < runner.Racers.Length; i++) if (!runner.Racers[i].Alive) deaths++;

                stats.Add(new RoundStat
                {
                    Seed = seed,
                    Seconds = runner.ElapsedTime,
                    Outcome = runner.Result.outcome,
                    Winner = runner.Result.winnerName,
                    Alive = runner.AliveCount,
                    Deaths = deaths,
                    Hazard = runner.HazardDeaths,
                    Crush = runner.CrushDeaths,
                    Extra = extra != null ? extra(runner) : "",
                });

                bootstrap.Teardown();
            }

            return stats;
        }

        public static string Summarize(string title, List<RoundStat> stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"== {title}: {stats.Count} seeds");
            var seconds = new List<float>();
            var winners = new Dictionary<string, int>();
            int timeouts = 0, draws = 0;
            foreach (RoundStat st in stats)
            {
                seconds.Add(st.Seconds);
                if (st.Outcome == SimulationOutcome.TimeLimit) timeouts++;
                if (st.Outcome == SimulationOutcome.Draw) draws++;
                string w = string.IsNullOrEmpty(st.Winner) ? "-" : st.Winner;
                winners[w] = winners.TryGetValue(w, out int n) ? n + 1 : 1;
            }
            seconds.Sort();
            float median = seconds.Count > 0 ? seconds[seconds.Count / 2] : 0f;
            sb.AppendLine($"seconds: min {seconds[0]:F1}  median {median:F1}  max {seconds[seconds.Count - 1]:F1}   timeouts {timeouts}  draws {draws}");
            sb.Append("winners: ");
            foreach (var kv in winners) sb.Append(kv.Key).Append('×').Append(kv.Value).Append("  ");
            sb.AppendLine();
            foreach (RoundStat st in stats)
                sb.AppendLine($"  seed {st.Seed}: {st.Seconds,6:F1}s {st.Outcome,-12} win={st.Winner,-8} alive={st.Alive,2} deaths={st.Deaths,2} hazard={st.Hazard,2} crush={st.Crush,2} {st.Extra}");
            return sb.ToString();
        }

        public static void WriteReport(string name, string text)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "ModeLab");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name + ".txt"), text);
        }
    }
}
