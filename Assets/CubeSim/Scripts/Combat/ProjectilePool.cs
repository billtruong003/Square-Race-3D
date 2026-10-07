using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Combat
{
    /// <summary>
    /// Travelling shots for ranged weapons. Pooled and stepped in one flat loop, using the same
    /// cast-based approach as the racers - walls stop a bolt exactly where they should.
    ///
    /// Gameplay size and visual size are deliberately separate. <see cref="WeaponDefinition.projectileRadius"/>
    /// is the collision radius and is the only value the simulation reads; the model on top comes from
    /// the weapon visual library and can be far larger, because a physically correct 1 cm bullet is
    /// invisible at this camera height.
    /// </summary>
    public sealed class ProjectilePool
    {
        private sealed class Projectile
        {
            public Transform Transform;
            public string WeaponId;
            public Vector3 Position;
            public Vector3 Direction;
            public float Speed;
            public float RemainingRange;
            public float Radius;
            public float Damage;
            public Racer Owner;
            public bool Active;
            public int Pierce;
            public bool Lobbed;
            public float BlastRadius;
            public float BlastEdgeDamage;
            public float Knockback;
            public float TotalRange;
            public int Bounces;
            public int BouncesAtSpawn;
        }

        private readonly List<Projectile> _projectiles = new List<Projectile>(32);
        private readonly Transform _root;
        private readonly MaterialLibrary _materials;
        private readonly WeaponVisualLibrary _visuals;
        private readonly int _wallMask;
        private readonly float _groundY;

        /// <summary>(impact point, travel direction) - a shot that ended on static geometry.</summary>
        public event Action<Vector3, Vector3> OnHitWall;

        /// <summary>(victim, shooter, impact point, travel direction) - a shot that connected.</summary>
        public event Action<Racer, Racer, Vector3, Vector3> OnHitRacer;

        /// <summary>(shooter or null, centre, radius) - a blast went off.</summary>
        public event Action<Racer, Vector3, float> OnBlast;

        /// <summary>Set by the combat system so blasts and hits can shove racers.</summary>
        public Action<Racer, Vector3, float> Knock;

        private Racer[] _racersForBlast;

        /// <summary>Radial damage: full at the centre, edge damage at the rim, plus knockback.</summary>
        /// <summary>Kills credited per weapon id, for balance reports.</summary>
        public readonly System.Collections.Generic.Dictionary<string, int> KillsByWeapon = new System.Collections.Generic.Dictionary<string, int>();

        public void CreditKill(string weaponId)
        {
            KillsByWeapon[weaponId] = KillsByWeapon.TryGetValue(weaponId, out int n) ? n + 1 : 1;
        }

        public void Detonate(Racer source, Vector3 centre, float radius, float centreDamage, float edgeDamage, float knockback,
            Action<Racer, Racer, float, DeathCause> applyDamage, string weaponId = "Blast", bool sourceDies = false)
        {
            if (_racersForBlast == null) return;
            // With the bomber about to die too, the field must keep one other cube alive.
            int keep = sourceDies ? 2 : 1;

            // Nearest first, and never the last two at once: a blast that wiped the field would
            // end the round as a draw with nobody on the card.
            var order = new System.Collections.Generic.List<Racer>();
            for (int i = 0; i < _racersForBlast.Length; i++)
            {
                Racer r = _racersForBlast[i];
                if (!r.IsActive) continue;
                Vector3 d = r.Position - centre; d.y = 0f;
                if (d.magnitude <= radius + r.HalfExtent) order.Add(r);
            }
            order.Sort((a, b) => (a.Position - centre).sqrMagnitude.CompareTo((b.Position - centre).sqrMagnitude));

            for (int i = 0; i < order.Count; i++)
            {
                Racer r = order[i];
                if (!r.IsActive) continue;
                int active = 0; for (int j = 0; j < _racersForBlast.Length; j++) if (_racersForBlast[j].IsActive) active++;
                Vector3 d = r.Position - centre; d.y = 0f;
                float dist = d.magnitude;
                float t = Mathf.Clamp01(dist / Mathf.Max(0.01f, radius));
                float dmg = Mathf.Lerp(centreDamage, edgeDamage, t);
                if (active <= keep && dmg >= r.Health) dmg = Mathf.Max(0f, r.Health - 1f);   // the survivor is scorched, not killed
                applyDamage(r, source, dmg, DeathCause.Ranged);
                if (!r.Alive) CreditKill(weaponId);
                if (r.IsActive && knockback > 0f && dist > 1e-3f) Knock?.Invoke(r, d / dist, knockback * (1f - 0.5f * t));
            }
            OnBlast?.Invoke(source, centre, radius);
        }

        public ProjectilePool(Transform parent, MaterialLibrary materials, float groundY,
            WeaponVisualLibrary visuals)
        {
            _materials = materials;
            _visuals = visuals;
            _groundY = groundY;
            _wallMask = SimulationLayers.WallMask;

            _root = new GameObject("Projectiles").transform;
            _root.SetParent(parent, false);
        }

        public void Spawn(Racer owner, WeaponDefinition weapon, Vector3 origin, Vector3 direction, float range)
        {
            Projectile projectile = Rent(weapon);

            projectile.Owner = owner;
            projectile.Position = new Vector3(origin.x, _groundY + owner.HalfExtent * 0.9f, origin.z);
            projectile.Direction = direction;
            projectile.Speed = weapon.projectileSpeed;
            projectile.RemainingRange = range;
            projectile.Radius = weapon.projectileRadius;
            projectile.Damage = weapon.damage;
            projectile.Pierce = weapon.pierce;
            projectile.Lobbed = weapon.lobbed;
            projectile.BlastRadius = weapon.blastRadius;
            projectile.BlastEdgeDamage = weapon.blastEdgeDamage;
            projectile.Knockback = weapon.knockback;
            projectile.TotalRange = range;
            projectile.Bounces = weapon.bounces;
            projectile.BouncesAtSpawn = weapon.bounces;
            projectile.Active = true;

            projectile.Transform.gameObject.SetActive(true);
            projectile.Transform.localPosition = projectile.Position;
            projectile.Transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>
        /// Hitscan weapons resolve without ever creating a projectile, but they should still read as
        /// a hit on screen, so they report through the same event.
        /// </summary>
        public void ReportHitscanHit(Racer victim, Racer shooter, Vector3 impact, Vector3 direction)
            => OnHitRacer?.Invoke(victim, shooter, impact, direction);

        /// <summary>Advances every live shot and applies the first hit it finds.</summary>
        public void Step(float deltaTime, Racer[] racers, System.Action<Racer, Racer, float, DeathCause> applyDamage)
        {
            _racersForBlast = racers;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                Projectile p = _projectiles[i];
                if (!p.Active) continue;

                float step = Mathf.Min(p.Speed * deltaTime, p.RemainingRange);
                if (step <= 0f) { Retire(p); continue; }

                // Walls stop the bolt before it can reach anything behind them.
                if (Physics.SphereCast(p.Position, p.Radius, p.Direction, out RaycastHit hit, step,
                        _wallMask, QueryTriggerInteraction.Ignore))
                {
                    p.Position += p.Direction * Mathf.Max(0f, hit.distance);
                    p.Transform.localPosition = p.Position;
                    OnHitWall?.Invoke(p.Position, p.Direction);
                    if (p.Bounces > 0 && p.BlastRadius <= 0f)
                    {
                        // Ricochet: reflect off the wall face and keep going with the rest of the range.
                        p.Bounces--;
                        Vector3 n = hit.normal; n.y = 0f;
                        if (n.sqrMagnitude > 1e-6f) p.Direction = Vector3.Reflect(p.Direction, n.normalized);
                        p.Position += p.Direction * (p.Radius + 0.05f);
                        p.Transform.localPosition = p.Position;
                        p.Transform.localRotation = Quaternion.LookRotation(p.Direction, Vector3.up);
                        p.RemainingRange -= Mathf.Max(0f, hit.distance);
                        continue;
                    }
                    if (p.BlastRadius > 0f) Detonate(p.Owner, p.Position, p.BlastRadius, p.Damage, p.BlastEdgeDamage, p.Knockback, applyDamage, p.WeaponId);
                    Retire(p);
                    continue;
                }

                // A lobbed shell flies over heads until it lands; it only hits what it lands on.
                Racer victim = p.Lobbed && p.RemainingRange > step + 0.01f ? null : FindHit(p, racers, step);
                p.Position += p.Direction * step;
                p.RemainingRange -= step;
                float arc = p.Lobbed && p.TotalRange > 0f ? 4f * (p.RemainingRange / p.TotalRange) * (1f - p.RemainingRange / p.TotalRange) : 0f;
                p.Transform.localPosition = p.Position + new Vector3(0f, arc * 2.5f, 0f);

                if (victim != null)
                {
                    Racer shooter = p.Owner;
                    Vector3 impact = victim.Position;
                    Vector3 travel = p.Direction;

                    if (p.BlastRadius > 0f)
                    {
                        Detonate(shooter, p.Position, p.BlastRadius, p.Damage, p.BlastEdgeDamage, p.Knockback, applyDamage, p.WeaponId);
                        Retire(p);
                    }
                    else
                    {
                        // A bullet fired by a cube that has since died must not take the last
                        // survivor with it (two shots crossing on the same tick made a draw).
                        float dmg = p.Damage;
                        int active = 0; for (int j = 0; j < racers.Length; j++) if (racers[j].IsActive) active++;
                        if (active <= 1 && dmg >= victim.Health) dmg = Mathf.Max(0f, victim.Health - 1f);
                        applyDamage(victim, shooter, dmg, DeathCause.Ranged);
                        if (!victim.Alive) CreditKill(p.WeaponId);
                        if (victim.IsActive && p.Knockback > 0f) Knock?.Invoke(victim, travel, p.Knockback);
                        if (p.Pierce > 0) p.Pierce--; else Retire(p);
                    }

                    OnHitRacer?.Invoke(victim, shooter, impact, travel);
                }
                else if (p.RemainingRange <= 0f)
                {
                    if (p.BlastRadius > 0f) Detonate(p.Owner, p.Position, p.BlastRadius, p.Damage, p.BlastEdgeDamage, p.Knockback, applyDamage, p.WeaponId);
                    Retire(p);
                }
            }
        }

        /// <summary>Swept box test against every living racer other than the shooter.</summary>
        private static Racer FindHit(Projectile p, Racer[] racers, float step)
        {
            Racer best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.Alive || (racer == p.Owner && p.Bounces == p.BouncesAtSpawn)) continue;

                Vector3 offset = racer.Position - p.Position;
                float along = offset.x * p.Direction.x + offset.z * p.Direction.z;
                if (along < -racer.HalfExtent || along > step + racer.HalfExtent) continue;

                float clamped = Mathf.Clamp(along, 0f, step);
                Vector3 closest = p.Position + p.Direction * clamped;
                float dx = Mathf.Abs(closest.x - racer.Position.x);
                float dz = Mathf.Abs(closest.z - racer.Position.z);
                float reach = racer.HalfExtent + p.Radius;

                if (dx <= reach && dz <= reach && clamped < bestDistance)
                {
                    bestDistance = clamped;
                    best = racer;
                }
            }

            return best;
        }

        /// <summary>
        /// Reuse is keyed on the weapon id: each weapon carries its own bullet model, so handing a
        /// retired shotgun shell to a rifle would silently swap the model mid-episode.
        /// </summary>
        private Projectile Rent(WeaponDefinition weapon)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                if (!_projectiles[i].Active && _projectiles[i].WeaponId == weapon.id) return _projectiles[i];
            }

            var holder = new GameObject("Bolt_" + weapon.id);
            holder.transform.SetParent(_root, false);

            GameObject model = WeaponVisualFactory.CreateProjectile(weapon, holder.transform, _visuals,
                out float visualScale);

            // No pack model for this weapon: fall back to a stretched primitive, still sized by the
            // visual scale rather than by the collision radius.
            if (model == null) BuildPrimitiveBolt(weapon, holder.transform, visualScale);

            var projectile = new Projectile { Transform = holder.transform, WeaponId = weapon.id };
            _projectiles.Add(projectile);
            return projectile;
        }

        private void BuildPrimitiveBolt(WeaponDefinition weapon, Transform parent, float visualScale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Bolt";
            DestroyComponent(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = _materials.GetWeaponMaterial(weapon);

            float r = Mathf.Max(0.05f, weapon.projectileRadius) * visualScale;
            go.transform.localScale = new Vector3(r * 2f, r * 2f, r * 5f);
        }

        private static void Retire(Projectile p)
        {
            p.Active = false;
            p.Owner = null;
            p.Transform.gameObject.SetActive(false);
        }

        private static void DestroyComponent(UnityEngine.Object component)
        {
            if (component == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(component);
            else UnityEngine.Object.DestroyImmediate(component);
        }
    }
}
