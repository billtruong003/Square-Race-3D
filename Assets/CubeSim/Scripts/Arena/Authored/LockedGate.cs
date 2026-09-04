using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>A wall that drops into the floor when its key is taken. Data only.</summary>
    [RequireComponent(typeof(ArenaWall))]
    [DisallowMultipleComponent]
    public class LockedGate : MonoBehaviour
    {
        [SerializeField] private string gateId = "A";
        public string GateId => gateId;
        public void Configure(string id) => gateId = id;
    }
}
