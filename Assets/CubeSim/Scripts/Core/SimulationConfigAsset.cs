using UnityEngine;

namespace CubeSim.Core
{
    /// <summary>
    /// Project-asset carrier for a <see cref="SimulationConfig"/>. Scenes reference the asset, and an
    /// automated pipeline can overwrite the asset's JSON without touching any scene or script.
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "CubeSim/Simulation Config", order = 0)]
    public class SimulationConfigAsset : ScriptableObject
    {
        [SerializeField] private SimulationConfig config = new SimulationConfig();

        public SimulationConfig Config => config;

        /// <summary>A defensive copy, so runtime mutation never dirties the asset.</summary>
        public SimulationConfig CreateRuntimeCopy() => config.Clone();

        public string ToJson(bool pretty = true) => config.ToJson(pretty);

        public void LoadFromJson(string json)
        {
            SimulationConfig parsed = SimulationConfig.FromJson(json);
            if (parsed != null) config = parsed;
        }
    }
}
