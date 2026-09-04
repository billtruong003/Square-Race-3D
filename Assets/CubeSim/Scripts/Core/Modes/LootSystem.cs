using System;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Combat;
using CubeSim.Racers;

namespace CubeSim.Core.Modes
{
    public enum LootKind { Knife = 0, Potion = 1, Shield = 2, Boost = 3, Bomb = 4 }

    /// <summary>
    /// Lucky Block: every crate broken rolls one seeded drop for the cube that broke it. Knives
    /// appear on the floor where the crate stood; potion, shield and boost go straight to the
    /// breaker; the bomb goes off in everybody's face, breaker included.
    /// </summary>
    public sealed class LootSystem
    {
        private readonly ModeConfig _config;
        private readonly SimulationRandom _random;
        private readonly CombatSystem _combat;
        private readonly Racer[] _racers;
        private readonly Action<Racer, Racer, float, DeathCause> _damage;
        private readonly float[] _weights;
        private float _now;

        public int Drops { get; private set; }
        public int KnivesDropped { get; private set; }

        /// <summary>(kind, breaker, crate position)</summary>
        public event Action<LootKind, Racer, Vector3> OnLoot;

        public LootSystem(ModeConfig config, SimulationRandom random, CombatSystem combat, Racer[] racers,
            BreakableWallSystem walls, Action<Racer, Racer, float, DeathCause> damage)
        {
            _config = config;
            _random = random;
            _combat = combat;
            _racers = racers;
            _damage = damage;
            _weights = new[] { config.lootKnife, config.lootPotion, config.lootShield, config.lootBoost, config.lootBomb };
            if (walls != null) walls.OnWallBroken += OnCrateBroken;
        }

        public void Step(float elapsed)
        {
            _now = elapsed;
            // Boost expiry lives here because crates are not devices, so the device system may
            // not exist to restore the speed.
            for (int i = 0; i < _racers.Length; i++)
            {
                Racer r = _racers[i];
                if (r.BoostUntil > 0f && elapsed >= r.BoostUntil)
                {
                    r.Speed = r.BaseSpeed;
                    r.BoostUntil = 0f;
                }
            }
        }

        private void OnCrateBroken(BreakableWall wall, Racer breaker)
        {
            if (wall == null || wall.GetComponent<LootCrate>() == null) return;
            Vector3 at = wall.transform.position;
            at.y = breaker != null ? breaker.Position.y : at.y;

            LootKind kind = Roll();
            Drops++;
            // A crate map with no knife is a stall waiting to happen (LB06 seed ran to the watchdog
            // with two cubes circling an empty pit): by the fourth crate one must be a knife.
            if (KnivesDropped == 0 && Drops >= 4) kind = LootKind.Knife;

            switch (kind)
            {
                case LootKind.Knife:
                    _combat?.SpawnPickup(new Vector3(at.x, 0f, at.z));
                    KnivesDropped++;
                    break;

                case LootKind.Potion:
                    if (breaker != null && breaker.IsActive) breaker.Health = Mathf.Min(breaker.MaxHealth, breaker.Health + 1f);
                    break;

                case LootKind.Shield:
                    if (breaker != null && breaker.IsActive) { breaker.Shield = 1; breaker.Badge = "🛡"; }
                    break;

                case LootKind.Boost:
                    if (breaker != null && breaker.IsActive)
                    {
                        if (breaker.BoostUntil <= 0f) breaker.BaseSpeed = breaker.Speed;
                        breaker.Speed = breaker.BaseSpeed * _config.lootBoostScale;
                        breaker.BoostUntil = _now + _config.lootBoostSeconds;
                    }
                    break;

                case LootKind.Bomb:
                {
                    float radius = 3f;
                    for (int i = 0; i < _racers.Length; i++)
                    {
                        Racer r = _racers[i];
                        if (!r.IsActive) continue;
                        Vector3 d = r.Position - at; d.y = 0f;
                        if (d.sqrMagnitude <= radius * radius) _damage(r, null, 1f, DeathCause.Hazard);
                    }
                    break;
                }
            }

            OnLoot?.Invoke(kind, breaker, at);
        }

        private LootKind Roll()
        {
            float total = 0f;
            for (int i = 0; i < _weights.Length; i++) total += Mathf.Max(0f, _weights[i]);
            float pick = _random.NextFloat() * total;
            for (int i = 0; i < _weights.Length; i++)
            {
                pick -= Mathf.Max(0f, _weights[i]);
                if (pick <= 0f) return (LootKind)i;
            }
            return LootKind.Potion;
        }
    }
}
