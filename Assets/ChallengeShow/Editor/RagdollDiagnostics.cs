using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Static, bind-pose analysis of every generated ragdoll.
    ///
    /// Written because "the ragdolls look broken" is not something you can fix by staring at them.
    /// This measures the four things that actually make an articulated body come apart on screen:
    ///
    ///   1. MASS RATIO across a joint. PhysX solves joints iteratively; a light parent carrying a
    ///      much heavier child cannot hold it, and the joint visibly separates.
    ///   2. JOINT SPAN. A joint whose connected body is not the bone's direct parent is bridging
    ///      skipped bones, which puts the pivot in the wrong place and lets the skin stretch.
    ///   3. ANCHOR ERROR at rest. Should be ~0. Anything else means the joint is already fighting
    ///      the bind pose before a single force is applied.
    ///   4. COLLIDER PENETRATION between bones that are NOT joint-connected. Unity already
    ///      suppresses collision across a joint, so anything left here is two bodies genuinely
    ///      starting inside each other, which resolves as a shove.
    ///
    /// Read-only: instantiates each prefab, measures, and destroys it.
    /// </summary>
    public static class RagdollDiagnostics
    {
        private const string CatalogPath = "Assets/ChallengeShow/Data/ChallengeShowCatalog.asset";

        [MenuItem("Challenge Show/Diagnostics/Ragdoll Bind-Pose Audit")]
        public static void Run() => Debug.Log(Report());

        public static string Report()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>(CatalogPath);
            if (catalog == null) return "[Diag] no catalog";

            var sb = new StringBuilder();
            sb.Append("unit             | bodies | joints | totalMass | massMin..Max      | worstRatio (joint)                  | maxAnchorErr | spanSkips | penPairs | worstPen\n");

            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                foreach (var unit in family.ValidUnits)
                    sb.Append(Measure(unit));
            }
            return sb.ToString();
        }

        private static string Measure(ChallengeUnitDefinition unit)
        {
            if (unit.sourcePrefab == null) return $"{unit.displayName}: no prefab\n";

            var go = (GameObject)PrefabUtility.InstantiatePrefab(unit.sourcePrefab);
            try
            {
                // The pooled runtime object is what carries the ragdoll, so rebuild it here the same
                // way the authoring step does; the source prefab itself has no bodies.
                int built = RagdollBuilder.Build(go, unit);
                if (built == 0) return $"{unit.displayName}: ragdoll build produced nothing\n";

                var bodies = go.GetComponentsInChildren<Rigidbody>(true);
                var joints = go.GetComponentsInChildren<CharacterJoint>(true);

                float total = 0f, minM = float.MaxValue, maxM = 0f;
                foreach (var b in bodies)
                {
                    total += b.mass;
                    minM = Mathf.Min(minM, b.mass);
                    maxM = Mathf.Max(maxM, b.mass);
                }

                float worstRatio = 1f; string worstJoint = "-";
                float maxAnchorErr = 0f;
                int spanSkips = 0;

                foreach (var j in joints)
                {
                    if (j.connectedBody == null) continue;

                    var child = j.GetComponent<Rigidbody>();
                    float ratio = Mathf.Max(child.mass / j.connectedBody.mass,
                                            j.connectedBody.mass / child.mass);
                    if (ratio > worstRatio)
                    {
                        worstRatio = ratio;
                        worstJoint = $"{j.name}({child.mass:0.0})<-{j.connectedBody.name}({j.connectedBody.mass:0.0})";
                    }

                    // How many transforms sit between this bone and the body it is jointed to? Zero
                    // means a clean parent-child joint.
                    int skips = 0;
                    var t = j.transform.parent;
                    while (t != null && t != j.connectedBody.transform) { skips++; t = t.parent; }
                    if (t != null && skips > 0) spanSkips += skips;

                    // Anchor error: the two anchor points should coincide in world space at rest.
                    Vector3 a = j.transform.TransformPoint(j.anchor);
                    Vector3 b2 = j.connectedBody.transform.TransformPoint(j.connectedAnchor);
                    maxAnchorErr = Mathf.Max(maxAnchorErr, Vector3.Distance(a, b2));
                }

                // Penetration between bones with no joint between them.
                var cols = new List<Collider>(go.GetComponentsInChildren<Collider>(true));
                int penPairs = 0; float worstPen = 0f; string worstPenPair = "-";
                for (int i = 0; i < cols.Count; i++)
                    for (int k = i + 1; k < cols.Count; k++)
                    {
                        if (JointConnects(cols[i], cols[k])) continue;
                        if (!Physics.ComputePenetration(
                                cols[i], cols[i].transform.position, cols[i].transform.rotation,
                                cols[k], cols[k].transform.position, cols[k].transform.rotation,
                                out _, out float dist)) continue;
                        if (dist <= 0.001f) continue;
                        penPairs++;
                        if (dist > worstPen) { worstPen = dist; worstPenPair = $"{cols[i].name}/{cols[k].name}"; }
                    }

                return string.Format("{0,-16} | {1,6} | {2,6} | {3,9:0.0} | {4,6:0.00}..{5,-9:0.0} | {6,-35} | {7,12:0.0000} | {8,9} | {9,8} | {10:0.000} {11}\n",
                    unit.displayName, bodies.Length, joints.Length, total, minM, maxM,
                    worstRatio.ToString("0.0") + "x " + worstJoint, maxAnchorErr, spanSkips,
                    penPairs, worstPen, worstPenPair);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>True if a joint directly connects these two colliders' bodies (either direction).</summary>
        private static bool JointConnects(Collider a, Collider b)
        {
            var ra = a.GetComponent<Rigidbody>();
            var rb = b.GetComponent<Rigidbody>();
            if (ra == null || rb == null) return false;

            var ja = a.GetComponent<CharacterJoint>();
            if (ja != null && ja.connectedBody == rb) return true;
            var jb = b.GetComponent<CharacterJoint>();
            return jb != null && jb.connectedBody == ra;
        }
    }
}
