using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Builds a minimal Animator controller for each gameplay unit.
    ///
    /// The vendor controllers are showreels: every state carries an exit-time transition into the
    /// next one, so a unit told to run plays its run clip once and then wanders off into
    /// "Idle 0" or "Look Around" mid-challenge. They also enable root motion, which drives the
    /// root transform and cancels out the motor entirely.
    ///
    /// Rather than editing the vendor assets, each gameplay unit gets its own two-state controller
    /// -- Idle and Move, no transitions -- reusing the vendor clips by reference. Island display
    /// copies keep the original controller, so they still idle and perform their flourishes.
    /// </summary>
    public static class UnitAnimatorBuilder
    {
        public const string IdleState = "Idle";
        public const string MoveState = "Move";
        public const string RecoverState = "Recover";

        /// <summary>
        /// Stand-up clip candidates, best first. No unit in the pack has a true get-up-from-prone
        /// animation, so the two Skeletons use their Resurrect clip and everyone else uses Take
        /// Damage, whose opening recoil reads as shaking off the hit and covers the pose snap.
        /// </summary>
        private static readonly string[] RecoverCandidates = { "Resurrect", "Take Damage", "Idle" };

        private const string OutputFolder = "Assets/ChallengeShow/Data/Animators";

        /// <summary>
        /// Locomotion clips chosen per unit where the generic "Run Forward" pick is not the
        /// characterful one. Burrow travels as a head breaching the ground; the Skeleton creeps;
        /// the Mage has no legs and only ever flies.
        /// </summary>
        private static readonly (string unit, string state)[] LocomotionOverrides =
        {
            ("Burrow", "Head Only Move Forward In Place"),
            ("Skeleton", "Creep Dash Forward In Place"),
            ("Skeleton Mage", "Fly Forward In Place")
        };

        [MenuItem("Challenge Show/4. Build Unit Animators")]
        public static void BuildAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>(
                "Assets/ChallengeShow/Data/ChallengeShowCatalog.asset");
            if (catalog == null) { Debug.LogError("[ChallengeShow] No catalog."); return; }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/ChallengeShow/Data", "Animators");

            int built = 0;
            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                foreach (var unit in family.ValidUnits)
                    if (Build(unit)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChallengeShow] Built {built} unit animators.");
        }

        private static bool Build(ChallengeUnitDefinition definition)
        {
            if (definition.gameplayPrefab == null || definition.sourcePrefab == null) return false;

            var sourceAnimator = definition.sourcePrefab.GetComponentInChildren<Animator>(true);
            var vendor = sourceAnimator != null
                ? sourceAnimator.runtimeAnimatorController as AnimatorController
                : null;
            if (vendor == null)
            {
                Debug.LogWarning($"[ChallengeShow] {definition.displayName}: no vendor controller.");
                return false;
            }

            string moveState = ResolveOverride(definition.displayName) ?? definition.runStateName;
            AnimationClip moveClip = FindClip(vendor, moveState) ?? FindClip(vendor, definition.runStateName);
            AnimationClip idleClip = FindClip(vendor, definition.idleStateName) ?? FindClip(vendor, "Idle");

            AnimationClip recoverClip = null;
            string recoverSource = null;
            foreach (var candidate in RecoverCandidates)
            {
                recoverClip = FindClip(vendor, candidate);
                if (recoverClip != null) { recoverSource = candidate; break; }
            }

            if (moveClip == null)
            {
                Debug.LogWarning($"[ChallengeShow] {definition.displayName}: no clip for '{moveState}'.");
                return false;
            }

            string path = $"{OutputFolder}/CU_{definition.displayName.Replace(" ", "")}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            // Remove the empty state Unity seeds new controllers with.
            foreach (var s in machine.states.ToArray()) machine.RemoveState(s.state);

            var idle = machine.AddState(IdleState);
            idle.motion = idleClip != null ? idleClip : moveClip;
            idle.writeDefaultValues = true;

            var move = machine.AddState(MoveState);
            move.motion = moveClip;
            move.writeDefaultValues = true;

            var recover = machine.AddState(RecoverState);
            recover.motion = recoverClip != null ? recoverClip : idleClip;
            recover.writeDefaultValues = true;

            // No transitions at all. The unit decides what it is doing; the controller never
            // advances on its own, which is the whole point of replacing the vendor graph.
            machine.defaultState = idle;

            definition.runStateName = moveState;
            // The definition stores the SOURCE clip name so the unit can look up its real duration;
            // the controller state itself is always called "Recover".
            definition.recoverStateName = recoverClip != null ? recoverClip.name : idleClip != null ? idleClip.name : "";
            EditorUtility.SetDirty(definition);

            ApplyToPrefab(definition, controller);
            Debug.Log($"[ChallengeShow] {definition.displayName}: move '{moveClip.name}' " +
                      $"({moveClip.length:0.00}s) | recover '{recoverSource ?? "none"}' " +
                      $"({(recoverClip != null ? recoverClip.length : 0f):0.00}s)");
            return true;
        }

        private static void ApplyToPrefab(ChallengeUnitDefinition definition, AnimatorController controller)
        {
            string prefabPath = AssetDatabase.GetAssetPath(definition.gameplayPrefab);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = controller;
                // Root motion would drive the transform and fight the Rigidbody motor; the motor
                // owns movement, the animation only sells it.
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static string ResolveOverride(string unitName)
        {
            foreach (var (unit, state) in LocomotionOverrides)
                if (unit == unitName) return state;
            return null;
        }

        private static AnimationClip FindClip(AnimatorController controller, string stateName)
        {
            if (string.IsNullOrEmpty(stateName) || controller.layers.Length == 0) return null;
            foreach (var s in controller.layers[0].stateMachine.states)
                if (s.state.name == stateName) return s.state.motion as AnimationClip;
            return null;
        }
    }
}
