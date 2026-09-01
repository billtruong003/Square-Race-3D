using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Pool of stylised green goo bursts. Impacts come in clusters — a ragdoll hitting the crystal
    /// wall generates several contacts in a few frames — so the particle systems are recycled
    /// rather than instantiated per hit.
    /// </summary>
    public class GooSplatterPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem splatterPrefab;
        [SerializeField] private int prewarmCount = 6;
        [SerializeField] private int maxInstances = 16;

        private readonly List<ParticleSystem> instances = new();

        private void Awake()
        {
            for (int i = 0; i < prewarmCount; i++) CreateInstance();
        }

        /// <summary>
        /// Fire a burst at a contact. <paramref name="intensity"/> is 0..1 and scales both the
        /// particle count and the size, so a light bump reads differently from a hard slam.
        /// </summary>
        public void Spawn(Vector3 position, Vector3 normal, float intensity)
        {
            var ps = GetIdleInstance();
            if (ps == null) return;

            ps.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
            ps.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, intensity);

            var emission = ps.emission;
            var burst = emission.GetBurst(0);
            burst.count = Mathf.RoundToInt(Mathf.Lerp(8f, 40f, intensity));
            emission.SetBurst(0, burst);

            ps.Play(true);
        }

        private ParticleSystem GetIdleInstance()
        {
            foreach (var ps in instances)
                if (ps != null && !ps.isPlaying) return ps;

            return instances.Count < maxInstances ? CreateInstance() : instances[0];
        }

        private ParticleSystem CreateInstance()
        {
            if (splatterPrefab == null) return null;
            var ps = Instantiate(splatterPrefab, transform);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instances.Add(ps);
            return ps;
        }
    }
}
