using UnityEngine;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.Arena
{
    /// <summary>
    /// One advancing wall of the shrinking playfield.
    ///
    /// The slab is an axis-aligned half space. It carries a BoxCollider on the pressure layer so the
    /// racer cast loop reflects off it exactly like a maze wall, and it also exposes the boundary
    /// analytically so the constraint solver can reason about it without any cast.
    ///
    /// It is driven by transform writes only - no Rigidbody - which is also why it passes straight
    /// through the static maze without pushing it.
    /// </summary>
    public sealed class PressureSlab
    {
        private readonly PressureSlabConfig _config;
        private readonly Transform _transform;
        private readonly BoxCollider _collider;
        private readonly float _edge;        // arena edge coordinate this slab grows from
        private readonly float _crossSize;
        private readonly float _overhang;
        private readonly float _height;
        private readonly float _groundY;

        /// <summary>0 = the boundary lies on X, 1 = on Z.</summary>
        public int Axis { get; }

        /// <summary>+1 when the playable side is above the boundary on that axis, -1 when below.</summary>
        public float InsideSign { get; }

        public float Inset { get; private set; }

        /// <summary>World coordinate of the inner face on the slab's axis.</summary>
        public float Boundary => _edge + InsideSign * Inset;

        /// <param name="bounds">
        /// The arena's real XZ extents. Taken as a parameter rather than read off ArenaDefinition,
        /// because an authored map's size lives in its prefab and the config's numbers are unused -
        /// reading them put the slabs in the middle of the arena.
        /// </param>
        public PressureSlab(PressureSlabConfig config, PressureConfig shared, Rect bounds,
            float groundY, MaterialLibrary materials, Transform parent)
        {
            _config = config;
            _overhang = Mathf.Max(0.1f, shared.overhang);
            _height = shared.height;
            _groundY = groundY;

            switch (config.side)
            {
                case PressureSide.Left:
                    Axis = 0; InsideSign = 1f; _edge = bounds.xMin; _crossSize = bounds.height; break;
                case PressureSide.Right:
                    Axis = 0; InsideSign = -1f; _edge = bounds.xMax; _crossSize = bounds.height; break;
                case PressureSide.Back:
                    Axis = 1; InsideSign = 1f; _edge = bounds.yMin; _crossSize = bounds.width; break;
                default:
                    Axis = 1; InsideSign = -1f; _edge = bounds.yMax; _crossSize = bounds.width; break;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pressure_" + config.side;
            go.layer = SimulationLayers.Pressure;
            go.GetComponent<MeshRenderer>().sharedMaterial = materials.Pressure;

            _collider = go.GetComponent<BoxCollider>();
            _transform = go.transform;
            _transform.SetParent(parent, false);

            Inset = Mathf.Max(0f, config.startInset);
            ApplyTransform();
        }

        /// <summary>Deterministic advance: position depends only on elapsed run time.</summary>
        public void Tick(float elapsedTime)
        {
            float active = elapsedTime - _config.startDelay;
            float inset = active <= 0f
                ? _config.startInset
                : _config.startInset + _config.speed * active;

            float target = Mathf.Max(_config.startInset, _config.targetInset);
            Inset = Mathf.Clamp(inset, _config.startInset, target);
            ApplyTransform();
        }

        /// <summary>
        /// The slab spans from just outside the arena edge to its current boundary. Sizing it to the
        /// inset (rather than a fixed depth reaching far past the arena) keeps it from poking outside
        /// the border wall, where it intersected the wall shell and produced torn edges.
        /// </summary>
        private void ApplyTransform()
        {
            float span = Mathf.Max(0.02f, Inset + _overhang);
            float center = Boundary - InsideSign * span * 0.5f;

            _transform.localPosition = Axis == 0
                ? new Vector3(center, _groundY + _height * 0.5f, 0f)
                : new Vector3(0f, _groundY + _height * 0.5f, center);

            _transform.localScale = Axis == 0
                ? new Vector3(span, _height, _crossSize)
                : new Vector3(_crossSize, _height, span);
        }

        /// <summary>How deep a box of the given half extent reaches into this slab. Negative = clear.</summary>
        public float Penetration(Vector3 position, float halfExtent)
        {
            float coordinate = Axis == 0 ? position.x : position.z;
            return PlanarMath.HalfSpacePenetration(coordinate, halfExtent, Boundary, InsideSign);
        }

        /// <summary>Moves a position to the legal side of this boundary. Returns the corrected value.</summary>
        public Vector3 Clamp(Vector3 position, float halfExtent, float skinWidth)
        {
            float penetration = Penetration(position, halfExtent);
            if (penetration <= 0f) return position;

            float coordinate = Axis == 0 ? position.x : position.z;
            float corrected = coordinate + InsideSign * (penetration + skinWidth);

            if (Axis == 0) position.x = corrected; else position.z = corrected;
            return position;
        }

        public Vector3 Normal => Axis == 0
            ? new Vector3(InsideSign, 0f, 0f)
            : new Vector3(0f, 0f, InsideSign);

        public void SetEnabled(bool enabled)
        {
            if (_collider != null) _collider.enabled = enabled;
            if (_transform != null) _transform.gameObject.SetActive(enabled);
        }
    }
}
