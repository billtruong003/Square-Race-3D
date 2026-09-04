using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>
    /// Marks a breakable wall as a Lucky Block crate: when it breaks, the LootSystem rolls a
    /// seeded drop for whoever broke it. Data only; the BreakableWall on the same object does
    /// the hits and the removal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootCrate : MonoBehaviour
    {
    }
}
