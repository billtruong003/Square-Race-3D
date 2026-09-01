using System.Collections.Generic;
using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// One monster family: a display name, an identity colour and its three evolution units.
    /// Family islands and their labels are generated from these assets, so adding a sixth family
    /// is a data change rather than a scene edit.
    /// </summary>
    [CreateAssetMenu(menuName = "Challenge Show/Family Definition", fileName = "Family_")]
    public class MonsterFamilyDefinition : ScriptableObject
    {
        public string familyName = "Family";
        public Color displayColor = Color.white;

        [Tooltip("Ordered base -> final evolution.")]
        public ChallengeUnitDefinition[] units = new ChallengeUnitDefinition[3];

        public IEnumerable<ChallengeUnitDefinition> ValidUnits
        {
            get
            {
                foreach (var u in units)
                    if (u != null) yield return u;
            }
        }

        /// <summary>Tallest unit in the family, used to size island spacing and label height.</summary>
        public float TallestUnitHeight
        {
            get
            {
                float tallest = 0f;
                foreach (var u in ValidUnits) tallest = Mathf.Max(tallest, u.height);
                return tallest;
            }
        }
    }
}
