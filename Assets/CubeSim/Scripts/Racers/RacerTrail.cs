using UnityEngine;

namespace CubeSim.Racers
{
    /// <summary>
    /// A tapered ribbon trailing the racer, built from the simulation's own position history.
    ///
    /// Deliberately not a TrailRenderer: the project's screen-space outline detects edges from the
    /// Depth and DepthNormals buffers, and URP transparents write neither, so a transparent trail
    /// would silently receive no outline. This is an opaque MeshRenderer using the same ToonLit
    /// material family as everything else, which has DepthOnly and DepthNormals passes and is picked
    /// up by the outline's mask pass through UniversalForward. No separate outline path exists.
    ///
    /// It samples <see cref="Racer.Position"/>, never a bone, and never writes back to the racer, so
    /// it cannot affect collision, targeting, pressure or determinism.
    /// </summary>
    public sealed class RacerTrail : MonoBehaviour
    {
        private Vector3[] _points;
        private float[] _widths;
        private int _count;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uv;
        private int[] _triangles;

        private float _width;
        private float _minPointDistance;
        private float _heightOffset;
        private float _lifetime;
        private int _capacity;
        private bool _ready;

        private Vector3 _lastSample;

        // Death cleanup state.
        private TrailSettings _settings;
        private bool _dying;
        private float _deathTimer;
        private float _retractProgress;
        private float _fadeProgress;
        private Color _color;

        // Root cap.
        private Transform _cap;
        private MeshRenderer _capRenderer;

        public static RacerTrail Create(Transform parent, TrailSettings settings, Material material,
            Color color, float racerSize, int layer)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            go.layer = layer;

            var trail = go.AddComponent<RacerTrail>();
            trail.Initialise(settings, material, color, racerSize);
            return trail;
        }

