using System;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Core.Modes
{
    /// <summary>
    /// Infection: at <see cref="ModeConfig.infectionStart"/> one cube (seeded) turns; every
    /// clean cube it or its victims touch turns too; the last clean cube standing wins.
    ///
    /// Purely event driven off the contact grid, so it is as deterministic as the collisions
    /// themselves. Contacts arrive in (min index, max index) order from the grid, which is what
    /// makes "two clean cubes touched two infected ones on the same step" reproducible.
    /// </summary>
    public sealed class InfectionSystem
    {
        private readonly ModeConfig _config;
        private readonly SimulationRandom _random;
        private bool _started;
        private float _now;
        private readonly System.Collections.Generic.Dictionary<int, float> _readyAt = new System.Collections.Generic.Dictionary<int, float>();

        public int InfectedCount { get; private set; }
        public Racer PatientZero { get; private set; }
        public Racer LastInfected { get; private set; }

        /// <summary>(victim, source or null for patient zero)</summary>
        public event Action<Racer, Racer> OnInfected;

        public InfectionSystem(ModeConfig config, SimulationRandom random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>Hook this to <see cref="RacerContactGrid.OnContact"/>.</summary>
        public void OnContact(Racer a, Racer b)
        {
            if (!_started) return;
            if (a.Infected == b.Infected) return;
            if (!a.IsActive || !b.IsActive) return;

            Racer carrier = a.Infected ? a : b;
            Racer target = a.Infected ? b : a;
            if (_readyAt.TryGetValue(carrier.Index, out float ready) && _now < ready) return;

            _readyAt[carrier.Index] = _now + _config.infectionBiteCooldown;
            Bites++;

            // A bite takes a heart; the cube turns when it has none left to give. With two hearts
            // that is two bites, which is what stretches a 25-second wipe into a real chase.
            if (target.Health > 1f)
            {
                target.Health -= 1f;
                OnBitten?.Invoke(target, carrier);
                return;
            }

            Infect(target, carrier);
        }

        public int Bites { get; private set; }

        /// <summary>(victim, carrier) - a bite that hurt but did not turn.</summary>
        public event Action<Racer, Racer> OnBitten;

        public void Step(float elapsed, Racer[] racers)
        {
            _now = elapsed;
            if (_started || elapsed < _config.infectionStart) return;
            _started = true;

            int alive = 0;
            for (int i = 0; i < racers.Length; i++) if (racers[i].IsActive) alive++;
            if (alive == 0) return;

            int pick = _random.Range(0, alive);
            for (int i = 0; i < racers.Length; i++)
            {
                if (!racers[i].IsActive) continue;
                if (pick-- == 0) { PatientZero = racers[i]; Infect(racers[i], null); return; }
            }
        }

        public int CleanAlive(Racer[] racers)
        {
            int clean = 0;
            for (int i = 0; i < racers.Length; i++)
                if (racers[i].IsActive && !racers[i].Infected) clean++;
            return clean;
        }

        public Racer FirstCleanAlive(Racer[] racers)
        {
            for (int i = 0; i < racers.Length; i++)
                if (racers[i].IsActive && !racers[i].Infected) return racers[i];
            return null;
        }

        private void Infect(Racer victim, Racer source)
        {
            if (victim.Infected) return;
            victim.Infected = true;
            victim.Badge = "☣";
            victim.Health = victim.MaxHealth;
            victim.Speed *= _config.infectedSpeedScale;
            if (victim.BaseSpeed > 0f) victim.BaseSpeed *= _config.infectedSpeedScale;
            InfectedCount++;
            LastInfected = victim;
            _readyAt[victim.Index] = _now + _config.infectionIncubation;
            OnInfected?.Invoke(victim, source);
        }
    }
}
