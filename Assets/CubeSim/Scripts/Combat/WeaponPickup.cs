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

        private float _rearmTimer;
        private float _ownerLockTimer;

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
            _spin.localScale = Vector3.one * Mathf.Max(0.1f, visualScale);
            WeaponVisualFactory.Create(definition, materials, _spin, visuals, WeaponVisualFactory.Context.Pickup);

            // A flat glowing pad reads far better from a top-down camera than the weapon alone.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            DestroyComponent(pad.GetComponent<Collider>());
            pad.transform.SetParent(_root, false);
            float padSize = 0.95f * Mathf.Max(0.1f, visualScale);
            pad.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            pad.transform.localScale = new Vector3(padSize, 0.03f, padSize);
            pad.GetComponent<MeshRenderer>().sharedMaterial = materials.GetWeaponMaterial(definition);

            SetPosition(position);
        }

        public void SetPosition(Vector3 position)
        {
            Position = new Vector3(position.x, _groundY, position.z);
            _root.localPosition = Position + new Vector3(0f, 0.75f, 0f);
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

            _spin.localRotation *= Quaternion.Euler(0f, 140f * deltaTime, 0f);
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

        private static void DestroyComponent(Object component)
        {
            if (component == null) return;
            if (Application.isPlaying) Object.Destroy(component);
            else Object.DestroyImmediate(component);
        }
    }
}
