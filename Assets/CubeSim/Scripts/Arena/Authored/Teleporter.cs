using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>Half of a teleporter pair: entering one pad places the racer on its twin.</summary>
    public class Teleporter : ArenaRegion
    {
        [SerializeField] private string pairId = "1";
        [SerializeField] private float cooldown = 1.5f;
        public string PairId => pairId;
        public float Cooldown => Mathf.Max(0.2f, cooldown);
        public void Configure(string id) => pairId = id;
        protected override Color GizmoColor => new Color(0.6f, 0.3f, 1f, 0.9f);
    }
}
