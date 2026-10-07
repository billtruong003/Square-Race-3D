using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Racers;
using CubeSim.Core;

namespace CubeSim.Combat
{
    /// <summary>
    /// Weapon Arena behaviours: a held weapon works on its own while the cube keeps bouncing.
    /// Blades orbit the body, chainsaws grind in front, gloves punch, guns fire along the run,
    /// snipers lock a laser then shoot, launchers lob at the nearest cube. Damage happens where
    /// the viewer sees the blade or the bullet, so the fight reads from above without any AI.
    ///
    /// Everything here ticks inside CombatSystem.Step and only reads the racer arrays, so it is
    /// as deterministic as the rest of the simulation.
    /// </summary>
    public sealed partial class CombatSystem
    {
        private readonly Dictionary<long, float> _bladeCooldown = new Dictionary<long, float>();

        /// <summary>(shooter, target) - a sniper started aiming; the laser should draw until the shot.</summary>
        public event Action<Racer, Racer> OnAim;
        /// <summary>(position, seconds until impact) - a lobbed shell will land here.</summary>
        public event Action<Vector3, float> OnLobWarning;
        /// <summary>(racer) - a shield absorbed a hit or broke.</summary>
        public event Action<Racer, bool> OnShieldHit;

        private static long PairKey(Racer a, Racer b) => ((long)a.Index << 20) | (uint)b.Index;

