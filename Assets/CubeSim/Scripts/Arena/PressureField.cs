using UnityEngine;

namespace CubeSim.Arena
{
    /// <summary>
    /// What the simulation needs from "pressure", whichever shape it takes: linear slabs closing in
    /// from the arena edges, or an authored route filling segment by segment.
    ///
    /// The constraint solver and the weapon drop validator talk to this, so neither knows or cares
    /// which mode an episode is running.
    /// </summary>
    public abstract class PressureField
    {
        public abstract bool Enabled { get; }

        /// <summary>Advance the field. Deterministic function of elapsed run time.</summary>
        public abstract void Tick(float elapsedTime);

        /// <summary>True when a box of this half extent is clear of every filled region.</summary>
        public abstract bool IsInsideBounds(Vector3 position, float halfExtent);

        /// <summary>Pushes a position out of any filled region. Returns the corrected position.</summary>
        public abstract Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth);

        /// <summary>Reflects a direction off whichever filled face it is currently pushing into.</summary>
        public abstract Vector3 ReflectOffBoundaries(Vector3 position, Vector3 direction,
            float halfExtent, out bool reflected);

        /// <summary>Rough still-open rectangle, used to validate weapon drops and to report progress.</summary>
        public abstract Rect CurrentBounds(Rect arenaRect);

        /// <summary>0..1 progress through the whole squeeze, for readouts.</summary>
        public virtual float Progress => 0f;

        /// <summary>Human-readable state for logs and the inspector.</summary>
        public virtual string Describe() => GetType().Name;
    }

    /// <summary>A pressure field that is switched off. Keeps every call site free of null checks.</summary>
    public sealed class NullPressureField : PressureField
    {
        public override bool Enabled => false;

        public override void Tick(float elapsedTime) { }

        public override bool IsInsideBounds(Vector3 position, float halfExtent) => true;

        public override Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth) => position;

        public override Vector3 ReflectOffBoundaries(Vector3 position, Vector3 direction,
            float halfExtent, out bool reflected)
        {
            reflected = false;
            return direction;
        }

        public override Rect CurrentBounds(Rect arenaRect) => arenaRect;

        public override string Describe() => "off";
    }
}
