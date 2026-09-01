using UnityEngine;

namespace ChallengeShow
{
    /// <summary>
    /// The show's full roster. Single entry point for editor tooling, island generation and the
    /// director, so nothing needs to scan the project at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Challenge Show/Show Catalog", fileName = "ChallengeShowCatalog")]
    public class ChallengeShowCatalog : ScriptableObject
    {
        public MonsterFamilyDefinition[] families = new MonsterFamilyDefinition[5];

        public int TotalUnits
        {
            get
            {
                int n = 0;
                foreach (var f in families)
                {
                    if (f == null) continue;
                    foreach (var _ in f.ValidUnits) n++;
                }
                return n;
            }
        }

        public MonsterFamilyDefinition FamilyOf(ChallengeUnitDefinition unit)
        {
            foreach (var f in families)
            {
                if (f == null) continue;
                foreach (var u in f.ValidUnits)
                    if (u == unit) return f;
            }
            return null;
        }
    }
}
