using UnityEngine;

namespace CubeSim.Core
{
    /// <summary>
    /// Resolves the simulation layers once. Static arena geometry and the moving pressure slabs live
    /// on separate layers so the pressure volumes can overlap the maze without any physics
    /// interaction between them.
    /// </summary>
    public static class SimulationLayers
    {
        public const string WallLayerName = "SimWall";
        public const string PressureLayerName = "SimPressure";
        public const string RacerLayerName = "SimRacer";

        private static bool _resolved;
        private static int _wall = -1;
        private static int _pressure = -1;
        private static int _racer = -1;

        public static int Wall { get { Resolve(); return _wall; } }
        public static int Pressure { get { Resolve(); return _pressure; } }
        public static int Racer { get { Resolve(); return _racer; } }

        /// <summary>Everything a racer casts against.</summary>
        public static int BlockingMask
        {
            get
            {
                Resolve();
                int mask = 0;
                if (_wall >= 0) mask |= 1 << _wall;
                if (_pressure >= 0) mask |= 1 << _pressure;
                return mask;
            }
        }

        public static int WallMask
        {
            get { Resolve(); return _wall >= 0 ? 1 << _wall : 0; }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _wall = LayerMask.NameToLayer(WallLayerName);
            _pressure = LayerMask.NameToLayer(PressureLayerName);
            _racer = LayerMask.NameToLayer(RacerLayerName);

            if (_wall < 0 || _pressure < 0 || _racer < 0)
            {
                Debug.LogError(
                    $"[CubeSim] Missing layers. Expected '{WallLayerName}', '{PressureLayerName}' and " +
                    $"'{RacerLayerName}' in Project Settings > Tags and Layers.");
            }
        }

        /// <summary>Test hook - forces the next access to re-read the project layers.</summary>
        public static void Invalidate() => _resolved = false;
    }
}
