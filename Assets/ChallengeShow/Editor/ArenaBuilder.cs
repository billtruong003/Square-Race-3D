using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Assembles the arena scene from the catalog and an <see cref="ArenaRecipe"/>.
    ///
    /// This builder used to place the environment tile by tile: roughly 610 renderers and 1,200
    /// GameObjects of floors, walls, columns and pebbles, all authored as hard-coded coordinates in
    /// this one file. It now instantiates a short list of baked macro prefabs instead, and confines
    /// itself to what actually has to be wired up in the scene: gameplay collision, the systems, the
    /// rosters and the presentation.
    ///
    /// The split matters more than the object count. Environment art is authored and baked in
    /// <see cref="MacroAuthoring"/> / <see cref="KenneyMacroBaker"/> and composed by the recipe;
    /// nothing below this line decides what a court looks like.
    /// </summary>
    public static class ArenaBuilder
    {
        private const string GeneratedRoot = "ChallengeArena";

        /// <summary>Set during a build so later passes can wire to the systems just created.</summary>
        private static ChallengeRunRecorder LastBuiltRecorder;
        private static ChallengeDirector LastBuiltDirector;

        private const string ScenePath = "Assets/ChallengeShow/Scenes/ChallengeArena.unity";
        private const string CatalogPath = "Assets/ChallengeShow/Data/ChallengeShowCatalog.asset";
        private const string MaterialRoot = "Assets/ChallengeShow/Materials";
        private const string RecipePath = "Assets/ChallengeShow/Environment/Data/ArenaRecipe_Video1.asset";

        // --- arena layout, all in metres; Z is down-lane, Y is up ---
        //
        // These are the validated gameplay numbers. The environment macros are laid out to serve
        // them; they are never adjusted to suit the art.
        private const float LaneWidth = 9f;
        private const float LaneStartZ = -6f;
        private const float LaneEndZ = 50f;

        // Spacing is driven by where launched units actually land. A strike at the arm throws a
        // unit 3.5-10 m back, so the wall face has to sit inside that band or the crystal impact
        // never happens; and the arm's strike capsule reaches back to ArmZ - 6.05, which has to
        // clear the start line by more than the largest unit's radius (Cactus Boss is 1.14 m).
        private const float WallZ = 6.1f;    // impact face spans z 5.9-6.9, i.e. 1.6-2.6 m behind the start
        private const float SpawnZ = 8.5f;   // 2.7 m clear of the wall face
        private const float ArmZ = 16f;      // reach starts at 9.95, spawn front edge is 9.64
        private const float FinishZ = 42f;
        private const float ArmPivotHeight = 4.6f;
        private const float ArmScale = 0.35f;

        /// <summary>
        /// Sweep speed, in degrees per second.
        ///
        /// Worth knowing before touching it: <see cref="RotatingArmObstacle"/> NORMALISES the arm's
        /// surface velocity before applying the impulse, so this value changes encounter rate and
        /// contact angle only. Per-hit impulse, deltaV and therefore per-hit damage are completely
        /// independent of it - the trial below measured an identical peak deltaV of 9.2 m/s at every
        /// speed, which confirms it.
        ///
        /// Chosen from a four-point trial, 15 units per speed, identical conditions:
        ///
        ///     90  PASS 4/15   2.60 hits/unit   9 wall impacts
        ///     110 PASS 1/15   2.67 hits/unit  12 wall impacts
        ///     120 PASS 0/15   2.87 hits/unit  10 wall impacts
        ///     135 PASS 0/15   2.93 hits/unit  10 wall impacts
        ///
        /// Hits per unit climb monotonically with speed while the pass rate collapses, because a
        /// faster arm simply meets the runner more often.
        ///
        /// That trial ran before the ragdoll depenetration clamp was fixed, and the fix moved the
        /// whole curve: with ragdolls no longer being ejected at absurd speed they settle nearer the
        /// arm, so every speed now lands more hits than it did. Re-measured on the corrected build,
        /// 90 deg/s already averages 3.40 meaningful hits per unit against toughness values of about
        /// 3 to 4 - so the roster is close to the overwhelm threshold at the SLOWEST speed tested,
        /// and any increase in encounter rate removes the remaining finishers. 110 on the corrected
        /// build passed nobody at all.
        ///
        /// 90 therefore stays, not from caution but because the fix consumed the headroom that made
        /// a faster arm survivable. Final measured result: 2 passes, 3.40 hits/unit, 30 recoveries,
        /// 12 wall impacts, no instability.
        /// </summary>
        private const float ArmRotationSpeed = 90f;

        [MenuItem("Challenge Show/3. Build Arena Scene")]
        public static void BuildArena()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[ChallengeShow] No catalog — run steps 1 and 2 first.");
                return;
            }

            var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipe>(RecipePath);
            if (recipe == null)
            {
                Debug.LogError("[ChallengeShow] No arena recipe — run step 2b to build the macro library.");
                return;
            }

            Scene scene = EnsureScene();

            var stale = GameObject.Find(GeneratedRoot);

            // Hand-authored presentation survives the rebuild.
            //
            // This command regenerates the arena by destroying its whole root, which used to take
            // the Lighting and CloudSea objects - and ten RenderSettings values - with it. Those
            // carry work that exists only in the scene: a chosen skybox, fog switched off, a retuned
            // cloud volume. They are detached first, then re-attached, and the builder only authors
            // them when there was genuinely nothing there.
            var presentation = PresentationLock.Capture(stale);

            if (stale != null) Object.DestroyImmediate(stale);

            var root = new GameObject(GeneratedRoot);
            presentation.Restore(root.transform);

            if (!presentation.HasLighting) BuildLighting(root.transform);
            if (!presentation.HasCloud) BuildCloudSea(root.transform);

            var bastions = BuildEnvironment(root.transform, recipe);
            var lane = BuildLaneLogic(root.transform);
            BuildSpikeWall(root.transform, out var wallCollider);
            var arm = BuildArmObstacle(root.transform);
            var displays = BuildRosters(catalog, bastions);

            BuildSystems(root.transform, catalog, lane, displays, arm);
            BuildVfx(root.transform, wallCollider);
            BuildPresentation(root.transform, catalog);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ChallengeShow] Arena built from {recipe.lane.Length} lane macros, " +
                      $"{displays.Count} bastions, lane {LaneStartZ}..{LaneEndZ}.");
        }

        /// <summary>
        /// Re-place the environment macros only, leaving gameplay, systems and presentation alone.
        ///
        /// Exists so that re-composing the map does not require the full destructive rebuild. The
        /// project should not force a whole-scene regeneration to move one bastion.
        /// </summary>
        [MenuItem("Challenge Show/3a. Rebuild Environment Only")]
        public static void RebuildEnvironmentOnly()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipe>(RecipePath);
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>(CatalogPath);
            var root = GameObject.Find(GeneratedRoot);
            if (recipe == null || catalog == null || root == null)
            {
                Debug.LogError("[ChallengeShow] Need an existing arena, a recipe and a catalog.");
                return;
            }

            var oldEnv = root.transform.Find("Environment");
            if (oldEnv != null) Object.DestroyImmediate(oldEnv.gameObject);

            var bastions = BuildEnvironment(root.transform, recipe);
            var displays = BuildRosters(catalog, bastions);

            // The director holds references into the bastions that were just replaced.
            var director = Object.FindFirstObjectByType<ChallengeDirector>();
            if (director != null) SerializeFieldArray(director, "familyDisplays", displays.ToArray());

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[ChallengeShow] Environment re-placed: {recipe.lane.Length} lane macros, {displays.Count} bastions.");
        }

        /// <summary>
        /// Overwrite scene presentation with builder defaults. Destructive and deliberately awkward
        /// to reach - it throws away exactly the hand-tuning <see cref="PresentationLock"/> exists
        /// to protect, so it is never part of a normal rebuild.
        /// </summary>
        [MenuItem("Challenge Show/3z. RESET Presentation (destructive)")]
        public static void ResetPresentation()
        {
            if (!EditorUtility.DisplayDialog("Reset presentation?",
                    "This discards the scene's current skybox, fog, sun colour and cloud settings " +
                    "and replaces them with builder defaults. Manual lighting and " +
                    "cloud tuning will be lost.",
                    "Reset", "Cancel"))
                return;

            var root = GameObject.Find(GeneratedRoot);
            if (root == null) { Debug.LogError("[ChallengeShow] No arena in the scene."); return; }

            var oldLighting = root.transform.Find("Lighting");
            if (oldLighting != null) Object.DestroyImmediate(oldLighting.gameObject);
            var oldCloud = root.transform.Find("CloudSea");
            if (oldCloud != null) Object.DestroyImmediate(oldCloud.gameObject);

            BuildLighting(root.transform);
            BuildCloudSea(root.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Scene EnsureScene()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        // ---------------------------------------------------------------- environment

        /// <summary>
        /// Instantiate the recipe: lane sections chained through their sockets, then bastions and
        /// landmarks at their authored spots.
        ///
        /// Lane sections are positioned by socket rather than by index arithmetic, so a section of a
        /// different length can be dropped into the list without recomputing anything downstream.
        /// </summary>
        /// <returns>The bastion instances, in catalog order.</returns>
        private static List<Transform> BuildEnvironment(Transform parent, ArenaRecipe recipe)
        {
            var env = NewChild(parent, "Environment");

            var laneRoot = NewChild(env.transform, "Lane");
            float cursor = recipe.laneStartZ;

            foreach (var section in recipe.lane)
            {
                if (section.macro == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(section.macro, laneRoot.transform);
                inst.name = section.macro.name;

                float entrance = SocketZ(inst.transform, KenneyMacroBaker.SocketEntrance);
                float exit = SocketZ(inst.transform, KenneyMacroBaker.SocketExit);

                inst.transform.localPosition = new Vector3(0f, 0f, cursor - entrance);
                cursor += exit - entrance;
            }

            var bastionRoot = NewChild(env.transform, "Bastions");
            var bastions = new List<Transform>();
            foreach (var p in recipe.bastions)
                bastions.Add(PlaceMacro(bastionRoot.transform, p));

            var landmarkRoot = NewChild(env.transform, "Landmarks");
            foreach (var p in recipe.landmarks)
                PlaceMacro(landmarkRoot.transform, p);

            return bastions;
        }

        private static float SocketZ(Transform macro, string socketName)
        {
            var s = macro.Find(socketName);
            return s != null ? s.localPosition.z : 0f;
        }

        /// <summary>
        /// Place one macro. Bastions turn to face the lane, because their whole purpose is to show
        /// three contestants to the camera; the recipe's yaw is an offset on top of that so no two
        /// stages sit perfectly square.
        /// </summary>
        private static Transform PlaceMacro(Transform parent, ArenaRecipe.Placement p)
        {
            if (p.macro == null) return null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(p.macro, parent);
            inst.name = string.IsNullOrEmpty(p.label) ? p.macro.name : p.macro.name;
            inst.transform.position = p.position;

            Vector3 toLane = new Vector3(0f, p.position.y, 26f) - p.position;
            Quaternion face = toLane.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(new Vector3(toLane.x, 0f, toLane.z), Vector3.up)
                : Quaternion.identity;
            inst.transform.rotation = face * Quaternion.Euler(0f, p.yaw, 0f);
            inst.transform.localScale = Vector3.one * (p.scale <= 0f ? 1f : p.scale);

            return inst.transform;
        }

        // ---------------------------------------------------------------- lighting

        private static void BuildLighting(Transform parent)
        {
            var group = NewChild(parent, "Lighting");

            var sunGo = NewChild(group.transform, "Sun");
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.87f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;

            var skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ilumisoft/Mountain Valley/Materials/Skybox.mat");
            if (skybox != null) RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.32f, 0.36f);
            RenderSettings.sun = sun;

            // Aerial perspective. Distance is otherwise read almost entirely from apparent size,
            // which is what made the frame look flat. Linear fog starting well past the lane keeps
            // the gameplay area completely crisp while separating the background layer.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.78f, 0.90f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 230f;
        }

        /// <summary>
        /// The cloud sea beneath the arena.
        ///
        /// Sized to sit under every macro and run well past them in every direction, so the frame
        /// never shows where the volume stops. Its top is below the lowest understructure, which
        /// keeps it filling the empty lower world without ever swallowing a contestant, the runner,
        /// the arm or a label.
        /// </summary>
        private static void BuildCloudSea(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CloudSea";
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // pure visual, never physics

            const float CloudTopY = -3.5f;
            const float CloudThickness = 26f;
            go.transform.localPosition = new Vector3(0f, CloudTopY - CloudThickness * 0.5f, 26f);
            go.transform.localScale = new Vector3(420f, CloudThickness, 420f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = LoadOrCreateCloudMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var sea = go.AddComponent<CloudSea>();
            SerializeField(sea, "quality", 1);   // Medium by default
            SerializeField(sea, "noiseTexture", CloudNoiseGenerator.LoadOrBake());
        }

        private static Material LoadOrCreateCloudMaterial()
        {
            const string path = MaterialRoot + "/CloudSea.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("CleanRender/CloudSea")) { name = "CloudSea" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- lane gameplay

        /// <summary>
        /// The lane's gameplay surface: one hidden box collider, the lane component, and the two
        /// verdict volumes.
        ///
        /// Collision and presentation are completely separate. The collider is a plain box so the
        /// run is perfectly predictable, and the environment macros are dressed on top of it without
        /// contributing a single collider of their own.
        /// </summary>
        private static ChallengeLane BuildLaneLogic(Transform parent)
        {
            var group = NewChild(parent, "LaneGameplay");
            float length = LaneEndZ - LaneStartZ;
            float midZ = (LaneStartZ + LaneEndZ) * 0.5f;

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "LaneCollision";
            deck.transform.SetParent(group.transform, false);
            deck.transform.localPosition = new Vector3(0f, -0.5f, midZ);
            deck.transform.localScale = new Vector3(LaneWidth, 1f, length);
            Object.DestroyImmediate(deck.GetComponent<MeshRenderer>());   // never drawn

            var laneGo = NewChild(group.transform, "Lane");
            var lane = laneGo.AddComponent<ChallengeLane>();

            var start = NewChild(laneGo.transform, "StartPoint");
            start.transform.localPosition = new Vector3(0f, 0f, SpawnZ);
            var finish = NewChild(laneGo.transform, "FinishPoint");
            finish.transform.localPosition = new Vector3(0f, 0f, FinishZ);

            SerializeField(lane, "startPoint", start.transform);
            SerializeField(lane, "finishPoint", finish.transform);
            SerializeField(lane, "laneWidth", LaneWidth);

            var gate = NewChild(group.transform, "FinishTrigger");
            gate.transform.localPosition = new Vector3(0f, 3f, FinishZ);
            var gateBox = gate.AddComponent<BoxCollider>();
            gateBox.isTrigger = true;
            gateBox.size = new Vector3(LaneWidth + 6f, 12f, 1.5f);
            SerializeField(gate.AddComponent<ChallengeZoneTrigger>(), "verdict", 0);   // Pass

            var fail = NewChild(group.transform, "FailVolume");
            fail.transform.localPosition = new Vector3(0f, -40f, 20f);
            var failBox = fail.AddComponent<BoxCollider>();
            failBox.isTrigger = true;
            failBox.size = new Vector3(400f, 10f, 400f);
            var failZone = fail.AddComponent<ChallengeZoneTrigger>();
            SerializeField(failZone, "verdict", 1);   // Fail
            SerializeField(failZone, "failReason", (int)ChallengeOutcomeReason.FellOutOfArena);

            return lane;
        }

        // ---------------------------------------------------------------- spike wall

        /// <summary>
        /// The crystal wall's collision.
        ///
        /// The crystals and their masonry now live in the ImpactZone macro; what stays here is the
        /// authored physics, which is the part gameplay depends on. One clean box for the impact
        /// face, because a ragdoll needs a predictable surface to bounce off rather than twenty
        /// crystal shards to snag on, plus a backstop so a hard enough launch cannot punch through.
        /// </summary>
        private static void BuildSpikeWall(Transform parent, out Collider wallCollider)
        {
            var group = NewChild(parent, "CrystalSpikeWall");
            group.transform.localPosition = new Vector3(0f, 0f, WallZ);

            // Flush against the back of the impact face. It used to sit 2.6 m further back and 24 m
            // tall; once the wall moved into the landing zone that left a slab standing where
            // ragdolls come down, and bones penetrating it were depenetrated hard enough to fling
            // units tens of kilometres.
            var backstop = NewChild(group.transform, "Backstop");
            backstop.transform.localPosition = new Vector3(0f, 3f, -0.95f);
            backstop.AddComponent<BoxCollider>().size = new Vector3(LaneWidth + 8f, 12f, 1.5f);

            var impact = NewChild(group.transform, "ImpactFace");
            impact.transform.localPosition = new Vector3(0f, 2.4f, 0.3f);
            var box = impact.AddComponent<BoxCollider>();
            box.size = new Vector3(LaneWidth + 1.5f, 5f, 1f);
            wallCollider = box;

            var mat = new PhysicsMaterial("CrystalWall") { bounciness = 0.45f, dynamicFriction = 0.35f };
            AssetDatabase.CreateAsset(mat, $"{MaterialRoot}/CrystalWall.physicMaterial");
            box.material = mat;
        }

        // ---------------------------------------------------------------- arm

        private static RotatingArmObstacle BuildArmObstacle(Transform parent)
        {
            var group = NewChild(parent, "RotatingArmObstacle");
            group.transform.localPosition = new Vector3(0f, ArmPivotHeight, ArmZ);

            // Explicit pivot child. The Arm asset happens to already have its origin at the
            // shoulder, but the obstacle should not depend on that: the pivot is what defines the
            // rotation centre and the local X axis the arm sweeps around.
            var pivot = NewChild(group.transform, "Pivot");
            var body = pivot.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var armSrc = LoadPrefab("Assets/Arm/model_0.prefab");
            if (armSrc != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(armSrc, pivot.transform);
                visual.name = "ArmVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * ArmScale;

                // The imported Arm prefab ships a SphereCollider on its shoulder ball. Left in place
                // on a kinematic, fast-rotating body it would resolve contacts on its own and fight
                // the authored impulse, so the visual stays purely visual.
                foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);

                // The shoulder ball is the arm's pivot and reads as the hazard's mechanism, so it
                // gets a warning accent. Purely a material swap - the strike volume is unchanged.
                var ball = visual.transform.childCount > 0 ? visual.transform.GetChild(0) : null;
                var ballRenderer = ball != null ? ball.GetComponent<MeshRenderer>() : null;
                if (ballRenderer != null) ballRenderer.sharedMaterial = ArenaMaterials.Load().hazard;
            }

            var obstacle = group.AddComponent<RotatingArmObstacle>();
            SerializeField(obstacle, "pivot", pivot.transform);
            SerializeField(obstacle, "pivotBody", body);
            SerializeField(obstacle, "rotationSpeed", ArmRotationSpeed);
            SerializeField(obstacle, "direction", 1);
            // Start pointing straight up. From 0 degrees the arm hangs over the start line and clubs
            // large units the instant they spawn, before they have run a step; from 270 it sweeps
            // down the far side first and meets the runner out on the lane.
            SerializeField(obstacle, "startAngle", 270f);
            SerializeField(obstacle, "strikeLocalStart", new Vector3(0f, 0f, 1.2f));
            SerializeField(obstacle, "strikeLocalEnd", new Vector3(0f, 0f, 5.2f));
            SerializeField(obstacle, "strikeRadius", 0.85f);
            SerializeField(obstacle, "impactStrength", 260f);
            SerializeField(obstacle, "referenceMass", 40f);
            SerializeField(obstacle, "massCompensation", 0.8f);
            SerializeField(obstacle, "upwardBias", 0.45f);

            return obstacle;
        }

        // ---------------------------------------------------------------- rosters

        /// <summary>
        /// Put each family's three contestants onto its bastion macro.
        ///
        /// The macro supplies the architecture and two sockets; everything animated is added here,
        /// because a SkinnedMeshRenderer cannot be baked into a combined mesh and the display units
        /// have to be switched off individually when a contestant is summoned.
        /// </summary>
        private static List<MonsterFamilyDisplay> BuildRosters(ChallengeShowCatalog catalog,
                                                               List<Transform> bastions)
        {
            var displays = new List<MonsterFamilyDisplay>();

            for (int i = 0; i < catalog.families.Length && i < bastions.Count; i++)
            {
                var family = catalog.families[i];
                var bastion = bastions[i];
                if (family == null || bastion == null) continue;

                bastion.gameObject.name = $"Bastion_{family.familyName.Replace(" ", "")}";

                var slotRoot = NewChild(bastion, "Slots");
                var display = bastion.gameObject.AddComponent<MonsterFamilyDisplay>();
                // Same formula the macro was sized from, so the roster always fits its stage.
                display.SlotSpacing = EnvironmentMacroLibrary.SlotSpacingFor(family);

                int index = 0, total = 0;
                foreach (var _ in family.ValidUnits) total++;

                foreach (var unit in family.ValidUnits)
                {
                    var slot = NewChild(slotRoot.transform, $"Slot{index + 1}_{unit.displayName.Replace(" ", "")}");
                    slot.transform.localPosition = display.SlotLocalPosition(index, total);

                    if (unit.sourcePrefab != null)
                    {
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(unit.sourcePrefab, slot.transform);
                        visual.name = $"Display_{unit.displayName}";
                        visual.transform.localPosition = Vector3.zero;
                        visual.transform.localRotation = display.DisplayRotation;
                        var animator = visual.GetComponentInChildren<Animator>();
                        if (animator != null) animator.applyRootMotion = false;
                    }

                    var tag = NewChild(slot.transform, "AbsentTag");
                    tag.transform.localPosition = new Vector3(0f, unit.height * 0.6f, 0f);
                    var tagText = tag.AddComponent<TextMeshPro>();
                    tagText.text = "IN ARENA";
                    tagText.fontSize = 4.5f;
                    tagText.alignment = TextAlignmentOptions.Center;
                    tagText.color = new Color(1f, 0.85f, 0.2f);
                    tag.GetComponent<RectTransform>().sizeDelta = new Vector2(9f, 2.5f);
                    tag.AddComponent<WorldLabelBillboard>();
                    tag.SetActive(false);

                    index++;
                }

                displays.Add(BindLabel(bastion, family, display, slotRoot.transform));
            }
            return displays;
        }

        /// <summary>
        /// Mount the family name on the bastion's label socket.
        ///
        /// Anchored to the architecture rather than floating in open sky, and sized so it reads
        /// without competing with the contestants standing under it.
        /// </summary>
        private static MonsterFamilyDisplay BindLabel(Transform bastion, MonsterFamilyDefinition family,
                                                      MonsterFamilyDisplay display, Transform slotRoot)
        {
            var socket = bastion.Find("Socket_Label");
            var labelAnchor = NewChild(bastion, "LabelAnchor");
            labelAnchor.transform.localPosition = socket != null ? socket.localPosition : new Vector3(0f, 4f, 0f);

            var label = labelAnchor.AddComponent<TextMeshPro>();
            label.text = family.familyName.ToUpperInvariant();
            label.fontSize = 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = family.displayColor;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.3f;
            label.outlineColor = new Color32(18, 20, 28, 255);
            labelAnchor.GetComponent<RectTransform>().sizeDelta = new Vector2(16f, 3.2f);
            labelAnchor.AddComponent<WorldLabelBillboard>();

            display.Bind(family, slotRoot, label, labelAnchor.transform);
            return display;
        }

        // ---------------------------------------------------------------- vfx + systems

        private static GooSplatterPool BuildVfx(Transform parent, Collider wallCollider)
        {
            var group = NewChild(parent, "VFX");
            var pool = group.AddComponent<GooSplatterPool>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChallengeShow/Prefabs/VFX/GooSplatter.prefab");
            if (prefab != null)
                SerializeField(pool, "splatterPrefab", prefab.GetComponent<ParticleSystem>());

            if (wallCollider != null)
            {
                var surface = wallCollider.gameObject.AddComponent<GooImpactSurface>();
                SerializeField(surface, "pool", pool);
                if (LastBuiltRecorder != null) SerializeField(surface, "recorder", LastBuiltRecorder);
            }
            return pool;
        }

        private static void BuildSystems(Transform parent, ChallengeShowCatalog catalog, ChallengeLane lane,
                                         List<MonsterFamilyDisplay> displays, RotatingArmObstacle arm)
        {
            var group = NewChild(parent, "Systems");

            var poolGo = NewChild(group.transform, "UnitPool");
            var pool = poolGo.AddComponent<ChallengeUnitPool>();

            var directorGo = NewChild(group.transform, "Director");
            var director = directorGo.AddComponent<ChallengeDirector>();
            SerializeField(director, "catalog", catalog);
            SerializeField(director, "lane", lane);
            SerializeField(director, "pool", pool);
            SerializeFieldArray(director, "familyDisplays", displays.ToArray());
            SerializeField(director, "arm", arm);
            LastBuiltDirector = director;

            var recorder = directorGo.AddComponent<ChallengeRunRecorder>();
            SerializeField(recorder, "director", director);
            SerializeField(recorder, "lane", lane);
            SerializeField(recorder, "arm", arm);
            LastBuiltRecorder = recorder;

            BuildCameras(parent, director, lane);
        }

        /// <summary>
        /// A single plain Camera driven by ChallengeCameraRig. Cinemachine was tried first, but for
        /// four fixed shots it added a blend/priority layer between the intent and the result
        /// without buying anything; a scripted rig makes the framing exact and easy to re-tune.
        /// </summary>
        private static void BuildCameras(Transform parent, ChallengeDirector director, ChallengeLane lane)
        {
            var group = NewChild(parent, "Cameras");

            var camGo = NewChild(group.transform, "Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 46f;
            cam.farClipPlane = 900f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var rig = camGo.AddComponent<ChallengeCameraRig>();
            SerializeField(rig, "director", director);
            SerializeField(rig, "lane", lane);
            // Must look DOWN the lane from behind the spawn. The roster layout is described in screen
            // space - Cat foreground left, Skeleton foreground right, Dog background centre - and
            // that only holds while +Z recedes into the frame. Swinging the camera out to the side
            // to open up the bridge silently rotated the whole roster: high-Z bastions swung to
            // screen left and Cat ended up on the right. Readability is bought with height and
            // pitch instead, which keeps the mapping intact.
            SerializeField(rig, "establishingPosition", new Vector3(-15f, 27f, -33f));
            SerializeField(rig, "establishingLookAt", new Vector3(2f, 2f, 30f));
            SerializeField(rig, "establishingFov", 46f);
            SerializeField(rig, "sideAngle", 78f);
            SerializeField(rig, "followDistance", 11f);
            SerializeField(rig, "followHeight", 4f);
            SerializeField(rig, "ragdollDistance", 13f);
            SerializeField(rig, "ragdollHeight", 5.5f);

            camGo.transform.position = new Vector3(-15f, 27f, -33f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(2f, 2f, 30f) - camGo.transform.position);
        }

        /// <summary>Summon beat and PASS/FAIL verdict, both driven off director events.</summary>
        private static void BuildPresentation(Transform parent, ChallengeShowCatalog catalog)
        {
            var group = NewChild(parent, "Presentation");
            var director = LastBuiltDirector;
            if (director == null) return;

            var summon = group.AddComponent<SummonPresenter>();
            SerializeField(summon, "director", director);
            SerializeField(summon, "catalog", catalog);
            var burst = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChallengeShow/Prefabs/VFX/SummonBurst.prefab");
            if (burst != null) SerializeField(summon, "burstPrefab", burst.GetComponent<ParticleSystem>());

            // The verdict lives on its own object so it can be positioned independently of the
            // presenter and simply switched off between attempts.
            var labelGo = NewChild(group.transform, "VerdictLabel");
            var label = labelGo.AddComponent<TextMeshPro>();
            label.text = "PASS!";
            label.fontSize = 9f;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.28f;
            label.outlineColor = new Color32(16, 18, 26, 255);
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 4f);
            labelGo.AddComponent<WorldLabelBillboard>();
            labelGo.SetActive(false);

            var verdict = group.AddComponent<VerdictPresenter>();
            SerializeField(verdict, "director", director);
            SerializeField(verdict, "label", label);
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject LoadPrefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[ChallengeShow] Missing prefab: {path}");
            return go;
        }

        private static void SerializeField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[ChallengeShow] No field '{field}' on {target.GetType().Name}"); return; }

            switch (value)
            {
                case float f: prop.floatValue = f; break;
                case int i when prop.propertyType == SerializedPropertyType.Enum: prop.enumValueIndex = i; break;
                case int i: prop.intValue = i; break;
                case Vector3 v: prop.vector3Value = v; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SerializeFieldArray<T>(Object target, string field, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
