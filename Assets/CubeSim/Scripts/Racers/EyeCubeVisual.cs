using UnityEngine;

namespace CubeSim.Racers
{
    /// <summary>
    /// The eye layer of the cube racer. The user-authored cube (Assets/Eyes) carries a plane on
    /// its top face with an eye texture; this component swaps that texture so the pupils always
    /// look where the racer is going - four directional looks plus a centered idle stare. The cube
    /// itself never yaws (classic square-race style): direction is told entirely through the eyes.
    ///
    /// The body tint comes through RacerVisual's property-block pass like any model; the eye plane
    /// stamps its own block (texture + white) on every change, so the racer colour never bleeds
    /// into the whites of the eyes. Cosmetic only - reads direction, writes nothing back.
    /// </summary>
    public sealed class EyeCubeVisual : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer eyeRenderer;

        [Tooltip("Idle stare, pupils centered.")]
        [SerializeField] private Texture2D center;

        [Tooltip("Pupils toward +X,+Z / -X,+Z / +X,-Z / -X,-Z in world space.")]
        [SerializeField] private Texture2D lookNorthEast;
        [SerializeField] private Texture2D lookNorthWest;
        [SerializeField] private Texture2D lookSouthEast;
        [SerializeField] private Texture2D lookSouthWest;

        [SerializeField] private Texture2D hurt;
        [SerializeField] private Texture2D dead;
        [SerializeField, Min(0.05f)] private float hurtSeconds = 0.35f;

        /// <summary>Quadrant flips need to stick briefly, or near-axis movement strobes the eyes.</summary>
        private const float MinSwitchInterval = 0.12f;

        private MaterialPropertyBlock _block;
        private Texture2D _current;
        private float _lastSwitchTime;
        private bool _moving;
        private bool _dead;
        private float _hurtUntil;
        private Texture2D _look;

        public bool IsEyeRenderer(Renderer candidate) => candidate == eyeRenderer;

        public void Configure(Renderer eye, Texture2D idle,
            Texture2D northEast, Texture2D northWest, Texture2D southEast, Texture2D southWest,
            Texture2D hurtTexture = null, Texture2D deadTexture = null)
        {
            eyeRenderer = eye;
            center = idle;
            lookNorthEast = northEast;
            lookNorthWest = northWest;
            lookSouthEast = southEast;
            lookSouthWest = southWest;
            hurt = hurtTexture;
            dead = deadTexture;
        }

        private void OnEnable()
        {
            _dead = false;
            _hurtUntil = 0f;
            _moving = false;
            _look = center;
            _current = null;
            Apply(center, true);
        }

        public void ShowHurt()
        {
            if (_dead) return;
            _hurtUntil = Time.time + hurtSeconds;
            Apply(hurt, true);
        }

        public void ShowDead()
        {
            _dead = true;
            _hurtUntil = 0f;
            Apply(dead, true);
        }

        private void Update()
        {
            if (_dead || _hurtUntil <= 0f || Time.time < _hurtUntil) return;
            _hurtUntil = 0f;
            Apply(_moving ? _look : center, true);
        }

        /// <summary>Points the pupils along the movement direction. Called every visual frame.</summary>
        public void Look(Vector3 direction)
        {
            _moving = direction.sqrMagnitude > 1e-6f;
            if (!_moving) return;

            Texture2D target = direction.x >= 0f
                ? direction.z >= 0f ? lookNorthEast : lookSouthEast
                : direction.z >= 0f ? lookNorthWest : lookSouthWest;

            _look = target;
            if (!_dead && Time.time >= _hurtUntil) Apply(target, false);
        }

        /// <summary>Standing still returns the pupils to the centered stare.</summary>
        public void SetMoving(bool moving)
        {
            _moving = moving;
            if (!moving && !_dead && Time.time >= _hurtUntil) Apply(center, false);
        }

        private void Apply(Texture2D texture, bool force)
        {
            if (eyeRenderer == null || texture == null || texture == _current) return;
            if (!force && Time.time - _lastSwitchTime < MinSwitchInterval) return;

            _block ??= new MaterialPropertyBlock();
            eyeRenderer.GetPropertyBlock(_block);
            _block.SetTexture(BaseMapId, texture);
            // The tint pass paints every renderer under the model; white here keeps the eyes out.
            _block.SetColor(BaseColorId, Color.white);
            eyeRenderer.SetPropertyBlock(_block);

            _current = texture;
            _lastSwitchTime = Time.time;
        }
    }
}
