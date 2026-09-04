using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>One heart back. Ignored by a racer at full health, so it waits for someone who needs it.</summary>
    [DisallowMultipleComponent]
    public class PotionPickup : MonoBehaviour
    {
        [SerializeField] private float heal = 1f;
        [SerializeField] private float respawnDelay = 20f;
        [SerializeField] private float radius = 1.2f;
        public float Heal => Mathf.Max(0f, heal);
        public float RespawnDelay => respawnDelay;
        public float Radius => Mathf.Max(0.2f, radius);
    }
}
