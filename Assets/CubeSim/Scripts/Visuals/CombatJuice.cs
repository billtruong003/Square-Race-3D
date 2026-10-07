using UnityEngine;
using CubeSim.Combat;
using CubeSim.Core;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The Arena "game feel" layer: hit-stop, screen shake, hit flash, comic text, per-weapon
    /// effects and sounds, laser sights and lob marks. It only listens to combat events and only
    /// touches presentation (and the bootstrap's step gate for hit-stop), so the simulation stays
    /// deterministic and the older modes, which never construct it, stay exactly as they were.
    /// </summary>
    public sealed class CombatJuice
    {
        private readonly VfxSystem _vfx;
        private readonly SimAudioSystem _audio;
        private readonly DamagePopupSystem _popups;
        private readonly CameraShake _shake;
        private readonly LaserSightSystem _lasers;

        /// <summary>Simulation ticks the bootstrap should skip: the freeze frame on a hit.</summary>
        public int HitStopTicks { get; private set; }

        public CombatJuice(VfxSystem vfx, SimAudioSystem audio, DamagePopupSystem popups, CameraShake shake, LaserSightSystem lasers)
        {
            _vfx = vfx; _audio = audio; _popups = popups; _shake = shake; _lasers = lasers;
        }

        /// <summary>Called by the bootstrap before each step; true = hold this tick.</summary>
        public bool ConsumeHitStop()
        {
            if (HitStopTicks <= 0) return false;
            HitStopTicks--;
            return true;
        }

        private void HitStop(int ticks) { if (ticks > HitStopTicks) HitStopTicks = ticks; }

        private const float KillCamSeconds = 1.4f;
        private const float KillCamTimeScale = 0.3f;
        private float _slowLeft = -1f;

        /// <summary>Seconds of real time the finishing blow is stretched over; the director waits this long before its card.</summary>
        public static float KillCamHold => KillCamSeconds;

        /// <summary>The finishing blow: the world slows and the camera leans in on the kill.</summary>
        private void KillCam(Vector3 at)
        {
            Time.timeScale = KillCamTimeScale;
            _slowLeft = KillCamSeconds;
            _shake?.PunchIn(at, 0.28f, 0.25f, KillCamSeconds - 0.25f, 0.6f);
        }

        public void Attach(CombatSystem combat, SimulationRunner runner)
        {
            if (combat == null) return;

            combat.OnMeleeSwing += (racer, dir) =>
            {
                if (racer.Weapon != null && racer.Weapon.kind == WeaponKind.Punch)
                {
                    _vfx?.Play(VfxId.PunchSwing, racer.Position + dir * (racer.HalfExtent + 0.6f), dir, racer.Color);
                    _audio?.Play(SimSoundId.Whoosh);
                }
            };

            combat.OnMeleeHit += (victim, attacker, contact) =>
            {
                WeaponKind kind = attacker != null && attacker.Weapon != null ? attacker.Weapon.kind : WeaponKind.Classic;
                Vector3 dir = victim.Position - attacker.Position; dir.y = 0f;
                if (kind == WeaponKind.Vest) return;   // the blast handler covers the vest
                switch (kind)
                {
                    case WeaponKind.Punch:
                        _vfx?.Play(VfxId.PunchHit, contact, dir, attacker.Color);
                        _vfx?.Play(VfxId.TextPow, contact + Vector3.up * 0.5f, Vector3.forward);
                        _audio?.Play(SimSoundId.PunchHit);
                        HitStop(4); _shake?.Add(0.45f, 0.22f);
                        break;
                    case WeaponKind.Front:
                        _vfx?.Play(VfxId.ChainsawGrind, contact, dir, victim.Color);
                        _audio?.Play(SimSoundId.ChainsawHit);
                        HitStop(1); _shake?.Add(0.12f, 0.1f);
                        break;
                    default:
                        _vfx?.Play(VfxId.SlashHit, contact, dir, attacker.Color);
                        _vfx?.Play(VfxId.TextKapow, contact + Vector3.up * 0.5f, Vector3.forward);
                        _audio?.Play(SimSoundId.BladeHit);
                        HitStop(3); _shake?.Add(0.3f, 0.18f);
                        break;
                }
                victim.Visual?.Flash(0.09f);
            };

            combat.OnRangedShot += (racer, origin, dir) =>
            {
                string id = racer.Weapon != null ? racer.Weapon.id : "";
                switch (id)
                {
                    case "Shotgun": _vfx?.Play(VfxId.GunMuzzle, origin, dir, racer.Color); _audio?.Play(SimSoundId.ShotgunShot); _shake?.Add(0.25f, 0.12f); break;
                    case "Sniper":
                        _vfx?.Play(VfxId.GunMuzzle, origin, dir, racer.Color); _audio?.Play(SimSoundId.SniperShot);
                        _lasers?.Beam(origin, origin + dir * (racer.Weapon != null ? racer.Weapon.attackRange : 30f), 0.18f);
                        _shake?.Add(0.35f, 0.15f);
                        break;
                    case "Rocket": _vfx?.Play(VfxId.GunMuzzle, origin, dir, racer.Color); _audio?.Play(SimSoundId.RocketShot); _shake?.Add(0.2f, 0.1f); break;
                    case "Grenade": _audio?.Play(SimSoundId.GrenadeShot); break;
                    default: _vfx?.Play(VfxId.GunMuzzle, origin, dir, racer.Color); _audio?.Play(SimSoundId.GunShot); break;
                }
            };

            combat.OnAim += (shooter, target) =>
            {
                float seconds = shooter.Weapon != null ? shooter.Weapon.aimSeconds : 0.8f;
                _lasers?.Aim(shooter, target, seconds);
                _audio?.Play(SimSoundId.SpikeWarn);
            };

            combat.OnLobWarning += (at, seconds) =>
            {
                _lasers?.MarkFloor(at, 4f, seconds);
            };

            combat.OnShieldHit += (racer, broke) =>
            {
                _vfx?.Play(VfxId.ShieldBlock, racer.Position, racer.Direction, new Color(0.4f, 0.8f, 1f));
                _audio?.Play(SimSoundId.ShieldBlock);
                if (broke) _popups?.Show(racer.Position, new Color(0.5f, 0.85f, 1f), "SHIELD BROKE", 1.3f);
                else _popups?.Show(racer.Position, new Color(0.5f, 0.85f, 1f), "BLOCK", 1.1f);
            };

            combat.OnKnock += (victim, dir, metres) =>
            {
                victim.Visual?.Launch(dir, metres);
            };

            if (combat.Projectiles != null)
            {
                combat.Projectiles.OnHitRacer += (victim, shooter, point, dir) =>
                {
                    victim.Visual?.Flash(0.07f);
                    _audio?.Play(SimSoundId.BulletHit);
                    HitStop(1); _shake?.Add(0.15f, 0.1f);
                };

                combat.Projectiles.OnBlast += (source, centre, radius) =>
                {
                    _vfx?.Play(VfxId.Explosion, centre, Vector3.forward);
                    _vfx?.Play(VfxId.ExplosionRing, centre, Vector3.forward);
                    _vfx?.Play(VfxId.TextBoom, centre + Vector3.up * 0.8f, Vector3.forward);
                    _audio?.Play(SimSoundId.Explosion);
                    HitStop(5); _shake?.Add(1.1f, 0.4f);
                };
            }

            if (runner != null)
            {
                runner.OnRacerDamaged += (victim, attacker, amount) =>
                {
                    if (amount >= 50f)
                    {
                        _vfx?.Play(VfxId.TextCritical, victim.Position + Vector3.up * 1.2f, Vector3.forward);
                        _audio?.Play(SimSoundId.Critical);
                        HitStop(6); _shake?.Add(0.7f, 0.3f);
                    }
                };

                runner.OnRacerKilled += (victim, killer, cause) =>
                {
                    _vfx?.Play(VfxId.Kill, victim.Position, victim.Direction, victim.Color);
                    _popups?.Show(victim.Position, new Color(1f, 0.85f, 0.2f), "K.O.", 2.2f);
                    _audio?.Play(SimSoundId.Finisher);
                    HitStop(8); _shake?.Add(0.9f, 0.45f);
                    // The last kill of the round gets the kill-cam (one survivor left = round over).
                    if (runner.AliveCount <= 1) KillCam(victim.Position);
                };
            }
        }

        public void Tick(float deltaTime)
        {
            _lasers?.Tick(deltaTime);
            if (_slowLeft >= 0f)
            {
                _slowLeft -= Time.unscaledDeltaTime;
                if (_slowLeft < 0f) Time.timeScale = 1f;
            }
        }

        public void Dispose()
        {
            _lasers?.Dispose();
            if (_slowLeft >= 0f) { Time.timeScale = 1f; _slowLeft = -1f; }
        }
    }
}
