using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds CubeSim's fox racer: a ToonLit-converted prefab variant plus its own animator
    /// controller.
    ///
    /// The Cube Pets pack ships the fox on URP/Lit with a shared "colormap" material. Rather than
    /// editing the pack (which must stay untouched), this writes a CubeSim-owned prefab and material
    /// under Assets/CubeSim and leaves the source asset alone.
    ///
    /// Clip names below were read out of the FBX, not guessed:
    ///   static, idle, walk, run, eat, dance, gesture-positive, gesture-negative
    /// </summary>
    public static class FoxAssetBuilder
    {
        public const string SourceModel = "Assets/KenneyDungeon/FBX format/animal-fox.fbx";
        public const string SourceTexture = "Assets/KenneyDungeon/Textures/colormap.png";

        public const string Folder = "Assets/CubeSim/Visuals/Fox";
        public const string MaterialPath = Folder + "/CubeSimFox.mat";
        public const string PrefabPath = Folder + "/CubeSimFox.prefab";
        public const string ControllerPath = Folder + "/CubeSimFox.controller";

        // Clips as they exist in the FBX.
        private const string IdleClip = "idle";
        private const string RunClip = "run";
        private const string MeleeClip = "eat";                 // a lunging bite - reads as a melee hit
        private const string RangedClip = "gesture-positive";   // a raised-head gesture - reads as a shot
        private const string DeathClip = "static";              // no death clip in the pack; see RacerVisual
        private const string CelebrateClip = "dance";

        [MenuItem("CubeSim/Build Fox Racer Assets", priority = 11)]
        public static RacerVisualLibrary.Entry BuildAll()
        {
            Directory.CreateDirectory(Folder);

            Material material = BuildMaterial();
            GameObject prefab = BuildPrefab(material);
            AnimatorController controller = BuildController();

            return new RacerVisualLibrary.Entry
            {
                id = "Fox",
                prefab = prefab,
                animatorController = controller,
                nativeHeight = 1.69f,   // measured from the model's renderer bounds
                scaleMultiplier = 1f,
                yOffset = 0f,
                handBoneName = ""
            };
        }

        /// <summary>
        /// The pack's material is URP/Lit, which sits outside the toon look the rest of CubeSim uses.
        /// This makes a ToonLit copy that keeps the pack's colormap atlas.
        /// </summary>
        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;

            // Deliberately untextured. Racer colour is applied per-instance as a _BaseColor tint,
            // and multiplying a bright team colour through the pack's orange colormap atlas turns
            // every fox muddy brown. Flat colour is also what the reference footage uses.
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.45f, 0.42f, 0.5f, 1f));
            if (material.HasProperty("_Threshold")) material.SetFloat("_Threshold", 0.4f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            if (material.HasProperty("_RimColor")) material.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.25f));

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The pack's shared atlas. Falls back to whatever the source material already uses.</summary>
        private static Texture FindColormap()
        {
            var direct = AssetDatabase.LoadAssetAtPath<Texture>(SourceTexture);
            if (direct != null) return direct;

            foreach (string guid in AssetDatabase.FindAssets("colormap t:Texture", new[] { "Assets/KenneyDungeon" }))
            {
                var found = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null) return found;
            }

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SourceModel))
            {
                if (o is Material m && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                {
                    return m.GetTexture("_BaseMap");
                }
            }

            Debug.LogWarning("[CubeSim] Fox colormap texture not found; the fox will be untextured.");
            return null;
        }

        /// <summary>
        /// A CubeSim-owned prefab of the fox with the ToonLit material and an Animator ready to go.
        /// The source FBX is only read.
        /// </summary>
        private static GameObject BuildPrefab(Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (source == null)
            {
                Debug.LogError($"[CubeSim] Fox model not found at {SourceModel}.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "CubeSimFox";

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;

            var importer = AssetImporter.GetAtPath(SourceModel) as ModelImporter;
            if (importer != null)
            {
                Avatar avatar = null;
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SourceModel))
                {
                    if (o is Avatar a) { avatar = a; break; }
                }

                if (avatar != null) animator.avatar = avatar;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>
        /// Idle / Run / MeleeAttack / RangedAttack / Death / Celebrate. The pack has no death clip,
        /// so Death holds the neutral pose and RacerVisual tips the model over - which is why the
        /// state exists here rather than being faked entirely in code.
        /// </summary>
        public static AnimatorController BuildController()
        {
            Directory.CreateDirectory(Folder);

            AnimationClip idle = LoadClip(IdleClip);
            AnimationClip run = LoadClip(RunClip);
            AnimationClip melee = LoadClip(MeleeClip);
            AnimationClip ranged = LoadClip(RangedClip);
            AnimationClip death = LoadClip(DeathClip);
            AnimationClip celebrate = LoadClip(CelebrateClip);

            if (run == null)
            {
                Debug.LogError("[CubeSim] Fox run clip not found; controller not built.");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackMelee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackRanged", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Celebrate", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState runState = machine.AddState("Run");
            runState.motion = run;
            machine.defaultState = runState;

            if (idle != null)
            {
                AnimatorState idleState = machine.AddState("Idle");
                idleState.motion = idle;

                AnimatorStateTransition toIdle = runState.AddTransition(idleState);
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
                toIdle.hasExitTime = false;
                toIdle.duration = 0.12f;

                AnimatorStateTransition toRun = idleState.AddTransition(runState);
                toRun.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
                toRun.hasExitTime = false;
                toRun.duration = 0.12f;
            }

            AddOneShot(machine, runState, "AttackMelee", melee, "AttackMelee");
            AddOneShot(machine, runState, "AttackRanged", ranged, "AttackRanged");
            AddOneShot(machine, runState, "Celebrate", celebrate, "Celebrate");

            if (death != null)
            {
                AnimatorState dieState = machine.AddState("Death");
                dieState.motion = death;

                AnimatorStateTransition toDie = machine.AddAnyStateTransition(dieState);
                toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                toDie.duration = 0.06f;
                toDie.hasExitTime = false;
                toDie.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CubeSim] Fox animator built at {ControllerPath} (idle='{IdleClip}', run='{RunClip}', " +
                      $"melee='{MeleeClip}', ranged='{RangedClip}', death='{DeathClip}', celebrate='{CelebrateClip}')");

            return controller;
        }

        private static void AddOneShot(AnimatorStateMachine machine, AnimatorState returnState,
            string stateName, AnimationClip clip, string trigger)
        {
            if (clip == null) return;

            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.duration = 0.05f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = state.AddTransition(returnState);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.1f;
        }

        private static AnimationClip LoadClip(string clipName)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SourceModel))
            {
                if (o is AnimationClip clip && clip.name == clipName) return clip;
            }

            Debug.LogWarning($"[CubeSim] Fox clip '{clipName}' not found in {SourceModel}.");
            return null;
        }
    }
}
