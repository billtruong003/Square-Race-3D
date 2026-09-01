using UnityEngine;
using System.Collections.Generic;

namespace CleanRender
{
    [AddComponentMenu("CleanRender/Player Foliage Interactor")]
    public class PlayerFoliageInteractor : MonoBehaviour
    {
        private const int MAX_INTERACTORS = 8;

        [Header("Interaction")]
        [Range(0.5f, 5f)] public float bendRadius = 1.5f;
        [Range(0.1f, 3f)] public float bendStrength = 1.0f;
        public bool speedScaling = true;
        [Range(1f, 3f)] public float maxSpeedRadiusMultiplier = 1.5f;
        public float maxSpeed = 10f;

        [Header("Ground Detection")]
        public float groundOffset;
        public Transform overridePoint;

        [Header("Effects")]
        public ParticleSystem grassParticles;
        public float particleSpeedThreshold = 1f;

        private Vector3 _lastPosition;
        private float _currentSpeed;
        private float _currentRadius;

        private static readonly List<PlayerFoliageInteractor> Interactors = new List<PlayerFoliageInteractor>();
        private static readonly Vector4[] ShaderData = new Vector4[MAX_INTERACTORS];
        private static int _lastPushFrame = -1;

        private static readonly int PID_Positions = Shader.PropertyToID("_InteractorPositions");
        private static readonly int PID_Count = Shader.PropertyToID("_InteractorCount");

        private void OnEnable()
        {
            if (Interactors.Count < MAX_INTERACTORS)
                Interactors.Add(this);
            _lastPosition = GetInteractionPoint();
            _currentRadius = bendRadius;
        }

        private void OnDisable()
        {
            Interactors.Remove(this);
            PushToShader();

            if (grassParticles != null && grassParticles.isPlaying)
                grassParticles.Stop();
        }

        private void Update()
        {
            Vector3 currentPos = GetInteractionPoint();

            float dt = Time.deltaTime;
            _currentSpeed = dt > 0f ? Vector3.Distance(currentPos, _lastPosition) / dt : 0f;
            _lastPosition = currentPos;

            if (speedScaling)
            {
                float speedFactor = Mathf.Clamp01(_currentSpeed / maxSpeed);
                float targetRadius = bendRadius * Mathf.Lerp(1f, maxSpeedRadiusMultiplier, speedFactor);
                _currentRadius = Mathf.Lerp(_currentRadius, targetRadius, dt * 8f);
            }
            else
            {
                _currentRadius = bendRadius;
            }

            UpdateParticles();

            if (_lastPushFrame != Time.frameCount)
            {
                _lastPushFrame = Time.frameCount;
                PushToShader();
            }
        }

        public Vector3 GetInteractionPoint()
        {
            if (overridePoint != null)
                return overridePoint.position;
            return transform.position + Vector3.down * groundOffset;
        }

        public float GetEffectiveRadius() => _currentRadius;

        public Vector4 GetShaderData()
        {
            Vector3 pos = GetInteractionPoint();
            return new Vector4(pos.x, pos.y, pos.z, _currentRadius);
        }

        private static void PushToShader()
        {
            int count = Mathf.Min(Interactors.Count, MAX_INTERACTORS);

            for (int i = 0; i < MAX_INTERACTORS; i++)
                ShaderData[i] = i < count ? Interactors[i].GetShaderData() : Vector4.zero;

            Shader.SetGlobalVectorArray(PID_Positions, ShaderData);
            Shader.SetGlobalInt(PID_Count, count);
        }

        private void UpdateParticles()
        {
            if (grassParticles == null) return;

            if (_currentSpeed > particleSpeedThreshold)
            {
                if (!grassParticles.isPlaying)
                    grassParticles.Play();
                grassParticles.transform.position = GetInteractionPoint();
            }
            else if (_currentSpeed < particleSpeedThreshold * 0.5f && grassParticles.isPlaying)
            {
                grassParticles.Stop();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 pos = Application.isPlaying ? GetInteractionPoint() : transform.position - Vector3.up * groundOffset;
            float radius = Application.isPlaying ? _currentRadius : bendRadius;

            Gizmos.color = new Color(0.3f, 0.9f, 0.2f, 0.2f);
            Gizmos.DrawSphere(pos, radius);
            Gizmos.color = new Color(0.3f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(pos, radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos, 0.1f);
        }
#endif
    }
}
