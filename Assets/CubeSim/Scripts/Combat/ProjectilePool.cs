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
                    Retire(p);
                    continue;
                }

                Racer victim = FindHit(p, racers, step);
                p.Position += p.Direction * step;
                p.RemainingRange -= step;
                p.Transform.localPosition = p.Position;

                if (victim != null)
                {
                    Racer shooter = p.Owner;
                    Vector3 impact = victim.Position;
                    Vector3 travel = p.Direction;

                    applyDamage(victim, shooter, p.Damage, DeathCause.Ranged);
                    Retire(p);

                    OnHitRacer?.Invoke(victim, shooter, impact, travel);
                }
                else if (p.RemainingRange <= 0f)
                {
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
                if (!racer.Alive || racer == p.Owner) continue;

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
