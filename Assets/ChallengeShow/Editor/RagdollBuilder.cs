using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Generates a ragdoll from an arbitrary bone hierarchy.
    ///
    /// Unity's own ragdoll wizard needs a humanoid mapping, but only 2 of the 15 monsters are
    /// humanoid — the cats are quadrupeds and Cacti has no legs at all. So instead of matching bone
    /// names, this walks the actual skeleton and keeps bones that are structurally significant:
    /// long enough relative to the creature, not on the exclusion list, and within a body budget.
    /// That handles every rig in the pack with one code path.
    /// </summary>
    public static class RagdollBuilder
    {
        private class BoneNode
        {
            public Transform transform;
            public BoneNode parent;
            public Vector3 childOffset;   // local-space vector down the bone
            public float length;
        }

        public static int Build(GameObject root, ChallengeUnitDefinition definition)
        {
            Clear(root);

            Transform pelvis = FindPelvis(root);
            if (pelvis == null)
            {
                Debug.LogWarning($"[ChallengeShow] No root bone found on {root.name}; skipping ragdoll.");
                return 0;
            }

            List<BoneNode> kept = SelectBonesWithFallback(pelvis, definition);
            if (kept.Count < 2)
            {
                Debug.LogWarning($"[ChallengeShow] {root.name}: no usable ragdoll bones found.");
                return 0;
            }

            var bodies = new Dictionary<Transform, Rigidbody>();
            var mass = DistributeMass(kept, definition.mass);

            foreach (var node in kept)
            {
                var go = node.transform.gameObject;

                var body = ComponentUtility.GetOrAdd<Rigidbody>(go);
                body.mass = mass[node];
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Solver iteration counts are deliberately NOT set here. In Unity 6 they are not
                // serialized on Rigidbody at all - the component exposes no m_SolverIterations
                // property - so anything written at edit time is silently discarded and the prefab
                // reloads at the project default. They are applied at runtime in
                // ChallengeUnitRagdoll instead, alongside the other clamps.

                bodies[node.transform] = body;

                AddCollider(node, definition);
            }

            foreach (var node in kept)
            {
                if (node.parent == null || !bodies.ContainsKey(node.parent.transform)) continue;
                var joint = ComponentUtility.GetOrAdd<CharacterJoint>(node.transform.gameObject);
                ConfigureJoint(joint, bodies[node.parent.transform], definition.height);
            }

            return kept.Count;
        }

        /// <summary>
        /// Share the creature's mass out across its bones by how much of the body each one carries.
        ///
        /// The previous model derived mass from an estimated bone volume, with the capsule radius
        /// taken as a fraction of the bone's length - so mass scaled with the CUBE of the distance
        /// to the next joint. That is backwards for exactly the bones that matter. A ribcage's next
        /// joint is close, so it came out almost weightless; a collarbone's is far, so it came out
        /// heavy. The audit measured the result: Skeleton Giant carried a 0.2 kg ribcage holding up
        /// a 5.3 kg collarbone, and Mole Rat King reached a 39.9:1 ratio across a single joint.
        /// PhysX solves joints iteratively and a light parent simply cannot hold a much heavier
        /// child, so the joint separates and the skin stretches between them. That was the
        /// "disconnected bones" look.
        ///
        /// Mass now follows SUBTREE SIZE - what each bone actually has to support. That makes it
        /// decrease monotonically from the root outward by construction, so a parent is never
        /// lighter than its child, and the exponent keeps the spread narrow: for these rigs the
        /// worst ratio across any joint lands near 2:1 instead of 40:1.
        /// </summary>
        private static Dictionary<BoneNode, float> DistributeMass(List<BoneNode> kept, float totalMass)
        {
            var childCount = new Dictionary<BoneNode, int>();
            foreach (var n in kept) childCount[n] = 1;

            // Walk from the leaves back up: kept is breadth-first, so reversing it visits children
            // before parents and one pass accumulates every subtree.
            for (int i = kept.Count - 1; i >= 0; i--)
            {
                var n = kept[i];
                if (n.parent != null && childCount.ContainsKey(n.parent))
                    childCount[n.parent] += childCount[n];
            }

            // 0.65 keeps the ordering (a parent always outweighs its child) while compressing the
            // range, so a 16-bone root is about 6x a leaf rather than 16x.
            var raw = new Dictionary<BoneNode, float>();
            float sum = 0f;
            foreach (var n in kept)
            {
                float w = Mathf.Pow(childCount[n], 0.65f);
                raw[n] = w;
                sum += w;
            }

            var mass = new Dictionary<BoneNode, float>();
            foreach (var n in kept)
                mass[n] = Mathf.Max(0.2f, totalMass * raw[n] / Mathf.Max(0.0001f, sum));
            return mass;
        }

        public static void Clear(GameObject root)
        {
            foreach (var joint in root.GetComponentsInChildren<CharacterJoint>(true))
                Object.DestroyImmediate(joint);

            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                if (body.gameObject != root) Object.DestroyImmediate(body);

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                if (col.gameObject != root) Object.DestroyImmediate(col);
        }

        private static Transform FindPelvis(GameObject root)
        {
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.rootBone != null) return smr.rootBone;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "RigPelvis") return t;
            return null;
        }

        private static float LongestBoneLength(Transform pelvis)
        {
            float longest = 0f;
            foreach (var bone in pelvis.GetComponentsInChildren<Transform>(true))
                foreach (Transform c in bone)
                    longest = Mathf.Max(longest, c.localPosition.magnitude);
            return longest;
        }

        /// <summary>
        /// Compact creatures carry a lot of height in horns and spikes, so a threshold expressed as
        /// a fraction of total height can reject every bone they have. Relax it until the rig
        /// yields a usable ragdoll rather than silently producing none.
        /// </summary>
        private static List<BoneNode> SelectBonesWithFallback(Transform pelvis, ChallengeUnitDefinition definition)
        {
            // Threshold is relative to the longest bone in this rig. Scaling it to the creature's
            // overall height instead made it depend on horns and spikes: Cactus Boss measures 4.25m
            // tall but its leg bones are 0.44m, so a height-derived threshold rejected its legs.
            float minLength = LongestBoneLength(pelvis) * definition.ragdollMinBoneLengthRatio;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var bones = SelectBones(pelvis, definition, minLength);
                if (bones.Count >= 4 || minLength <= 0.02f) return bones;
                minLength *= 0.5f;
            }
            return SelectBones(pelvis, definition, 0.02f);
        }

        /// <summary>
        /// Reduce the full skeleton to a connected set of significant bones.
        ///
        /// Admission is breadth-first from the pelvis, not ranked by bone length. Ranking by length
        /// looked reasonable but gave Cactus Boss a ragdoll of two arms and no legs, because its
        /// arm bones are simply longer than its leg bones. Breadth-first spends the budget evenly
        /// across every limb before going deep into any one of them, and it guarantees a parent is
        /// always admitted before its child so the joint chain stays connected.
        /// </summary>
        private static List<BoneNode> SelectBones(Transform pelvis, ChallengeUnitDefinition definition, float minLength)
        {
            var kept = new List<BoneNode>();
            var rootNode = MakeNode(pelvis, null);
            kept.Add(rootNode);

            var queue = new Queue<(Transform bone, BoneNode significantAncestor)>();
            foreach (Transform child in pelvis) queue.Enqueue((child, rootNode));

            while (queue.Count > 0)
            {
                var (bone, ancestor) = queue.Dequeue();
                var node = MakeNode(bone, ancestor);

                bool significant = !IsExcluded(bone.name, definition.ragdollExcludeNameContains)
                                   && node.length >= minLength
                                   && kept.Count < definition.ragdollBoneBudget;

                BoneNode nextAncestor = ancestor;
                if (significant)
                {
                    kept.Add(node);
                    nextAncestor = node;
                }

                foreach (Transform child in bone) queue.Enqueue((child, nextAncestor));
            }

            return kept;
        }

        /// <summary>
        /// A bone's reach is its longest child offset, not the average of them. Averaging cancels
        /// out on symmetric branch points — a ribcage with a left arm, a right arm and a neck
        /// averages to almost zero and gets discarded as a stub, taking both arms with it.
        /// </summary>
        private static BoneNode MakeNode(Transform bone, BoneNode ancestor)
        {
            Vector3 longest = Vector3.zero;
            foreach (Transform c in bone)
                if (c.localPosition.sqrMagnitude > longest.sqrMagnitude) longest = c.localPosition;

            return new BoneNode
            {
                transform = bone,
                parent = ancestor,
                childOffset = longest,
                length = longest.magnitude
            };
        }

        private static bool IsExcluded(string name, string[] patterns)
        {
            if (patterns == null) return false;
            foreach (var p in patterns)
                if (!string.IsNullOrEmpty(p) && name.Contains(p)) return true;
            return false;
        }

        private static float BoneRadius(BoneNode node) => Mathf.Max(0.04f, node.length * 0.32f);

        private static float EstimateVolume(BoneNode node)
        {
            float r = BoneRadius(node);
            return Mathf.PI * r * r * Mathf.Max(node.length, r * 2f);
        }

        /// <summary>
        /// These rigs are 3ds Max style: a bone's children sit along its own local axis, usually -X.
        /// The capsule is laid along whichever local axis the child actually sits on rather than
        /// assuming, because a few bones (tails, feelers) branch differently.
        /// </summary>
        private static void AddCollider(BoneNode node, ChallengeUnitDefinition definition)
        {
            var go = node.transform.gameObject;
            float radius = BoneRadius(node);

            if (node.length < radius * 1.6f)
            {
                var sphere = ComponentUtility.GetOrAdd<SphereCollider>(go);
                sphere.radius = Mathf.Max(radius, definition.height * 0.06f);
                sphere.center = Vector3.zero;
                return;
            }

            var capsule = ComponentUtility.GetOrAdd<CapsuleCollider>(go);
            Vector3 v = node.childOffset;
            int axis = 0;
            float maxAbs = Mathf.Abs(v.x);
            if (Mathf.Abs(v.y) > maxAbs) { axis = 1; maxAbs = Mathf.Abs(v.y); }
            if (Mathf.Abs(v.z) > maxAbs) axis = 2;

            capsule.direction = axis;
            capsule.radius = radius;
            capsule.height = node.length + radius * 2f;
            capsule.center = v * 0.5f;
        }

        private static void ConfigureJoint(CharacterJoint joint, Rigidbody connectedTo, float creatureScale)
        {
            joint.connectedBody = connectedTo;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;

            // Projection is the last line of defence: it snaps a joint back when the solver has let
            // it drift. A flat 0.1 m meant 10% of Cacti's entire body but almost nothing on the
            // 4.25 m Cactus Boss, so the small creatures were allowed to visibly come apart before
            // anything pulled them back. Scaling it to the creature keeps the tolerance consistent.
            joint.projectionDistance = Mathf.Clamp(creatureScale * 0.02f, 0.01f, 0.06f);
            joint.projectionAngle = 20f;

            // Loose limits: this is cartoon flailing, not anatomically plausible articulation.
            joint.lowTwistLimit = new SoftJointLimit { limit = -45f };
            joint.highTwistLimit = new SoftJointLimit { limit = 45f };
            joint.swing1Limit = new SoftJointLimit { limit = 55f };
            joint.swing2Limit = new SoftJointLimit { limit = 45f };
        }
    }
}
