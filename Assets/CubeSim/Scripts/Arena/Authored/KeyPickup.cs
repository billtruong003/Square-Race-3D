using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>The key. Any racer that touches it opens every gate with the same id.</summary>
    [DisallowMultipleComponent]
    public class KeyPickup : MonoBehaviour
    {
        [SerializeField] private string gateId = "A";
        [SerializeField] private float radius = 1.2f;
        public string GateId => gateId;
        public float Radius => Mathf.Max(0.2f, radius);
        public void Configure(string id) => gateId = id;
    }
}
