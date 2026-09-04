using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds CubeSim's own animator controller and visual library from the Cute Series skeleton.
    ///
    /// Nothing in the asset pack is modified: the source prefab and its shipped controller are only
    /// read. The controller written here lives under Assets/CubeSim and is applied to the spawned
    /// model instance at runtime.
    /// </summary>
    public static class SkeletonAssetBuilder
    {
        public const string SkeletonPrefabPath =
            "Assets/Monsters Ultimate Pack 03 Cute Series/Skeleton Cute Series/Prefabs/Skeleton.prefab";

        private const string ClipFolder =
            "Assets/Monsters Ultimate Pack 03 Cute Series/Skeleton Cute Series/FBX/";

        public const string ControllerPath = "Assets/CubeSim/Visuals/Skeleton/CubeSimSkeleton.controller";
        public const string VisualLibraryPath = "Assets/CubeSim/Data/RacerVisualLibrary.asset";

        // Clip names read out of the pack's FBX files - not guessed.
        private const string RunClip = "Creep Dash Forward In Place";
        private const string DieClip = "Die";
        private const string MeleeClip = "Right Slash Attack";
        private const string RangedClip = "Projectile Attack";

        [MenuItem("CubeSim/Build Racer Visual Assets", priority = 10)]
        public static RacerVisualLibrary BuildAll()
        {
            AnimatorController controller = BuildController();
            RacerVisualLibrary.Entry fox = FoxAssetBuilder.BuildAll();
            return BuildVisualLibrary(controller, fox);
        }

        public static AnimatorController BuildController()
        {
            Directory.CreateDirectory("Assets/CubeSim/Visuals/Skeleton");

            AnimationClip run = LoadClip("Skeleton@Creep Dash Forward In Place.FBX", RunClip);
            AnimationClip die = LoadClip("Skeleton@Die.FBX", DieClip);
            AnimationClip melee = LoadClip("Skeleton@Right Slash Attack.FBX", MeleeClip);
            AnimationClip ranged = LoadClip("Skeleton@Projectile Attack.FBX", RangedClip);

            if (run == null)
            {
                Debug.LogError("[CubeSim] Run clip not found; the skeleton controller was not built.");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("AttackMelee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackRanged", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState runState = machine.AddState("Run");
            runState.motion = run;
            machine.defaultState = runState;

            AddOneShot(machine, runState, "AttackMelee", melee, "AttackMelee");
            AddOneShot(machine, runState, "AttackRanged", ranged, "AttackRanged");

            if (die != null)
            {
                AnimatorState dieState = machine.AddState("Die");
                dieState.motion = die;

                AnimatorStateTransition toDie = machine.AddAnyStateTransition(dieState);
                toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                toDie.duration = 0.08f;
                toDie.hasExitTime = false;
                toDie.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CubeSim] Animator controller built at {ControllerPath} " +
                      $"(run='{RunClip}', die='{DieClip}', melee='{MeleeClip}', ranged='{RangedClip}')");

            return controller;
        }

        /// <summary>A triggered state that plays once and falls back to the looping run.</summary>
        private static void AddOneShot(AnimatorStateMachine machine, AnimatorState returnState,
            string stateName, AnimationClip clip, string trigger)
        {
            if (clip == null) return;

            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.duration = 0.06f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = state.AddTransition(returnState);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.12f;
        }

        private static AnimationClip LoadClip(string fbxFile, string clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ClipFolder + fbxFile);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && clip.name == clipName) return clip;
            }

            Debug.LogWarning($"[CubeSim] Clip '{clipName}' not found in {fbxFile}.");
            return null;
        }

        public static RacerVisualLibrary BuildVisualLibrary(AnimatorController controller,
            RacerVisualLibrary.Entry fox = null)
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
            if (prefab == null) Debug.LogWarning($"[CubeSim] Skeleton prefab missing at {SkeletonPrefabPath}.");

            var library = AssetDatabase.LoadAssetAtPath<RacerVisualLibrary>(VisualLibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<RacerVisualLibrary>();

            var entries = new List<RacerVisualLibrary.Entry>();
            if (fox != null) entries.Add(fox);
            entries.AddRange(PetAssetBuilder.BuildAll());

            entries.AddRange(new List<RacerVisualLibrary.Entry>
            {
                new RacerVisualLibrary.Entry
                {
                    id = "Skeleton",
                    prefab = prefab,
                    animatorController = controller,
                    nativeHeight = 1.88f,   // measured from the prefab's renderer bounds
                    scaleMultiplier = 1f,
                    yOffset = 0f,
                    handBoneName = ""       // humanoid rig: resolved via HumanBodyBones.RightHand
                },
                new RacerVisualLibrary.Entry
                {
                    id = "Cube",
                    prefab = null,
                    animatorController = null,
                    nativeHeight = 1f,
                    scaleMultiplier = 1f
                }
            });

            library.SetEntries(entries);

            if (isNew) AssetDatabase.CreateAsset(library, VisualLibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // SetEntries above replaces the whole list, which silently dropped the eye cube once
            // already (plain no-eye cubes, Racer_00 names). Re-register it every rebuild.
            EyeCubeAssetBuilder.Build();

            return AssetDatabase.LoadAssetAtPath<RacerVisualLibrary>(VisualLibraryPath);
        }
    }
}
