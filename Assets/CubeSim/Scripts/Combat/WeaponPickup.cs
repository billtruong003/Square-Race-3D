using UnityEngine;
using CubeSim.Racers;
using CubeSim.Visuals;

namespace CubeSim.Combat
{
    /// <summary>
    /// A weapon lying on the ground. Racers do not seek it - they collect it by wandering into it,
    /// which keeps the "move straight, bounce" charm intact.
    ///
    /// Ownership is temporary, so a pickup spends most of an episode cycling between available,
    /// held, and briefly locked after a drop.
    /// </summary>
    public sealed class WeaponPickup
    {
        private readonly Transform _root;
        private readonly Transform _spin;
        private readonly float _groundY;
        private readonly float _baseScale;

        private float _rearmTimer;
        private float _ownerLockTimer;
        private float _pulseTime;

        /// <summary>How hard the pickup breathes: +/-12% of size at ~1.2 beats a second.</summary>
        private const float PulseAmplitude = 0.12f;
        private const float PulseHertz = 1.2f;

        public WeaponDefinition Definition { get; }
        public Vector3 Position { get; private set; }

        /// <summary>True when nobody is holding it, whether or not it is collectable yet.</summary>
        public bool Available { get; private set; } = true;

        /// <summary>Who held it last. Blocked from re-collecting until the owner lock expires.</summary>
        public Racer LastOwner { get; private set; }

        public WeaponPickup(WeaponDefinition definition, Vector3 position, float groundY,
            MaterialLibrary materials, Transform parent, float visualScale, WeaponVisualLibrary visuals)
        {
            Definition = definition;
            _groundY = groundY;

            _root = new GameObject("Pickup_" + definition.id).transform;
            _root.SetParent(parent, false);

            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root, false);
            _baseScale = Mathf.Max(0.1f, visualScale);
            _spin.localScale = Vector3.one * _baseScale;
            WeaponVisualFactory.Create(definition, materials, _spin, visuals, WeaponVisualFactory.Context.Pickup);

            // No pad underneath: a big knife with the size pulse is all the attention it needs.
            SetPosition(position);
        }

        public void SetPosition(Vector3 position)
        {
            Position = new Vector3(position.x, _groundY, position.z);
            // Hovers above the racers (2 m) and the walls (2.8 m): a loose knife is never hidden
            // under a cube that is standing on it.
            _root.localPosition = Position + new Vector3(0f, 3.1f, 0f);
        }

        /// <summary>
        /// Returns the weapon to the world. Nobody can take it for <paramref name="rearmDelay"/>, and
        /// the racer that just lost it is locked out for longer, so ownership actually circulates
        /// instead of snapping back to the same racer on the next step.
        /// </summary>
        public void Drop(Racer previousOwner, Vector3 position, float rearmDelay, float ownerLock)
        {
            SetPosition(position);
            LastOwner = previousOwner;
            _rearmTimer = Mathf.Max(0f, rearmDelay);
            _ownerLockTimer = Mathf.Max(_rearmTimer, ownerLock);
            Available = true;
            _root.gameObject.SetActive(true);
        }

        public void Collect(Racer owner)
        {
            Available = false;
            LastOwner = owner;
            _root.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (_rearmTimer > 0f) _rearmTimer = Mathf.Max(0f, _rearmTimer - deltaTime);
            if (_ownerLockTimer > 0f) _ownerLockTimer = Mathf.Max(0f, _ownerLockTimer - deltaTime);
            if (!Available) return;

            // Spin plus a size pulse - the "come get me" heartbeat that sells a pickup top-down.
            _pulseTime += deltaTime;
            _spin.localRotation *= Quaternion.Euler(0f, 140f * deltaTime, 0f);
            float pulse = 1f + PulseAmplitude * Mathf.Sin(_pulseTime * PulseHertz * 2f * Mathf.PI);
            _spin.localScale = Vector3.one * (_baseScale * pulse);
        }

        /// <summary>Anyone may collect it once the rearm delay has passed.</summary>
        public bool CanBeCollected => Available && _rearmTimer <= 0f;

        /// <summary>The previous owner additionally has to wait out the re-pickup cooldown.</summary>
        public bool CanBeCollectedBy(Racer racer)
        {
            if (!CanBeCollected) return false;
            if (racer == LastOwner && _ownerLockTimer > 0f) return false;
            return true;
        }
    }
}
