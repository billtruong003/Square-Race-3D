using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>A coin. Taken on touch, back after the delay - the Coin Rush economy.</summary>
    [DisallowMultipleComponent]
    public class CoinPickup : MonoBehaviour
    {
        [SerializeField] private int value = 1;
        [SerializeField] private float respawnDelay = 8f;
        [SerializeField] private float radius = 1.1f;
        public int Value => Mathf.Max(1, value);
        public float RespawnDelay => respawnDelay;
        public float Radius => Mathf.Max(0.2f, radius);
        public void Configure(int coinValue, float respawnSeconds)
        {
            value = coinValue;
            respawnDelay = respawnSeconds;
        }
    }
}