        /// <summary>Dispatch by weapon kind. Classic melee/ranged (legacy modes) keep their old path.</summary>
        private void StepArsenalWeapon(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            switch (w.kind)
            {
                case WeaponKind.Orbit: StepOrbit(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Front: StepFront(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Punch: StepPunch(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Gun: StepGun(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Sniper: StepSniper(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Lob: StepLob(racer, racers, deltaTime, applyDamage); break;
                case WeaponKind.Shield: break;   // passive: Shield counter on the racer does the work
                case WeaponKind.Vest: break;     // StepFuses handles it
            }
        }

        // ---------------------------------------------------------------- blades

        /// <summary>A blade sweeping around the body: hitbox is the segment from the centre outward.</summary>
        private void StepOrbit(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            float period = Mathf.Max(0.1f, w.orbitPeriod);
            racer.WeaponAngle = (racer.WeaponAngle + 360f * deltaTime / period) % 360f;
            Vector3 dir = Quaternion.Euler(0f, racer.WeaponAngle, 0f) * Vector3.forward;
            SweepBlade(racer, racers, racer.Position, dir, w.bladeLength, w, applyDamage);
        }

        /// <summary>A chainsaw held in front: a short blade along the run direction, grinding every tick.</summary>
        private void StepFront(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            racer.WeaponAngle = Mathf.Atan2(racer.Direction.x, racer.Direction.z) * Mathf.Rad2Deg;
            SweepBlade(racer, racers, racer.Position + racer.Direction * racer.HalfExtent, racer.Direction, w.bladeLength, w, applyDamage);
        }

        private void SweepBlade(Racer racer, Racer[] racers, Vector3 origin, Vector3 dir, float length, WeaponDefinition w,
            Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            for (int i = 0; i < racers.Length; i++)
            {
                Racer other = racers[i];
                if (other == racer || !other.IsActive) continue;
                if (!_config.friendlyFire && other.Team == racer.Team) continue;

                Vector3 rel = other.Position - origin; rel.y = 0f;
                float along = Vector3.Dot(rel, dir);
                if (along < -other.HalfExtent || along > length + other.HalfExtent) continue;
                float side = (rel - dir * Mathf.Clamp(along, 0f, length)).magnitude;
                if (side > other.HalfExtent + w.bladeWidth) continue;

                long key = PairKey(racer, other);
                if (_bladeCooldown.TryGetValue(key, out float until) && _time < until) continue;
                _bladeCooldown[key] = _time + Mathf.Max(0.05f, w.attackCooldown);

                Vector3 contact = origin + dir * Mathf.Clamp(along, 0f, length);
                applyDamage(other, racer, w.damage, DeathCause.Melee);
                if (!other.Alive) _projectiles.CreditKill(w.id);
                if (other.IsActive && w.knockback > 0f) Knock(other, (other.Position - racer.Position), w.knockback);
                if (other.IsActive && w.staggerSeconds > 0f) Stagger(other, w.staggerSeconds);
                OnMeleeHit?.Invoke(other, racer, contact);
            }
        }

        // ---------------------------------------------------------------- aiming

        /// <summary>Turns the weapon heading toward a direction; returns the remaining error in degrees.</summary>
        private static float AimToward(Racer racer, Vector3 dir, float deltaTime, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return 180f;
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            racer.WeaponAngle = Mathf.MoveTowardsAngle(racer.WeaponAngle, target, degPerSec * deltaTime);
            return Mathf.Abs(Mathf.DeltaAngle(racer.WeaponAngle, target));
        }

        private static Vector3 AimDir(Racer racer) => Quaternion.Euler(0f, racer.WeaponAngle, 0f) * Vector3.forward;

        // ---------------------------------------------------------------- punch

        /// <summary>The glove swings round to the nearest cube and punches when it is lined up.</summary>
        private void StepPunch(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            Racer target = NearestInCone(racer, racers, w.attackRange, 180f, false);
            Vector3 want = target != null ? target.Position - racer.Position : racer.Direction;
            float err = AimToward(racer, want, deltaTime, 1080f);
            if (target == null || racer.AttackCooldown > 0f || err > 20f) return;

            racer.AttackCooldown = w.attackCooldown;
            Vector3 dir = AimDir(racer);
            racer.Visual?.PlayAttack(WeaponCategory.Melee);
            OnMeleeSwing?.Invoke(racer, dir);

            applyDamage(target, racer, w.damage, DeathCause.Melee);
            if (!target.Alive) _projectiles.CreditKill(w.id);
            if (target.IsActive) Knock(target, dir, w.knockback);
            if (target.IsActive && w.staggerSeconds > 0f) Stagger(target, w.staggerSeconds);
            OnMeleeHit?.Invoke(target, racer, Vector3.Lerp(racer.Position, target.Position, 0.6f));
        }

        // ---------------------------------------------------------------- guns

        /// <summary>The gun tracks the nearest cube in range and fires once it is lined up; bullets may ricochet.</summary>
        private void StepGun(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            Racer target = NearestInCone(racer, racers, w.attackRange, 180f, w.requireLineOfSight);
            Vector3 want = target != null ? target.Position - racer.Position : racer.Direction;
            float err = AimToward(racer, want, deltaTime, 720f);
            if (target == null || racer.AttackCooldown > 0f || err > 10f) return;

            racer.AttackCooldown = w.attackCooldown;
            Vector3 shot = AimDir(racer);
            racer.Visual?.PlayAttack(WeaponCategory.Ranged);
            Vector3 origin = racer.Position + shot * (racer.HalfExtent + 1.1f);
            OnRangedShot?.Invoke(racer, origin, shot);

            int pellets = Mathf.Max(1, w.pellets);
            for (int k = 0; k < pellets; k++)
            {
                float angle = pellets == 1 ? 0f : w.spreadDegrees * ((float)k / (pellets - 1) - 0.5f);
                if (pellets == 1 && w.spreadDegrees > 0f) angle = (_arenaRandom != null ? _arenaRandom.NextFloat() - 0.5f : 0f) * w.spreadDegrees;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * shot;
                _projectiles.Spawn(racer, w, origin, dir, w.attackRange);
            }
            ConsumeUse(racer);
        }

        /// <summary>Locks the nearest cube, tracks it with a laser, then fires a piercing line.</summary>
        private void StepSniper(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            if (racer.AimTarget == null || !racer.AimTarget.IsActive)
            {
                racer.AimTarget = null;
                Racer target = racer.AttackCooldown > 0f ? null : NearestInCone(racer, racers, w.attackRange, 180f, w.requireLineOfSight);
                if (target == null) { AimToward(racer, racer.Direction, deltaTime, 360f); return; }
                racer.AimTarget = target;
                racer.AimRemaining = w.aimSeconds;
                OnAim?.Invoke(racer, target);
                return;
            }

            AimToward(racer, racer.AimTarget.Position - racer.Position, deltaTime, 540f);
            racer.AimRemaining -= deltaTime;
            if (racer.AimRemaining > 0f) return;

            racer.AimTarget = null;
            Vector3 dir = AimDir(racer);
            racer.AttackCooldown = w.attackCooldown;
            racer.Visual?.PlayAttack(WeaponCategory.Ranged);
            Vector3 origin = racer.Position + dir * (racer.HalfExtent + 1.1f);
            OnRangedShot?.Invoke(racer, origin, dir);
            FireLine(racer, racers, dir, w, applyDamage);
            ConsumeUse(racer);
        }

        /// <summary>Hitscan line: every opponent along the beam, nearest first, up to pierce + 1.</summary>
        private void FireLine(Racer racer, Racer[] racers, Vector3 shot, WeaponDefinition weapon, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            var line = new List<Racer>();
            for (int i = 0; i < racers.Length; i++)
            {
                Racer other = racers[i];
                if (!other.IsActive || other == racer) continue;
                if (!_config.friendlyFire && other.Team == racer.Team) continue;
                Vector3 rel = other.Position - racer.Position; rel.y = 0f;
                float along = Vector3.Dot(rel, shot);
                if (along <= 0f || along > weapon.attackRange) continue;
                float side = (rel - shot * along).magnitude;
                if (side > other.HalfExtent + 0.3f) continue;
                line.Add(other);
            }
            line.Sort((a, b) => Vector3.Dot(a.Position - racer.Position, shot).CompareTo(Vector3.Dot(b.Position - racer.Position, shot)));
            int budget = weapon.pierce + 1;
            for (int i = 0; i < line.Count && i < budget; i++)
            {
                Racer hit = line[i];
                applyDamage(hit, racer, weapon.damage, DeathCause.Ranged);
                if (!hit.Alive) _projectiles.CreditKill(weapon.id);
                if (hit.IsActive && weapon.knockback > 0f) Knock(hit, shot, weapon.knockback);
                if (hit.IsActive && weapon.staggerSeconds > 0f) Stagger(hit, weapon.staggerSeconds);
                _projectiles.ReportHitscanHit(hit, racer, hit.Position, shot);
            }
        }

        /// <summary>Swings the launcher round to the nearest cube and lobs a shell at its current spot.</summary>
        private void StepLob(Racer racer, Racer[] racers, float deltaTime, Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            WeaponDefinition w = racer.Weapon;
            Racer target = NearestInCone(racer, racers, w.attackRange, 180f, false);
            Vector3 want = target != null ? target.Position - racer.Position : racer.Direction;
            float err = AimToward(racer, want, deltaTime, 540f);
            if (target == null || racer.AttackCooldown > 0f || err > 8f) return;

            Vector3 to = target.Position - racer.Position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 2.5f) return;   // do not shell your own feet
            Vector3 dir = AimDir(racer);

            racer.AttackCooldown = w.attackCooldown;
            racer.Visual?.PlayAttack(WeaponCategory.Ranged);
            Vector3 origin = racer.Position + dir * (racer.HalfExtent + 1.1f);
            OnRangedShot?.Invoke(racer, origin, dir);
            OnLobWarning?.Invoke(racer.Position + dir * dist, dist / Mathf.Max(1f, w.projectileSpeed));
            _projectiles.Spawn(racer, w, origin, dir, dist);
            ConsumeUse(racer);
        }

        // ---------------------------------------------------------------- helpers

        private Racer NearestInCone(Racer racer, Racer[] racers, float range, float arcDegrees, bool needLineOfSight)
        {
            float cosArc = Mathf.Cos(Mathf.Clamp(arcDegrees, 1f, 180f) * Mathf.Deg2Rad);
            Racer best = null;
            float bestSqr = (range + racer.HalfExtent) * (range + racer.HalfExtent);
            for (int i = 0; i < racers.Length; i++)
            {
                Racer other = racers[i];
                if (other == racer || !other.IsActive) continue;
                if (!_config.friendlyFire && other.Team == racer.Team) continue;
                Vector3 offset = other.Position - racer.Position; offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (arcDegrees < 180f && sqr > 1e-6f && Vector3.Dot(offset / Mathf.Sqrt(sqr), racer.Direction) < cosArc) continue;
                if (needLineOfSight && !HasLineOfSight(racer, other)) continue;
                bestSqr = sqr;
                best = other;
            }
            return best;
        }

        /// <summary>Stagger: the victim stands still for a moment (speed 0, restored by the boost expiry).</summary>
        public void Stagger(Racer victim, float seconds)
        {
            if (victim == null || !victim.IsActive) return;
            if (victim.BoostUntil <= 0f) victim.BaseSpeed = victim.Speed;
            victim.Speed = 0f;
            victim.BoostUntil = _time + seconds;
            victim.StaggerUntil = _time + seconds;
        }

        // ---------------------------------------------------------------- weapon pads

        private sealed class Pad
        {
            public WeaponSpawnArea Area;
            public WeaponDefinition Weapon;
            public WeaponPickup Pickup;
            public float RespawnAt;
        }

        private readonly List<Pad> _pads = new List<Pad>();
        private readonly HashSet<WeaponPickup> _padPickups = new HashSet<WeaponPickup>();
        /// <summary>Loose weapons that did not grow on a pad (airdrops, a dead cube's weapon).</summary>
        private int LooseWeaponsOnFloor { get { int n = 0; for (int i = 0; i < _pickups.Count; i++) if (_pickups[i].Available && !_padPickups.Contains(_pickups[i])) n++; return n; } }

        /// <summary>Fixed pads: each holds one weapon type and regrows it after a delay.</summary>
        private void BuildPads()
        {
            if (_arena.WeaponAreas == null) return;
            for (int i = 0; i < _arena.WeaponAreas.Count; i++)
            {
                WeaponSpawnArea area = _arena.WeaponAreas[i];
                if (area == null || string.IsNullOrEmpty(area.WeaponId)) continue;
                WeaponDefinition def = _arenaCatalog.Find(d => d.id == area.WeaponId);
                if (def == null) continue;
                var pad = new Pad { Area = area, Weapon = def, RespawnAt = 0f };
                _pads.Add(pad);
            }
        }

        /// <summary>A flat tinted disc on the floor: the viewer learns where each weapon grows.</summary>
        private void BuildPadMarker(WeaponSpawnArea area, WeaponDefinition def)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "PadMarker_" + def.id;
            var col = disc.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(col); else UnityEngine.Object.DestroyImmediate(col); }
            disc.transform.SetParent(_root, false);
            disc.transform.position = new Vector3(area.Center.x, _groundY + 0.04f, area.Center.z);
            disc.transform.localScale = new Vector3(3.4f, 0.03f, 3.4f);
            var renderer = disc.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color c = def.color == Color.white ? new Color(1f, 0.85f, 0.3f) : def.color; c.a = 1f;
            renderer.sharedMaterial = _materials.GetTinted("pad_" + def.id, c, 0.9f);
        }

        private void StepPads()
        {
            for (int i = 0; i < _pads.Count; i++)
            {
                Pad pad = _pads[i];
                if (pad.Pickup != null)
                {
                    if (pad.Pickup.Available) continue;
                    // Taken: the pad forgets it (the pickup now lives with its owner) and regrows later.
                    pad.Pickup = null;
                    pad.RespawnAt = _time + pad.Area.RespawnSeconds;
                    continue;
                }
                if (_time < pad.RespawnAt) continue;
                Vector3 at = FindValidDropPosition(pad.Area.Center);
                pad.Pickup = new WeaponPickup(pad.Weapon, at, _groundY, _materials, _root, _pickupScale, _visuals);
                _pickups.Add(pad.Pickup);
                _padPickups.Add(pad.Pickup);
                OnDropLanded?.Invoke(at, pad.Weapon);
            }
        }
    }
}
