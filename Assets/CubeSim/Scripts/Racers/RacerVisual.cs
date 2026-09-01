using UnityEngine;
using CubeSim.Combat;

namespace CubeSim.Racers
{
    /// <summary>
    /// The cosmetic half of a racer: an animated model parented under the simulation root.
    ///
    /// It never moves the simulation. It only turns to face the direction the mover already chose,
    /// plays looping locomotion, fires one-shot attack and death animations, and holds the weapon
    /// model on the hand bone. Root motion is force-disabled so the animation cannot displace the
    /// simulation root.
    /// </summary>
    public sealed class RacerVisual : MonoBehaviour
    {
        public static readonly int RunState = Animator.StringToHash("Run");
        private static readonly int AttackMeleeTrigger = Animator.StringToHash("AttackMelee");
        private static readonly int AttackRangedTrigger = Animator.StringToHash("AttackRanged");
        private static readonly int DieTrigger = Animator.StringToHash("Die");
        private static readonly int CelebrateTrigger = Animator.StringToHash("Celebrate");
        private static readonly int MovingBool = Animator.StringToHash("Moving");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform model;
        [SerializeField] private Transform weaponSocket;

        [Tooltip("Degrees per second the model turns toward the movement direction.")]
        [SerializeField] private float turnSpeed = 1200f;

        private Transform _handBone;
        private WeaponAnchor _anchor;
        private bool _dead;
        private RacerTrail _trail;
        private float _deathTipTimer = -1f;
        private Quaternion _deathFromRotation;
        private Quaternion _deathToRotation;
        private Renderer[] _modelRenderers;
        private MaterialPropertyBlock _block;
        private Color _color = Color.white;

        public Transform Model => model;
        public Transform WeaponSocket => weaponSocket;
        public RacerTrail Trail => _trail;
        public WeaponAnchor Anchor => _anchor;
        public Color Color => _color;

        public void Bind(Animator boundAnimator, Transform boundModel, Transform handBone)
        {
            animator = boundAnimator;
            model = boundModel;
            _handBone = handBone;

            if (animator != null)
            {
                // The simulation root is authoritative. Root motion would fight it.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            Transform socketParent = handBone != null ? handBone : model;
            weaponSocket = new GameObject("WeaponSocket").transform;
            weaponSocket.SetParent(socketParent, false);
            weaponSocket.localPosition = Vector3.zero;
            weaponSocket.localRotation = Quaternion.identity;
        }

        public void AttachTrail(RacerTrail trail) => _trail = trail;

        public void AttachAnchor(WeaponAnchor anchor) => _anchor = anchor;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// The single source of truth for a racer's colour. Everything cosmetic - model tint, trail,
        /// and any future marker - is driven from here, so the trail can never drift out of sync.
        /// </summary>
        public void SetColor(Color color, float emission) => SetColor(color, emission, true);

        /// <summary>
        /// <paramref name="tintModel"/> false keeps the model's own texture: the racer colour then
        /// lives only in the trail, effects and UI, so a textured pet stays recognisable.
        /// </summary>
        public void SetColor(Color color, float emission, bool tintModel)
        {
            _color = color;

            if (_modelRenderers == null && model != null) _modelRenderers = model.GetComponentsInChildren<Renderer>();
            _block ??= new MaterialPropertyBlock();

            if (_modelRenderers != null && tintModel)
            {
                for (int i = 0; i < _modelRenderers.Length; i++)
                {
                    Renderer renderer = _modelRenderers[i];
                    if (renderer == null) continue;

                    renderer.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, color);
                    _block.SetColor(EmissionColorId, color * emission);
                    renderer.SetPropertyBlock(_block);
                }
            }

            _trail?.SetColor(color);
        }

        /// <summary>Feeds the trail from the simulation position. Cosmetic; never writes back.</summary>
        public void SampleTrail(Vector3 simulationPosition, float deltaTime)
            => _trail?.Sample(simulationPosition, deltaTime);

        /// <summary>Turns the model toward the current movement direction. Cosmetic only.</summary>
        public void FaceDirection(Vector3 direction, float deltaTime)
        {
            if (_dead || model == null) return;
            if (direction.sqrMagnitude < 1e-6f) return;

            Quaternion target = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
            model.rotation = Quaternion.RotateTowards(model.rotation, target, turnSpeed * deltaTime);

            _anchor?.Follow(direction, deltaTime);
        }

        public void SnapToDirection(Vector3 direction)
        {
            if (model == null || direction.sqrMagnitude < 1e-6f) return;
            model.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
        }

        public void PlayAttack(WeaponCategory category)
        {
            if (_dead || animator == null) return;
            animator.SetTrigger(category == WeaponCategory.Melee ? AttackMeleeTrigger : AttackRangedTrigger);
        }

        /// <summary>Tells the animator whether the racer is moving, so Idle can exist at all.</summary>
        public void SetMoving(bool moving)
        {
            if (_dead || animator == null) return;
            animator.SetBool(MovingBool, moving);
        }

        public void PlayCelebrate()
        {
            if (_dead || animator == null) return;
            animator.SetTrigger(CelebrateTrigger);
        }

        public void PlayDeath()
        {
            _dead = true;
            DetachWeapon();
            _trail?.Stop();

            if (animator != null) animator.SetTrigger(DieTrigger);

            // The pet pack has no death clip, so the model tips onto its side instead of just
            // freezing upright. Cosmetic - the simulation root has already stopped.
            if (model != null)
            {
                _deathFromRotation = model.rotation;
                _deathToRotation = model.rotation * Quaternion.Euler(0f, 0f, 82f);
                _deathTipTimer = 0f;
            }
        }

        private void Update()
        {
            if (_deathTipTimer < 0f || model == null) return;

            _deathTipTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_deathTipTimer / 0.45f);
            model.rotation = Quaternion.Slerp(_deathFromRotation, _deathToRotation, t * t);

            if (t >= 1f) _deathTipTimer = -1f;
        }

        /// <summary>
        /// Hands the weapon to the side anchor rather than a hand bone. At this camera distance a
        /// gripped weapon disappears behind the character and clips through it.
        /// </summary>
        public void AttachWeapon(GameObject weaponVisual, WeaponDefinition definition, float scale)
        {
            if (_anchor != null) _anchor.Attach(weaponVisual, scale);
            else if (weaponVisual != null) Destroy(weaponVisual);
        }

        public void DetachWeapon() => _anchor?.Detach();

        /// <summary>Where a ranged shot leaves the racer, in world space.</summary>
        public Vector3 MuzzleWorldPosition(Vector3 fallback)
            => _anchor != null && _anchor.Held != null ? _anchor.Held.position : fallback;
    }
}
