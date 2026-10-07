using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Records what actually happened in an Arena episode while it is being filmed: every hit,
    /// kill, pickup and blast, per round, in simulation time. Written next to the video so a
    /// cut can be judged on its real pacing (first blood, dead air, close finishes) rather than
    /// on a guess. Read-only listener; the simulation never sees it.
    /// </summary>
    public sealed class ArenaTelemetry
    {
        private sealed class Round
        {
            public int Index;
            public string Arena;
            public int Racers;
            public float Duration;
            public string Winner;
            public float WinnerHp;
            public readonly StringBuilder Events = new StringBuilder();
            public int Hits, Kills, Pickups, Blasts, Crits;
        }

        private readonly List<Round> _rounds = new List<Round>();
        private Round _current;
        private SimulationRunner _runner;

        public void BeginRound(int index, string arena, SimulationRunner runner)
        {
            _runner = runner;
            _current = new Round { Index = index, Arena = arena, Racers = runner.Racers.Length };
            _rounds.Add(_current);
            Round round = _current;

            runner.OnRacerDamaged += (victim, attacker, amount) =>
            {
                round.Hits++;
                if (amount >= 50f) round.Crits++;
                Event(round, "hit", victim, attacker, amount, attacker != null && attacker.Weapon != null ? attacker.Weapon.id : "");
            };
            runner.OnRacerKilled += (victim, killer, cause) =>
            {
                round.Kills++;
                string weapon = killer != null && killer.Weapon != null ? killer.Weapon.id : cause.ToString();
                Event(round, "kill", victim, killer, 0f, weapon);
            };
            if (runner.Combat != null)
            {
                runner.Combat.OnEquipped += (racer, weapon) => { round.Pickups++; Event(round, "pickup", racer, null, 0f, weapon.id); };
                if (runner.Combat.Projectiles != null)
                    runner.Combat.Projectiles.OnBlast += (source, centre, radius) => { round.Blasts++; Event(round, "blast", source, null, radius, ""); };
            }
        }

        public void EndRound(SimulationResult result)
        {
            if (_current == null || _runner == null) return;
            _current.Duration = _runner.ElapsedTime;
            _current.Winner = result != null ? result.winnerName : "";
            _current.WinnerHp = result != null ? result.winnerHealth : 0f;
        }

        private void Event(Round round, string kind, Racer who, Racer by, float amount, string weapon)
        {
            if (round.Events.Length > 0) round.Events.Append(",\n");
            float t = _runner != null ? _runner.ElapsedTime : 0f;
            round.Events.Append("      {\"t\":").Append(t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\"e\":\"").Append(kind).Append('"')
                .Append(",\"who\":\"").Append(who != null ? who.DisplayName : "").Append('"')
                .Append(",\"by\":\"").Append(by != null ? by.DisplayName : "").Append('"')
                .Append(",\"w\":\"").Append(weapon).Append('"')
                .Append(",\"v\":").Append(amount.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\"hp\":").Append((who != null ? who.Health : 0f).ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                .Append('}');
        }

        public void Write(string sceneName)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Recordings");
            Directory.CreateDirectory(folder);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\n  \"scene\":\"").Append(sceneName).Append("\",\n  \"rounds\":[\n");
            for (int i = 0; i < _rounds.Count; i++)
            {
                Round r = _rounds[i];
                sb.Append("    {\"round\":").Append(r.Index).Append(",\"arena\":\"").Append(r.Arena)
                  .Append("\",\"racers\":").Append(r.Racers)
                  .Append(",\"duration\":").Append(r.Duration.ToString("F2", inv))
                  .Append(",\"winner\":\"").Append(r.Winner).Append("\",\"winnerHp\":").Append(r.WinnerHp.ToString("F0", inv))
                  .Append(",\"hits\":").Append(r.Hits).Append(",\"kills\":").Append(r.Kills)
                  .Append(",\"crits\":").Append(r.Crits).Append(",\"pickups\":").Append(r.Pickups).Append(",\"blasts\":").Append(r.Blasts)
                  .Append(",\n     \"events\":[\n").Append(r.Events).Append("\n     ]}");
                sb.Append(i < _rounds.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(folder, sceneName + "_telemetry.json"), sb.ToString());
        }
    }
}
