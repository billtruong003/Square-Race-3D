using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The food models pellets are drawn with - real Kenney fruit and snacks instead of a tinted
    /// sphere - plus the one toon material (pack colormap) they all wear. Built by the editor
    /// from the food pack; scale and rest height are measured per model at build time so an
    /// apple and a watermelon land on the floor at the same readable size.
    /// </summary>
    [CreateAssetMenu(fileName = "FoodVisualLibrary", menuName = "CubeSim/Food Visual Library", order = 6)]
    public class FoodVisualLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public GameObject prefab;

            [Tooltip("Uniform scale that brings the model's longest side to the target size.")]
            public float scale = 1f;

            [Tooltip("Lift so the scaled model's bottom sits on the floor.")]
            public float restHeight = 0f;
        }

        [SerializeField] private Material material;
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Material Material => material;
        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(Material toon, List<Entry> value)
        {
            material = toon;
            entries = value;
        }
    }
}
