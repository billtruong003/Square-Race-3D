using System;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Core.Modes
{
    /// <summary>
    /// Hot Potato: one bomb, an 8 s fuse, passed by touch, lethal to its holder when it runs out
    /// and a heart to everybody nearby. Two seconds later a fresh bomb lands on a seeded survivor.
    /// The pass lockout stops two cubes stuck in a corner from trading it every step.
    /// </summary>
    public sealed class BombSystem
    {
        private readonly ModeConfig _config;
        private readonly SimulationRandom _random;

        private Racer _holder;
        private Racer _previousHolder;
        private float _passTime;
        private float _fuse;
        private float _respawnAt = -1f;
        private bool _armed;

        public Racer Holder => _holder;
        public float FuseRemaining => _holder != null ? _fuse : 0f;
        public int Explosions { get; private set; }
        public int Passes { get; private set; }

        public event Action<Racer, Racer> OnPassed;          // (from, to)
        public event Action<Racer> OnArmed;                  // new holder
        public event Action<Racer, Vector3> OnExploded;      // holder, position

        public BombSystem(ModeConfig config, SimulationRandom random)
        {
            _config = config;
            _random = random;
            _respawnAt = _config.bombRespawnDelay;   // first bomb after the opening beat
        }

        /// <summary>Hook this to <see cref="RacerContactGrid.OnContact"/>.</summary>
        public void OnContact(Racer a, Racer b)
        {
            if (_holder == null) return;
            Racer other = a == _holder ? b : b == _holder ? a : null;
            if (other == null || !other.IsActive) return;
            if (other == _previousHolder && _passTime + _config.bombPassLockout > _now) return;

            Racer from = _holder;
            _previousHolder = from;
            _passTime = _now;
            SetHolder(other);
            Passes++;
            OnPassed?.Invoke(from, other);
        }

        private float _now;

        public void Step(float elapsed, float deltaTime, Racer[] racers,
            Action<Racer, Racer, float, DeathCause> damage, Action<Racer, DeathCause> kill)
        {
            _now = elapsed;

            if (_holder != null && !_holder.IsActive)
            {
                // Holder died to something else (saw, crush): the bomb fizzles and re-arms later.
                ReleaseHolder();
                _holder = null;
                _respawnAt = elapsed + _config.bombRespawnDelay;
            }

            if (_holder == null)
            {
                if (_respawnAt >= 0f && elapsed >= _respawnAt) ArmRandom(racers);
                return;
            }

            _fuse -= deltaTime;
            if (_fuse > 0f) return;

            Racer victim = _holder;
            Vector3 at = victim.Position;
            ReleaseHolder();
            _holder = null;
            _previousHolder = null;
            Explosions++;

            // Blast first, then the holder, so the holder's own death does not shuffle the order
            // in which neighbours take damage.
            float radius = _config.bombBlastRadius;
            for (int i = 0; i < racers.Length; i++)
            {
                Racer r = racers[i];
                if (r == victim || !r.IsActive) continue;
                Vector3 d = r.Position - at; d.y = 0f;
                if (d.sqrMagnitude <= radius * radius) damage(r, victim, _config.bombBlastDamage, DeathCause.Hazard);
            }

            // If the blast just took the last rival, the holder is the survivor: a bomb that
            // kills everyone at once would end the round as a draw with nobody on the card.
            int others = 0;
            for (int i = 0; i < racers.Length; i++) if (racers[i] != victim && racers[i].IsActive) others++;
            if (others > 0) kill(victim, DeathCause.Hazard);
            OnExploded?.Invoke(victim, at);
            _respawnAt = elapsed + _config.bombRespawnDelay;
        }

        private void ArmRandom(Racer[] racers)
        {
            int alive = 0;
            for (int i = 0; i < racers.Length; i++) if (racers[i].IsActive) alive++;
            if (alive < 2) { _respawnAt = -1f; return; }

            int pick = _random.Range(0, alive);
            for (int i = 0; i < racers.Length; i++)
            {
                if (!racers[i].IsActive) continue;
                if (pick-- == 0) { _previousHolder = null; SetHolder(racers[i]); OnArmed?.Invoke(racers[i]); return; }
            }
        }

        private float _holderBaseSpeed;

        private void ReleaseHolder()
        {
            if (_holder == null) return;
            _holder.Badge = "";
            _holder.Speed = _holderBaseSpeed;
            if (_holder.BaseSpeed > 0f) _holder.BaseSpeed = _holderBaseSpeed;
        }

        private void SetHolder(Racer racer)
        {
            ReleaseHolder();
            _holder = racer;
            _holder.Badge = "💣";
            _holderBaseSpeed = racer.BaseSpeed > 0f ? racer.BaseSpeed : racer.Speed;
            racer.Speed = _holderBaseSpeed * _config.bombHolderSpeedScale;
            if (racer.BaseSpeed > 0f) racer.BaseSpeed = racer.Speed;
            if (!_armed || _fuse <= 0f) _fuse = _config.bombFuse;
            _armed = true;
            _respawnAt = -1f;
        }
    }
}
