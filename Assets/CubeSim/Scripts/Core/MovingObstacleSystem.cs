using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;

namespace CubeSim.Core
{
    /// <summary>
    /// Drives the map's moving parts: cycling doors that slide open and shut, and rotor crosses
    /// that sweep their room. Every pose is a pure function of elapsed run time - no state, no
    /// randomness - so a seed's obstacle choreography replays exactly.
    ///
    /// Runs at the top of the simulation step, before movement, and syncs transforms so the
    /// racers' casts see the obstacles where they actually are this step. A racer caught by a
    /// closing door is depenetrated by the constraint solver, or crushed if there is nowhere to
    /// go - the same rules the pressure already plays by.
    /// </summary>
    public sealed class MovingObstacleSystem
    {
        private sealed class Door
        {
            public CyclingWall Wall;
            public Transform Transform;
            public Collider Collider;
            public Vector3 ClosedScale;
            public Vector3 ClosedPosition;
        }

        private sealed class Rotor
        {
            public RotorObstacle Obstacle;
            public Transform Transform;
            public Transform[] Bars;
        }

        private readonly List<Door> _doors = new List<Door>();
        private readonly List<Rotor> _rotors = new List<Rotor>();

        public int DoorCount => _doors.Count;
        public int RotorCount => _rotors.Count;
        public bool Any => _doors.Count > 0 || _rotors.Count > 0;

        public MovingObstacleSystem(ArenaRuntime arena)
        {
            if (arena.Authored == null) return;

            foreach (CyclingWall wall in arena.Authored.GetComponentsInChildren<CyclingWall>(true))
            {
                var collider = wall.GetComponent<Collider>();
                if (collider == null) continue;

                _doors.Add(new Door
                {
                    Wall = wall,
                    Transform = wall.transform,
                    Collider = collider,
                    ClosedScale = wall.transform.localScale,
                    ClosedPosition = wall.transform.localPosition,
                });
            }

            foreach (RotorObstacle rotor in arena.Authored.GetComponentsInChildren<RotorObstacle>(true))
            {
                Collider[] bars = rotor.GetComponentsInChildren<Collider>(true);
                var barTransforms = new Transform[bars.Length];
                for (int b = 0; b < bars.Length; b++) barTransforms[b] = bars[b].transform;

                _rotors.Add(new Rotor { Obstacle = rotor, Transform = rotor.transform, Bars = barTransforms });
            }
        }

        /// <summary>
        /// The first cutting rotor whose bar overlaps a racer disc, or null. Pure geometry against
        /// the posed bar transforms (unit cubes under a non-uniform scale), so it is exactly as
        /// deterministic as the pose itself and never touches the physics scene.
        /// </summary>
        public RotorObstacle FindCuttingRotor(Vector3 position, float radius)
        {
            for (int i = 0; i < _rotors.Count; i++)
            {
                Rotor rotor = _rotors[i];
                if (rotor.Obstacle.DamagePerHit <= 0f) continue;

                for (int b = 0; b < rotor.Bars.Length; b++)
                {
                    Transform bar = rotor.Bars[b];
                    Vector3 local = bar.InverseTransformPoint(position);
                    Vector3 scale = bar.lossyScale;
                    float rx = radius / Mathf.Max(0.001f, Mathf.Abs(scale.x));
                    float rz = radius / Mathf.Max(0.001f, Mathf.Abs(scale.z));
                    if (Mathf.Abs(local.x) <= 0.5f + rx && Mathf.Abs(local.z) <= 0.5f + rz) return rotor.Obstacle;
                }
            }

            return null;
        }

        /// <summary>Poses every obstacle for this step's time. Deterministic by construction.</summary>
        public void Step(float elapsedTime)
        {
            if (!Any) return;

            for (int i = 0; i < _doors.Count; i++)
            {
                Door door = _doors[i];
                float openness = Openness(door.Wall, elapsedTime);

                // Slide down into the floor as it opens; collider drops out early so a nearly-open
                // door never clips a racer that is already committed to the gap.
                float height = Mathf.Lerp(door.ClosedScale.y, door.ClosedScale.y * 0.04f, openness);
                door.Transform.localScale = new Vector3(door.ClosedScale.x, height, door.ClosedScale.z);
                door.Transform.localPosition = door.ClosedPosition -
                    new Vector3(0f, (door.ClosedScale.y - height) * 0.5f, 0f);

                door.Collider.enabled = openness < 0.6f;
            }

            for (int i = 0; i < _rotors.Count; i++)
            {
                Rotor rotor = _rotors[i];
                float angle = rotor.Obstacle.PhaseDegrees + rotor.Obstacle.DegreesPerSecond * elapsedTime;
                rotor.Transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }

            // The movement casts run against the physics scene, not the transforms - without this
            // sync they would see every obstacle one step in the past.
            Physics.SyncTransforms();
        }

        /// <summary>0 = fully closed, 1 = fully open, easing through the slide.</summary>
        private static float Openness(CyclingWall wall, float time)
        {
            float slide = wall.SlideDuration;
            float cycle = wall.OpenDuration + wall.ClosedDuration + slide * 2f;
            float t = Mathf.Repeat(time + wall.PhaseOffset, cycle);

            if (t < wall.ClosedDuration) return 0f;
            t -= wall.ClosedDuration;

            if (t < slide) return t / slide;
            t -= slide;

            if (t < wall.OpenDuration) return 1f;
            t -= wall.OpenDuration;

            return 1f - t / slide;
        }
    }
}
