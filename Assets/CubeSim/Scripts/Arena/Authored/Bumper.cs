using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>A barrel that flings whoever touches it away, briefly at a higher speed.</summary>
    [DisallowMultipleComponent]
    public class Bumper : MonoBehaviour
    {
        [SerializeField] private float radius = 1.4f;
        [SerializeField] private float boostMultiplier = 2f;
        [SerializeField] private float boostDuration = 0.8f;
        [SerializeField] private float cooldown = 0.4f;

        public float Radius => Mathf.Max(0.2f, radius);
        public float BoostMultiplier => Mathf.Max(1f, boostMultiplier);
        public float BoostDuration => Mathf.Max(0.1f, boostDuration);
        public float Cooldown => Mathf.Max(0.05f, cooldown);

        public void Configure(float r) => radius = r;
    }
}
