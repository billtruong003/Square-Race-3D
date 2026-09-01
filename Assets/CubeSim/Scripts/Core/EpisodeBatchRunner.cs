using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Core
{
    /// <summary>
    /// Runs a queue of episodes back to back and collects one line of outcome per episode.
    ///
    /// This is the validation harness for the simulation, and it is also the shape an automated
    /// pipeline needs: hand it a list of configs, let it play them, read the results.
    /// </summary>
    [RequireComponent(typeof(SimulationBootstrap))]
    public class EpisodeBatchRunner : MonoBehaviour
    {
        public struct EpisodeReport
        {
            public string Label;
            public SimulationOutcome Outcome;
            public int Seed;
            public float Duration;
            public int RacerCount;
            public int AliveCount;
            public string WinnerId;
            public string WinnerWeapon;
            public int WinnerKills;
            public int WallCount;
            public int CrushDeaths;
            public int MeleeDeaths;
            public int RangedDeaths;
            public int Pickups;
            public int WallEmbedViolations;
            public int PressureViolations;
            public int Equips;
            public int Drops;
            public int TimeoutDrops;
            public int AmmoDrops;
            public int DeathDrops;
            public int Finishers;
            public int HazardDeaths;
            public int DistinctHolders;
        }

        private SimulationBootstrap _bootstrap;
        private SimulationValidator _validator;
        private readonly Queue<(string label, SimulationConfig config)> _queue = new Queue<(string, SimulationConfig)>();
        private readonly List<EpisodeReport> _reports = new List<EpisodeReport>();

        private bool _running;
        private string _currentLabel;
        private int _embedBaseline;
        private int _pressureBaseline;

        public bool Running => _running || _queue.Count > 0;
        public IReadOnlyList<EpisodeReport> Reports => _reports;

        private void Awake()
        {
            _bootstrap = GetComponent<SimulationBootstrap>();
            _validator = GetComponent<SimulationValidator>();
        }

        public void Enqueue(string label, SimulationConfig config) => _queue.Enqueue((label, config));

        public void Begin(float timeScale = 20f)
        {
            _reports.Clear();
            Time.timeScale = timeScale;
            StartNext();
        }

        private void StartNext()
        {
            if (_queue.Count == 0)
            {
                _running = false;
                Time.timeScale = 1f;
                Debug.Log(BuildReport());
                return;
            }

            (string label, SimulationConfig config) next = _queue.Dequeue();
            _currentLabel = next.label;
            _embedBaseline = _validator != null ? _validator.WallEmbedViolations : 0;
            _pressureBaseline = _validator != null ? _validator.PressureViolations : 0;

            _bootstrap.RunConfig(next.config);
            _running = true;
        }

        private void Update()
        {
            if (!_running) return;

            SimulationRunner runner = _bootstrap.Runner;
            if (runner == null || !runner.Finished) return;

            _reports.Add(Capture(runner));
            StartNext();
        }

        /// <summary>How many different racers held a weapon - the proof that ownership circulates.</summary>
        private static int CountDistinctHolders(SimulationRunner runner)
        {
            int held = 0;
            for (int i = 0; i < runner.Racers.Length; i++)
            {
                if (runner.Racers[i].TimesArmed > 0) held++;
            }

            return held;
        }

        private EpisodeReport Capture(SimulationRunner runner)
        {
            int melee = 0, ranged = 0;
            for (int i = 0; i < runner.Racers.Length; i++)
            {
                Racer racer = runner.Racers[i];
                if (racer.Cause == DeathCause.Melee) melee++;
                else if (racer.Cause == DeathCause.Ranged) ranged++;
            }

            return new EpisodeReport
            {
                Label = _currentLabel,
                Outcome = runner.Result.outcome,
                Seed = runner.Result.seed,
                Duration = runner.ElapsedTime,
                RacerCount = runner.RacerCount,
                AliveCount = runner.AliveCount,
                WinnerId = runner.Result.winnerId,
                WinnerWeapon = runner.Result.winnerWeaponId,
                WinnerKills = runner.Result.winnerKills,
                WallCount = runner.Arena.WallRects.Count,
                CrushDeaths = runner.CrushDeaths,
                MeleeDeaths = melee,
                RangedDeaths = ranged,
                Pickups = runner.Combat != null ? runner.Combat.Pickups.Count : 0,
                Equips = runner.Combat?.PickupCount ?? 0,
                Drops = runner.Combat?.DropCount ?? 0,
                TimeoutDrops = runner.Combat?.TimeoutDrops ?? 0,
                AmmoDrops = runner.Combat?.AmmoDrops ?? 0,
                DeathDrops = runner.Combat?.DeathDrops ?? 0,
                Finishers = runner.FinishedCount,
                HazardDeaths = runner.HazardDeaths,
                DistinctHolders = CountDistinctHolders(runner),
                WallEmbedViolations = (_validator != null ? _validator.WallEmbedViolations : 0) - _embedBaseline,
                PressureViolations = (_validator != null ? _validator.PressureViolations : 0) - _pressureBaseline
            };
        }

        public string BuildReport()
        {
            var sb = new StringBuilder("[CubeSim] Episode batch results\n");
            int embeds = 0, pressure = 0, winners = 0;

            for (int i = 0; i < _reports.Count; i++)
            {
                EpisodeReport r = _reports[i];
                embeds += r.WallEmbedViolations;
                pressure += r.PressureViolations;
                if (r.Outcome == SimulationOutcome.Winner) winners++;

                sb.AppendLine(
                    $"  {r.Label,-22} seed={r.Seed,-6} {r.Outcome,-9} t={r.Duration,6:F1}s " +
                    $"racers={r.RacerCount,3} walls={r.WallCount,3} " +
                    $"kills[melee={r.MeleeDeaths} ranged={r.RangedDeaths} crush={r.CrushDeaths} hazard={r.HazardDeaths}] " +
                    $"weapon[equips={r.Equips} drops={r.Drops} timeout={r.TimeoutDrops} ammo={r.AmmoDrops} " +
                    $"death={r.DeathDrops} holders={r.DistinctHolders}] finishers={r.Finishers} " +
                    $"winner={(string.IsNullOrEmpty(r.WinnerId) ? "-" : r.WinnerId)} " +
                    $"violations[embed={r.WallEmbedViolations} pressure={r.PressureViolations}]");
            }

            sb.AppendLine($"  TOTAL episodes={_reports.Count} winners={winners} " +
                          $"wallEmbedViolations={embeds} pressureViolations={pressure}");
            return sb.ToString();
        }
    }
}