        private void Initialise(TrailSettings settings, Material material, Color color, float racerSize)
        {
            _settings = settings;
            _width = settings.baseWidth > 0f
                ? settings.baseWidth
                : Mathf.Max(0.02f, settings.width * racerSize);
            _minPointDistance = Mathf.Max(0.02f, settings.minPointDistance);
            _heightOffset = settings.heightOffset;
            _lifetime = Mathf.Max(0f, settings.lifetime);

            // Length is a distance; capacity is how many samples that needs at the sample spacing.
            _capacity = Mathf.Clamp(Mathf.CeilToInt(settings.length / _minPointDistance) + 2, 4, 128);

            _points = new Vector3[_capacity];
            _widths = new float[_capacity];

            _vertices = new Vector3[_capacity * 2];
            _normals = new Vector3[_capacity * 2];
            _uv = new Vector2[_capacity * 2];
            _triangles = new int[(_capacity - 1) * 6];

            _mesh = new Mesh { name = "RacerTrail" };
            _mesh.MarkDynamic();

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _block = new MaterialPropertyBlock();

            if (settings.rootCapEnabled) BuildRootCap(settings, material);

            SetColor(color);

            // Vertices are written in world space, so the object must not add a transform of its own.
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            _ready = true;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// The disc that sits under the feet. Without it the ribbon ends in a hard straight edge
        /// against the character and the seam reads as a cut.
        /// </summary>
        private void BuildRootCap(TrailSettings settings, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "TrailRootCap";
            go.layer = gameObject.layer;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                // Purely visual - a collider here would show up in the movement casts.
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            _cap = go.transform;
            _cap.SetParent(transform, false);

            float radius = Mathf.Max(0.05f, _width * 0.5f * settings.rootCapRadius);
            _cap.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            _capRenderer = go.GetComponent<MeshRenderer>();

            // Same opaque material family as the ribbon, so the cap flows through the existing
            // screen-space outline pass rather than needing one of its own.
            _capRenderer.sharedMaterial = material;
            _capRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _capRenderer.receiveShadows = false;
        }

        /// <summary>Trail colour is pushed from RacerVisual, never configured independently.</summary>
        public void SetColor(Color color)
        {
            _color = color;
            ApplyColor(color, 1f);
        }

        private void ApplyColor(Color color, float intensity)
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * (0.35f * intensity));
                _renderer.SetPropertyBlock(_block);
            }

            if (_capRenderer != null)
            {
                _capRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * (0.5f * intensity));
                _capRenderer.SetPropertyBlock(_block);
            }
        }

        public void SetEnabled(bool value)
        {
            if (_renderer != null) _renderer.enabled = value;
        }

        /// <summary>Records a new simulation position and rebuilds the ribbon.</summary>
        public void Sample(Vector3 position, float deltaTime)
        {
            if (!_ready) return;

            position.y = _heightOffset;

            if (_count == 0)
            {
                Push(position);
                _lastSample = position;
                return;
            }

            if ((position - _lastSample).sqrMagnitude >= _minPointDistance * _minPointDistance)
            {
                Push(position);
                _lastSample = position;
            }
            else
            {
                // Keep the head glued to the racer between samples so the ribbon never lags visibly.
                _points[0] = position;
            }

            if (_lifetime > 0f) Age(deltaTime);

            // The cap rides the head so the seam under the feet stays covered.
            if (_cap != null)
            {
                float capY = _settings != null ? _settings.rootCapHeightOffset : _heightOffset + 0.01f;
                _cap.position = new Vector3(position.x, capY, position.z);
            }

            Rebuild();
        }

        /// <summary>
        /// Stops recording and starts the disappear sequence. A dead trail must not sit on the map
        /// for the rest of the episode.
        /// </summary>
        public void Stop()
        {
            if (_dying) return;

            _ready = false;
            _dying = true;
            _deathTimer = 0f;

            if (_cap != null) _cap.gameObject.SetActive(false);
        }

        /// <summary>Drives the death sequence. Cosmetic; never touches simulation state.</summary>
        private void Update()
        {
            if (!_dying) return;

            _deathTimer += Time.deltaTime;

            TrailDisappearMode mode = _settings != null
                ? _settings.disappearMode
                : TrailDisappearMode.RetractAndFade;

            float retractDuration = Mathf.Max(0.01f, _settings != null ? _settings.deathRetractDuration : 0.5f);
            float fadeDuration = Mathf.Max(0.01f, _settings != null ? _settings.deathFadeDuration : 0.4f);

            bool retracts = mode == TrailDisappearMode.Retract || mode == TrailDisappearMode.RetractAndFade;
            bool shrinks = mode == TrailDisappearMode.Shrink;
            bool fades = mode == TrailDisappearMode.Fade || mode == TrailDisappearMode.RetractAndFade;

            _retractProgress = retracts || shrinks ? Mathf.Clamp01(_deathTimer / retractDuration) : 0f;
            _fadeProgress = fades ? Mathf.Clamp01(_deathTimer / fadeDuration) : 0f;

            if (fades) ApplyColor(Color.Lerp(_color, _color * 0.2f, _fadeProgress), 1f - _fadeProgress);

            if (retracts)
            {
                // Eat the tail toward the head, so the ribbon reads as being pulled in.
                int target = Mathf.RoundToInt(_count * (1f - _retractProgress));
                while (_count > target && _count > 0) _count--;
            }

            Rebuild();

            float longest = Mathf.Max(
                retracts || shrinks ? retractDuration : 0f,
                fades ? fadeDuration : 0f);

            if (_deathTimer < longest) return;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void Push(Vector3 position)
        {
            int limit = Mathf.Min(_count, _capacity - 1);
            for (int i = limit; i > 0; i--)
            {
                _points[i] = _points[i - 1];
                _widths[i] = _widths[i - 1];
            }

            _points[0] = position;
            _widths[0] = 0f;
            _count = Mathf.Min(_count + 1, _capacity);
        }

        private void Age(float deltaTime)
        {
            float step = deltaTime / _lifetime;
            for (int i = 0; i < _count; i++) _widths[i] += step;

            while (_count > 2 && _widths[_count - 1] >= 1f) _count--;
        }

        /// <summary>
        /// Builds the ribbon. The half-width shrinks toward the tail, and the miter at each joint is
        /// clamped so a hard reflection produces a clean corner instead of a long spike.
        /// </summary>
        private void Rebuild()
        {
            if (_count < 2)
            {
                _mesh.Clear();
                return;
            }

            for (int i = 0; i < _count; i++)
            {
                Vector3 forward = SegmentDirection(i);
                var side = new Vector3(forward.z, 0f, -forward.x);

                float taper = 1f - (float)i / (_count - 1);
                if (_lifetime > 0f) taper *= Mathf.Clamp01(1f - _widths[i]);

                // Shrink mode collapses the ribbon in place instead of eating the tail.
                if (_dying && _settings != null && _settings.disappearMode == TrailDisappearMode.Shrink)
                {
                    taper *= 1f - _retractProgress;
                }

                // Ease the taper so the head stays wide and the tail thins quickly - the reference
                // trail is a wedge, not a linear triangle.
                float halfWidth = _width * 0.5f * taper * taper;

                Vector3 p = _points[i];
                _vertices[i * 2] = p + side * halfWidth;
                _vertices[i * 2 + 1] = p - side * halfWidth;

                _normals[i * 2] = Vector3.up;
                _normals[i * 2 + 1] = Vector3.up;

                float v = (float)i / (_count - 1);
                _uv[i * 2] = new Vector2(0f, v);
                _uv[i * 2 + 1] = new Vector2(1f, v);
            }

            int quads = _count - 1;
            for (int i = 0; i < quads; i++)
            {
                int t = i * 6;
                int a = i * 2;

                _triangles[t] = a;
                _triangles[t + 1] = a + 2;
                _triangles[t + 2] = a + 1;

                _triangles[t + 3] = a + 1;
                _triangles[t + 4] = a + 2;
                _triangles[t + 5] = a + 3;
            }

            // Degenerate the unused tail triangles rather than resizing the arrays every frame.
            for (int i = quads; i < _capacity - 1; i++)
            {
                int t = i * 6;
                for (int k = 0; k < 6; k++) _triangles[t + k] = 0;
            }

            for (int i = _count * 2; i < _vertices.Length; i++) _vertices[i] = _points[_count - 1];

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uv;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// Direction used to offset the ribbon at point i. Averaging the neighbouring segments and
        /// falling back to a single segment at a sharp corner is what stops reflections from
        /// producing a bow-tie.
        /// </summary>
        private Vector3 SegmentDirection(int i)
        {
            Vector3 incoming = i > 0 ? _points[i - 1] - _points[i] : _points[i] - _points[i + 1];
            Vector3 outgoing = i < _count - 1 ? _points[i] - _points[i + 1] : incoming;

            incoming.y = 0f;
            outgoing.y = 0f;

            if (incoming.sqrMagnitude < 1e-8f) incoming = outgoing;
            if (outgoing.sqrMagnitude < 1e-8f) outgoing = incoming;
            if (incoming.sqrMagnitude < 1e-8f) return Vector3.forward;

            incoming.Normalize();
            outgoing.Normalize();

            // A near-reversal means a bounce: mitering across it would fold the ribbon.
            if (Vector3.Dot(incoming, outgoing) < 0.2f) return incoming;

            Vector3 average = incoming + outgoing;
            return average.sqrMagnitude < 1e-8f ? incoming : average.normalized;
        }

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }

    /// <summary>How a trail leaves the scene once its racer dies.</summary>
    public enum TrailDisappearMode
    {
        /// <summary>The tail is eaten toward the head, so the ribbon appears to be pulled in.</summary>
        Retract = 0,

        /// <summary>Width collapses to nothing in place.</summary>
        Shrink = 1,

        /// <summary>Colour fades to the floor and the renderer switches off.</summary>
        Fade = 2,

        /// <summary>Retract and fade together - the default, and the most readable.</summary>
        RetractAndFade = 3
    }

    /// <summary>Trail tuning, carried in the racer config so it stays data driven.</summary>
    [System.Serializable]
    public class TrailSettings
    {
        public bool enabled = true;

        [Tooltip("Trail length in metres.")]
        public float length = 3.2f;

        [Tooltip("Width at the head, as a multiple of the racer size. Ignored when baseWidth is set.")]
        public float width = 0.85f;

        [Tooltip("Absolute head width in metres. 0 = derive from width * racer size.")]
        public float baseWidth = 0f;

        [Tooltip("Minimum distance between recorded points. Smaller = smoother, more vertices.")]
        public float minPointDistance = 0.25f;

        [Tooltip("Height above the floor. Keeps the ribbon off the floor plane so it cannot z-fight.")]
        public float heightOffset = 0.06f;

        [Tooltip("Seconds for a point to fade out. 0 = length alone controls the trail.")]
        public float lifetime = 0f;

        [Header("Death cleanup")]
        public TrailDisappearMode disappearMode = TrailDisappearMode.RetractAndFade;

        [Tooltip("Seconds for the trail to retract or shrink away after its racer dies.")]
        public float deathRetractDuration = 0.55f;

        [Tooltip("Seconds for the trail to fade out after its racer dies.")]
        public float deathFadeDuration = 0.45f;

        [Header("Root cap")]
        [Tooltip("Small disc under the feet that hides the seam where the ribbon starts.")]
        public bool rootCapEnabled = true;

        [Tooltip("Cap radius as a multiple of the trail head half-width.")]
        public float rootCapRadius = 1.15f;

        [Tooltip("Height of the cap above the floor. Slightly above the ribbon avoids co-planar faces.")]
        public float rootCapHeightOffset = 0.075f;

        [Tooltip("Draw the cap as opaque so the screen-space outline picks it up with everything else.")]
        public bool rootCapUsesOutline = true;
    }
}
