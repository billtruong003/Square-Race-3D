using UnityEngine;

namespace CubeSim.Arena.Authored
{
    /// <summary>Where racers start. Multiple areas are used round robin.</summary>
    public class SpawnArea : ArenaRegion
    {
        protected override Color GizmoColor => new Color(0.3f, 0.8f, 1f, 0.9f);
    }
}
