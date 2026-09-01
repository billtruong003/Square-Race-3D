using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// A family island's roster. Instantiates one non-simulated display copy of each family member
    /// onto its slots and hides that copy while the unit is out competing.
    ///
    /// Display copies are inert: no Rigidbody, no ChallengeUnit, no physics. Simulation state lives
    /// entirely on the gameplay instance the director spawns, so the two never fight over the unit.
    /// </summary>
    public class MonsterFamilyDisplay : MonoBehaviour
    {
        [SerializeField] private MonsterFamilyDefinition family;
        [SerializeField] private Transform slotRoot;
        [SerializeField] private TextMeshPro familyLabel;
        [SerializeField] private Transform labelAnchor;

        [Header("Layout")]
        [Tooltip("Metres between display slots. Widened automatically for large families.")]
        [SerializeField] private float slotSpacing = 3.2f;
        [Tooltip("Display units face this direction in island local space.")]
        [SerializeField] private Vector3 displayFacing = Vector3.back;

        private readonly Dictionary<ChallengeUnitDefinition, GameObject> displayInstances = new();
        private readonly Dictionary<ChallengeUnitDefinition, TextMeshPro> absentTags = new();

        public MonsterFamilyDefinition Family => family;

        private void Awake() => CacheExistingDisplays();

        /// <summary>
        /// Display copies are authored into the scene by the island generator, so at runtime we
        /// only need to find them again rather than instantiate anything.
        /// </summary>
        private void CacheExistingDisplays()
        {
            if (family == null || slotRoot == null) return;

            int i = 0;
            foreach (var unit in family.ValidUnits)
            {
                if (i >= slotRoot.childCount) break;
                Transform slot = slotRoot.GetChild(i++);

                Transform visual = slot.childCount > 0 ? slot.GetChild(0) : null;
                if (visual != null) displayInstances[unit] = visual.gameObject;

                var tag = slot.GetComponentInChildren<TextMeshPro>(true);
                if (tag != null)
                {
                    absentTags[unit] = tag;
                    tag.gameObject.SetActive(false);
                }
            }
        }

        public bool Contains(ChallengeUnitDefinition unit) => displayInstances.ContainsKey(unit);

        /// <summary>Hide or restore a family member's display copy.</summary>
        public void SetUnitPresent(ChallengeUnitDefinition unit, bool present)
        {
            if (displayInstances.TryGetValue(unit, out var go) && go != null)
                go.SetActive(present);

            if (absentTags.TryGetValue(unit, out var tag) && tag != null)
                tag.gameObject.SetActive(!present);
        }

        public void RestoreAll()
        {
            foreach (var kvp in displayInstances)
                SetUnitPresent(kvp.Key, true);
        }

        // --- Layout helpers, shared with the editor generator ---

        public Vector3 SlotLocalPosition(int index, int total)
        {
            float span = (total - 1) * slotSpacing;
            return new Vector3(-span * 0.5f + index * slotSpacing, 0f, 0f);
        }

        public Quaternion DisplayRotation =>
            Quaternion.LookRotation(displayFacing.sqrMagnitude > 0.001f ? displayFacing : Vector3.back, Vector3.up);

        public float SlotSpacing
        {
            get => slotSpacing;
            set => slotSpacing = value;
        }

        public void Bind(MonsterFamilyDefinition definition, Transform slots, TextMeshPro label, Transform anchor)
        {
            family = definition;
            slotRoot = slots;
            familyLabel = label;
            labelAnchor = anchor;
        }

        public TextMeshPro FamilyLabel => familyLabel;
        public Transform SlotRoot => slotRoot;
        public Transform LabelAnchor => labelAnchor;
    }
}
