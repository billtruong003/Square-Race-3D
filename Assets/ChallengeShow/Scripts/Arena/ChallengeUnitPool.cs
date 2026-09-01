using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// Keeps one gameplay instance per unit definition alive and reuses it across attempts.
    /// Recording sessions run the same 15 units over and over, so instantiate/destroy churn — and
    /// the physics rebuild that comes with it — is not worth paying repeatedly.
    /// </summary>
    public class ChallengeUnitPool : MonoBehaviour
    {
        private readonly Dictionary<ChallengeUnitDefinition, ChallengeUnit> instances = new();

        public ChallengeUnit Acquire(ChallengeUnitDefinition definition)
        {
            if (definition == null || definition.gameplayPrefab == null) return null;

            if (instances.TryGetValue(definition, out var existing) && existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = Instantiate(definition.gameplayPrefab, transform);
            go.name = definition.displayName;
            var unit = go.GetComponent<ChallengeUnit>();
            if (unit == null)
            {
                Debug.LogError($"[ChallengeShow] Gameplay prefab for {definition.displayName} has no ChallengeUnit.", go);
                Destroy(go);
                return null;
            }

            unit.SetDefinition(definition);
            instances[definition] = unit;
            return unit;
        }

        public void Release(ChallengeUnit unit)
        {
            if (unit == null) return;
            unit.gameObject.SetActive(false);
        }

        public void ReleaseAll()
        {
            foreach (var kvp in instances)
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
        }
    }
}
