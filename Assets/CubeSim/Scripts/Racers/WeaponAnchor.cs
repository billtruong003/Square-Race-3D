using UnityEngine;

namespace CubeSim.Racers
{
    /// <summary>
    /// Where a held weapon sits relative to its racer.
    ///
    /// Deliberately not a hand socket. At this camera distance a realistically gripped weapon is
    /// hidden by the character and clips through it; the reference style reads far better with the
    /// weapon floating beside and above the racer as an equipped indicator. Gameplay is unaffected -
    /// melee range, ranged line of sight and damage all still come from the simulation root.
    /// </summary>
    [System.Serializable]
    public class WeaponAnchorSettings
    {
        [Tooltip("Sideways offset from the racer, as a multiple of racer size.")]
        public float sideOffset = 0.85f;

        [Tooltip("Height above the racer's feet, as a multiple of racer size.")]
        public float heightOffset = 1.45f;

        [Tooltip("Forward offset, as a multiple of racer size.")]
        public float forwardOffset = 0.15f;

        [Tooltip("Degrees per second the anchor swings around to the racer's current facing.")]
        public float followSpeed = 540f;

        [Tooltip("Bob height in metres. 0 disables the idle float.")]
        public float bobAmplitude = 0.09f;

        public float bobSpeed = 3.2f;

        [Tooltip("Degrees per second the weapon spins about its own up axis. 0 keeps it steady.")]
        public float spinSpeed = 0f;

        [Tooltip("Tilt applied to the held weapon. Zero keeps it flat, full profile to the top-down " +
                 "camera - the tilt that used to live here just made every weapon read edge-on.")]
        public Vector3 holdEuler = Vector3.zero;
    }

    /// <summary>
    /// Carries the weapon model beside the racer. Purely cosmetic: it reads the simulation position
    /// and never writes to it.
    /// </summary>
    public sealed class WeaponAnchor : MonoBehaviour
    {
        private WeaponAnchorSettings _settings;
        private float _racerSize;
        private Transform _held;
        private float _bobPhase;
        private Quaternion _facing = Quaternion.identity;

        public Transform Held => _held;

        public static WeaponAnchor Create(Transform parent, WeaponAnchorSettings settings, float racerSize, int layer)
        {
            var go = new GameObject("WeaponAnchor");
            go.transform.SetParent(parent, false);
            go.layer = layer;

            var anchor = go.AddComponent<WeaponAnchor>();
            anchor._settings = settings ?? new WeaponAnchorSettings();
            anchor._racerSize = Mathf.Max(0.1f, racerSize);
            anchor.gameObject.SetActive(false);
            return anchor;
        }

        public void Attach(GameObject weapon, float scale)
        {
            Detach();
            if (weapon == null) return;

            _held = weapon.transform;
            _held.SetParent(transform, false);

            // The prefab already carries its own orientation and offset from the library, so scale
            // multiplies rather than replaces what the library set up.
            _held.localScale *= Mathf.Max(0.05f, scale);
            _held.localRotation = Quaternion.Euler(_settings.holdEuler) * _held.localRotation;

            gameObject.SetActive(true);
        }

        public void Detach()
        {
            if (_held == null) return;

            if (Application.isPlaying) Destroy(_held.gameObject);
            else DestroyImmediate(_held.gameObject);

            _held = null;
            gameObject.SetActive(false);
        }

        /// <summary>Places the anchor for this step. Called from the visual, never from the mover.</summary>
        public void Follow(Vector3 direction, float deltaTime)
        {
            if (_held == null) return;

            if (direction.sqrMagnitude > 1e-6f)
            {
                Quaternion target = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
                _facing = Quaternion.RotateTowards(_facing, target, _settings.followSpeed * deltaTime);
            }

            Vector3 right = _facing * Vector3.right;
            Vector3 forward = _facing * Vector3.forward;

            _bobPhase += deltaTime * _settings.bobSpeed;
            float bob = _settings.bobAmplitude * Mathf.Sin(_bobPhase);

            transform.localPosition =
                right * (_settings.sideOffset * _racerSize) +
                forward * (_settings.forwardOffset * _racerSize) +
                Vector3.up * (_settings.heightOffset * _racerSize - _racerSize * 0.5f + bob);

            transform.rotation = _settings.spinSpeed > 0f
                ? Quaternion.Euler(0f, _bobPhase * _settings.spinSpeed, 0f)
                : _facing;
        }
    }
}
