using System;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;

namespace CubeSim.Arena
{
    /// <summary>
    /// Maps an arenaId used in config to the prefab that holds the authored map. Adding a map means
    /// dropping its prefab in here - the episode config keeps referring to a plain string.
    /// </summary>
    [CreateAssetMenu(fileName = "AuthoredArenaLibrary", menuName = "CubeSim/Authored Arena Library", order = 2)]
    public class AuthoredArenaLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id = "Authored01";
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public GameObject Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase)) return entries[i].prefab;
            }

            return null;
        }

        public void SetEntries(List<Entry> value) => entries = value;

        /// <summary>Ids available, for error messages and editor tooling.</summary>
        public string DescribeIds()
        {
            if (entries.Count == 0) return "(none)";

            var ids = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++) ids[i] = entries[i].id;
            return string.Join(", ", ids);
        }
    }
}
