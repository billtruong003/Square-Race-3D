using UnityEngine;
using CubeSim.Combat;

namespace CubeSim.Racers
{
    public enum DeathCause
    {
        None = 0,
        Crushed = 1,
        Melee = 2,
        Ranged = 3,
        Hazard = 4
    }

    /// <summary>
    /// Runtime state for one racer: the simulation root. It owns position, collision extent,
    /// direction, speed and combat state.
    ///
    /// Deliberately a plain class rather than a MonoBehaviour: the runner iterates a flat array once
    /// per step, so there is no per-racer Update dispatch and no component lookups in the loop. The
    /// animated model hanging off <see cref="Visual"/> is cosmetic and never drives locomotion.
    /// </summary>
    public sealed class Racer
    {
        public readonly int Index;
        public readonly string Id;
        public readonly Transform Transform;
        public readonly RacerVisual Visual;

        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;

        /// <summary>
        /// Where this racer stood before the current step's movement. The swept racer-vs-racer test
        /// needs the segment travelled, not just the end point, or a fast pair swaps sides without
        /// ever registering an overlap.
        /// </summary>
        public Vector3 PreviousPosition;

        /// <summary>Half the collision box edge. The mover casts a box of exactly this size.</summary>
        public float HalfExtent;

        public float Radius => HalfExtent;

        public int Team;
        public Color Color;

        /// <summary>Cosmetic identity: the species name the UI shouts ("PIG", "PANDA"...).</summary>
        public string DisplayName = "";

        /// <summary>Cosmetic identity: the face the leaderboard shows. Null falls back to a swatch.</summary>
        public Sprite Portrait;

        public float Health;
        public float MaxHealth;
        public bool Alive = true;
        public DeathCause Cause = DeathCause.None;
        public float DeathTime = -1f;

        public WeaponDefinition Weapon;
        public WeaponPickup HeldPickup;
        public float AttackCooldown;

        /// <summary>Seconds of ownership left under TimeBased release.</summary>
        public float WeaponHoldRemaining;

        /// <summary>Uses left under AmmoBased release.</summary>
        public int WeaponAmmo;

        public bool ReachedGoal;
        public float GoalTime = -1f;
        public int Placement = -1;

        /// <summary>Finished the course: stops moving, stops attacking, cannot be crushed.</summary>
        public bool Retired;

        public int BounceCount;

        /// <summary>Bounces off other racers specifically. BounceCount also counts walls and pressure.</summary>
        public int RacerBounceCount;

        public float DistanceTravelled;
        public int Kills;

        /// <summary>How many times this racer has picked a weapon up. Proof of circulation.</summary>
        public int TimesArmed;

        /// <summary>Pellets eaten this episode - the Pet Survival score.</summary>
        public int FoodEaten;

        public bool Armed => Weapon != null;

        /// <summary>Alive and still racing - not retired in a goal.</summary>
        public bool IsActive => Alive && !Retired;

        public Racer(int index, string id, Transform transform, RacerVisual visual)
        {
            Index = index;
            Id = id;
            Transform = transform;
            Visual = visual;
        }

        /// <summary>Single transform write per step. The visual child handles its own facing.</summary>
        public void PushToTransform() => Transform.localPosition = Position;
    }
}
