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
        private EyeCubeVisual _eyes;
        private bool _dead;
        private RacerTrail _trail;
        private float _deathTipTimer = -1f;
        private Quaternion _deathFromRotation;
        private Quaternion _deathToRotation;
        private Renderer[] _modelRenderers;
        private MaterialPropertyBlock _block;
        private Color _color = Color.white;
        private float _emission;
        private float _flashLeft = -1f;
        private float _launchLeft = -1f;
        private Vector3 _launchDir;
        private float _launchMetres;
        private Vector3 _modelBasePos;
        private Quaternion _modelBaseRot;
        private const float LaunchSeconds = 0.32f;

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
            _eyes = boundModel != null ? boundModel.GetComponentInChildren<EyeCubeVisual>() : null;

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
            _emission = emission;
            Tint(color, emission, tintModel);
            _trail?.SetColor(color);
        }

        private void Tint(Color color, float emission, bool tintModel)
        {

            if (_modelRenderers == null && model != null) _modelRenderers = model.GetComponentsInChildren<Renderer>();
            _block ??= new MaterialPropertyBlock();

            if (_modelRenderers != null && tintModel)
            {
                for (int i = 0; i < _modelRenderers.Length; i++)
                {
                    Renderer renderer = _modelRenderers[i];
                    if (renderer == null || (_eyes != null && _eyes.IsEyeRenderer(renderer))) continue;

                    renderer.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, color);
                    _block.SetColor(EmissionColorId, color * emission);
                    renderer.SetPropertyBlock(_block);
                }
            }
        }

        /// <summary>A white hit flash: the cube blinks for a few frames, then its colour returns.</summary>
        public void Flash(float seconds)
        {
            if (_dead) return;
            _flashLeft = seconds;
            Tint(Color.white, 3f, true);
        }

        /// <summary>
        /// A knockback pop: the model hops and tumbles along the hit direction for a third of a
        /// second while the simulation slides the body. Cosmetic; the sim position is untouched.
        /// </summary>
        public void Launch(Vector3 direction, float metres)
        {
            if (_dead || model == null) return;
            if (_launchLeft < 0f) { _modelBasePos = model.localPosition; _modelBaseRot = model.localRotation; }
            _launchLeft = LaunchSeconds;
            _launchDir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            _launchMetres = metres;
        }

        private void TickJuice(float deltaTime)
        {
            if (_flashLeft >= 0f)
            {
                _flashLeft -= deltaTime;
                if (_flashLeft < 0f) Tint(_color, _emission, true);
            }

            if (_launchLeft >= 0f && model != null)
            {
                _launchLeft -= deltaTime;
                if (_launchLeft < 0f)
                {
                    model.localPosition = _modelBasePos;
                    model.localRotation = _modelBaseRot;
                }
                else
                {
                    float t = 1f - _launchLeft / LaunchSeconds;
                    float height = Mathf.Clamp(_launchMetres * 0.22f, 0.35f, 2.2f) * Mathf.Sin(t * Mathf.PI);
                    // Big shoves tumble a full turn; small ones just rock.
                    float angle = _launchMetres >= 5f ? 360f * t : 35f * Mathf.Sin(t * Mathf.PI);
                    Vector3 axis = Vector3.Cross(Vector3.up, _launchDir);
                    Vector3 localAxis = model.parent != null ? model.parent.InverseTransformDirection(axis) : axis;
                    model.localPosition = _modelBasePos + Vector3.up * height;
                    model.localRotation = Quaternion.AngleAxis(angle, localAxis) * _modelBaseRot;
                }
            }
        }

        /// <summary>Feeds the trail from the simulation position. Cosmetic; never writes back.</summary>
        public void SampleTrail(Vector3 simulationPosition, float deltaTime)
            => _trail?.Sample(simulationPosition, deltaTime);

        /// <summary>
        /// Turns the model toward the current movement direction. The eye cube never yaws -
        /// direction is told through the pupils instead, square-race style. Cosmetic only.
        /// </summary>
        public void FaceDirection(Vector3 direction, float deltaTime)
        {
            if (_dead || model == null) return;
            if (direction.sqrMagnitude < 1e-6f) return;

            if (_eyes != null)
            {
                _eyes.Look(direction);
            }
            else
            {
                Quaternion target = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
                model.rotation = Quaternion.RotateTowards(model.rotation, target, turnSpeed * deltaTime);
            }

            _anchor?.Follow(direction, deltaTime);
        }

        public void SnapToDirection(Vector3 direction)
        {
            if (model == null || direction.sqrMagnitude < 1e-6f) return;

            if (_eyes != null)
            {
                _eyes.Look(direction);
                return;
            }

            model.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
        }

        public void PlayAttack(WeaponCategory category)
        {
            if (_dead || animator == null) return;
            animator.SetTrigger(category == WeaponCategory.Melee ? AttackMeleeTrigger : AttackRangedTrigger);
        }

        /// <summary>Tells the animator (or the eyes) whether the racer is moving, so Idle exists.</summary>
        public void SetMoving(bool moving)
        {
            if (_dead) return;

            _eyes?.SetMoving(moving);
            if (animator != null) animator.SetBool(MovingBool, moving);
        }

        public void PlayCelebrate()
        {
            if (_dead) return;

            // A finisher stops moving for good, so its ribbon leaves with it - a frozen trail
            // parked on the course for the rest of the round just reads as dirt.
            _trail?.Stop();

            if (animator != null) animator.SetTrigger(CelebrateTrigger);
        }

        public void PlayHurt()
        {
            if (!_dead) _eyes?.ShowHurt();
        }

        public void PlayDeath()
        {
            _dead = true;
            _eyes?.ShowDead();
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
            TickJuice(Time.deltaTime);
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
