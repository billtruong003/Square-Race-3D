using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Maps a visual id used in config ("Skeleton", "Cube") to the actual model asset. Keeping the
    /// asset references here means an episode config stays a plain string and never has to name a
    /// GUID or a scene object.
    /// </summary>
    [CreateAssetMenu(fileName = "RacerVisualLibrary", menuName = "CubeSim/Racer Visual Library", order = 1)]
    public class RacerVisualLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id = "Skeleton";

            [Tooltip("Model prefab. Left empty, the racer falls back to a coloured cube.")]
            public GameObject prefab;

            [Tooltip("Animator controller applied to the spawned instance. The source prefab is never modified.")]
            public RuntimeAnimatorController animatorController;

            [Tooltip("Model height in metres at scale 1, used to fit the model to the racer size.")]
            public float nativeHeight = 1.88f;

            [Tooltip("Multiplies the fitted scale. 1 = model height equals racerSize * visualHeightRatio.")]
            public float scaleMultiplier = 1f;

            [Tooltip("Vertical offset applied to the model inside the simulation root.")]
            public float yOffset = 0f;

            [Tooltip("Face shot shown on the leaderboard. Empty falls back to a colour swatch.")]
            public Sprite portrait;

            [Tooltip("Name the UI shouts for this model. Empty derives from the id.")]
            public string displayName = "";

            [Tooltip("Bone the weapon is attached to. Empty uses the humanoid right hand.")]
            public string handBoneName = "";
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase)) return entries[i];
            }

            return null;
        }

        /// <summary>
        /// Per-racer lookup. The special id "Pets" deals the Pet_ roster out round-robin by racer
        /// index, so ten racers become ten different creatures - deterministically, no RNG.
        /// </summary>
        public Entry FindForIndex(string id, int index)
        {
            if (!string.Equals(id, "Pets", StringComparison.OrdinalIgnoreCase)) return Find(id);

            var pets = new List<Entry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id != null && entries[i].id.StartsWith("Pet_", StringComparison.OrdinalIgnoreCase))
                {
                    pets.Add(entries[i]);
                }
            }

            if (pets.Count == 0) return Find("Fox");
            return pets[Mathf.Abs(index) % pets.Count];
        }

        public void SetEntries(List<Entry> value) => entries = value;
    }
}
