using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the Pet Survival roster: one ToonLit prefab + animator per animal from the Cube Pets
    /// pack, so every racer can be a different creature instead of ten recolours of one fox.
    ///
    /// Follows the fox builder's rules: the source pack is only read, every generated asset lives
    /// under Assets/CubeSim, materials are flat white so the per-racer tint stays clean, and clip
    /// names come from the FBXs themselves (the whole pack shares the fox's clip set). Native height
    /// is measured off the instantiated model, not typed in.
    /// </summary>
    public static class PetAssetBuilder
    {
        private const string SourceFolder = "Assets/KenneyDungeon/FBX format/";
        private const string Folder = "Assets/CubeSim/Visuals/Pets";

        /// <summary>The id prefix the racer factory keys the per-racer cycle on.</summary>
        public const string IdPrefix = "Pet_";

        /// <summary>
        /// The photogenic dozen. Skipped on purpose: fish/crab (no legs to run on), caterpillar/bee
        /// (silhouettes too small at this camera), elephant/giraffe (too tall for the corridors).
        /// </summary>
        private static readonly string[] Animals =
        {
            "fox", "cat", "dog", "bunny", "penguin", "pig",
            "panda", "tiger", "lion", "chick", "cow", "koala",
        };

        [MenuItem("CubeSim/Build Pet Roster", priority = 12)]
        public static List<RacerVisualLibrary.Entry> BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Material material = BuildSharedMaterial();

            var entries = new List<RacerVisualLibrary.Entry>();
            foreach (string animal in Animals)
            {
                RacerVisualLibrary.Entry entry = BuildAnimal(animal, material);
                if (entry != null) entries.Add(entry);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CubeSim] Pet roster built: {entries.Count}/{Animals.Length} animals.");
            return entries;
        }

        private static RacerVisualLibrary.Entry BuildAnimal(string animal, Material material)
        {
            string sourcePath = SourceFolder + "animal-" + animal + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"[CubeSim] Pet model missing: {sourcePath}");
                return null;
            }

            string title = char.ToUpperInvariant(animal[0]) + animal.Substring(1);
            string prefabPath = $"{Folder}/CubeSimPet{title}.prefab";
            string controllerPath = $"{Folder}/CubeSimPet{title}.controller";

            AnimatorController controller = BuildController(sourcePath, controllerPath, animal);
            if (controller == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "CubeSimPet" + title;

            // Same hygiene as weapons and the fox: pack models may carry scene-level extras.
            foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
            foreach (AudioListener listener in instance.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            foreach (Light light in instance.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);

            float nativeHeight = 1f;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);

                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                nativeHeight = Mathf.Max(0.2f, bounds.size.y);
            }

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
            {
                if (o is Avatar avatar) { animator.avatar = avatar; break; }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            return new RacerVisualLibrary.Entry
            {
                id = IdPrefix + title,
                prefab = prefab,
                animatorController = controller,
                nativeHeight = nativeHeight,
                scaleMultiplier = 1f,
                yOffset = 0f,
                handBoneName = "",
                displayName = title.ToUpperInvariant(),
                portrait = CapturePortrait(prefab, title)
            };
        }

        /// <summary>
        /// The face shot the leaderboard shows: the pet rendered head-on against transparency,
        /// framed on the upper half of the body. Captured off in the void so whatever scene is open
        /// never leaks into the picture.
        /// </summary>
        private static Sprite CapturePortrait(GameObject prefab, string title)
        {
            string portraitFolder = Folder + "/Portraits";
            Directory.CreateDirectory(portraitFolder);
            string pngPath = $"{portraitFolder}/Pet{title}.png";

            const int Size = 256;
            var stage = new GameObject("PortraitStage");

            try
            {
                GameObject model = Object.Instantiate(prefab, stage.transform);
                model.transform.position = new Vector3(4000f, 4000f, 4000f);

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return null;

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var lightGo = new GameObject("PortraitLight");
                lightGo.transform.SetParent(stage.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

                var camGo = new GameObject("PortraitCamera");
                camGo.transform.SetParent(stage.transform, false);
                var camera = camGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;

                // Frame the upper body, camera out in front (the pack's animals face +Z).
                float headY = bounds.min.y + bounds.size.y * 0.62f;
                camera.orthographicSize = bounds.size.y * 0.42f;
                camera.transform.position = new Vector3(bounds.center.x, headY,
                    bounds.center.z + bounds.size.z * 2.2f);
                camera.transform.LookAt(new Vector3(bounds.center.x, headY, bounds.center.z));
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = bounds.size.z * 6f + 10f;

                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                camera.targetTexture = null;
                rt.Release();

                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(pngPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();

                return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }

        /// <summary>Same state graph as the fox, from this animal's own clips.</summary>
        private static AnimatorController BuildController(string sourcePath, string controllerPath, string animal)
        {
            AnimationClip idle = LoadClip(sourcePath, "idle");
            AnimationClip run = LoadClip(sourcePath, "run") ?? LoadClip(sourcePath, "walk");
            AnimationClip melee = LoadClip(sourcePath, "eat") ?? LoadClip(sourcePath, "gesture-negative");
            AnimationClip ranged = LoadClip(sourcePath, "gesture-positive") ?? melee;
            AnimationClip death = LoadClip(sourcePath, "static") ?? idle;
            AnimationClip celebrate = LoadClip(sourcePath, "dance") ?? ranged;

            if (run == null)
            {
                Debug.LogWarning($"[CubeSim] Pet '{animal}' has no run/walk clip; skipped.");
                return null;
            }

            AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

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
            exit.exitTime = 0.95f;
            exit.duration = 0.1f;
        }

        private static AnimationClip LoadClip(string sourcePath, string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
            {
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview") && clip.name == name)
                {
                    return clip;
                }
            }

            return null;
        }

        /// <summary>
        /// ToonLit with the pack's colormap atlas, shared by every pet. With the texture on, each
        /// species keeps its real colours; the per-racer tint is only applied when the episode asks
        /// for it (tintModels), because tinting through the atlas muddies everything.
        /// </summary>
        private static Material BuildSharedMaterial()
        {
            string path = Folder + "/CubeSimPet.mat";
            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            // The pack ships two colormaps; the one beside the FBX models is the atlas their UVs
            // are laid out against. The other belongs to the dungeon tileset and paints every
            // animal in washed-out wall colours.
            var colormap = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/KenneyDungeon/FBX format/Textures/colormap.png");

            material.shader = shader;
            material.SetTexture("_BaseMap", colormap != null ? colormap : Texture2D.whiteTexture);
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.45f, 0.42f, 0.5f, 1f));
            if (material.HasProperty("_Threshold")) material.SetFloat("_Threshold", 0.4f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
