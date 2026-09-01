using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Batch authoring for the roster. Fifteen monsters is too many to configure by hand and far
    /// too many to keep consistent by hand, so every per-unit value is measured from the source
    /// prefab and written into a ScriptableObject that stays hand-editable afterwards.
    /// </summary>
    public static class ChallengeShowAuthoring
    {
        private const string MonsterPackRoot = "Assets/Monsters Ultimate Pack 03 Cute Series";
        private const string DataRoot = "Assets/ChallengeShow/Data";
        private const string UnitPrefabRoot = "Assets/ChallengeShow/Prefabs/Units";

        /// <summary>
        /// Family membership as the pack itself presents it. Grouping is not invented: the name
        /// prefixes are unambiguous, and the three burrowers share a distinctive moveset
        /// ("Head Only Idle", "Head Attack", "Underground") that no other family has.
        /// Evolution order within a family is decided by measured height, not by this list.
        /// </summary>
        private static readonly (string family, Color color, string[] members)[] Families =
        {
            ("Cactus",   new Color(0.35f, 0.82f, 0.30f), new[] { "Cacti", "Cactus", "Cactus Boss" }),
            ("Cat",      new Color(0.98f, 0.78f, 0.20f), new[] { "Cat Meow", "Cat Lightning", "Cat Bolt" }),
            ("Dog",      new Color(0.98f, 0.45f, 0.18f), new[] { "Dog Pup", "Dog Bark", "Dog Bowwow" }),
            ("Mole Rat", new Color(0.80f, 0.36f, 0.30f), new[] { "Burrow", "Mole Rat", "Mole Rat King" }),
            ("Skeleton", new Color(0.72f, 0.62f, 0.95f), new[] { "Skeleton", "Skeleton Mage", "Skeleton Giant" })
        };

        /// <summary>Run-state candidates in preference order; rigs disagree on what "run" is called.</summary>
        private static readonly string[] RunStateCandidates =
        {
            "Run Forward In Place",
            "Creep Dash Forward In Place",
            "Fly Forward In Place",
            "Walk Forward In Place",
            "Creep Walk Forward In Place"
        };

        [MenuItem("Challenge Show/1. Generate Unit + Family Data")]
        public static void GenerateData()
        {
            EnsureFolder(DataRoot + "/Units");
            EnsureFolder(DataRoot + "/Families");

            var familyAssets = new List<MonsterFamilyDefinition>();

            foreach (var (familyName, color, members) in Families)
            {
                var measured = members
                    .Select(m => (name: m, prefab: FindMonsterPrefab(m)))
                    .Where(t => t.prefab != null)
                    .Select(t => (t.name, t.prefab, metrics: Measure(t.prefab)))
                    .OrderBy(t => t.metrics.height)     // base form first, final evolution last
                    .ToList();

                var unitAssets = new List<ChallengeUnitDefinition>();
                for (int i = 0; i < measured.Count; i++)
                {
                    var m = measured[i];
                    unitAssets.Add(CreateOrUpdateUnit(m.name, m.prefab, m.metrics, i + 1));
                }

                var family = LoadOrCreate<MonsterFamilyDefinition>($"{DataRoot}/Families/Family_{Sanitise(familyName)}.asset");
                family.familyName = familyName;
                family.displayColor = color;
                family.units = unitAssets.ToArray();
                EditorUtility.SetDirty(family);
                familyAssets.Add(family);
            }

            var catalog = LoadOrCreate<ChallengeShowCatalog>($"{DataRoot}/ChallengeShowCatalog.asset");
            catalog.families = familyAssets.ToArray();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChallengeShow] Generated {familyAssets.Count} families / {catalog.TotalUnits} units.");
        }

        [MenuItem("Challenge Show/2. Build Gameplay Unit Prefabs")]
        public static void BuildUnitPrefabs()
        {
            EnsureFolder(UnitPrefabRoot);
            var catalog = AssetDatabase.LoadAssetAtPath<ChallengeShowCatalog>($"{DataRoot}/ChallengeShowCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[ChallengeShow] Run step 1 first — no catalog found.");
                return;
            }

            int built = 0;
            foreach (var family in catalog.families)
            {
                if (family == null) continue;
                foreach (var unit in family.ValidUnits)
                {
                    if (BuildUnitPrefab(unit)) built++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChallengeShow] Built {built} gameplay unit prefabs.");
        }

        private static bool BuildUnitPrefab(ChallengeUnitDefinition definition)
        {
            if (definition.sourcePrefab == null) return false;

            string path = $"{UnitPrefabRoot}/CU_{Sanitise(definition.displayName)}.prefab";

            // A variant of the vendor prefab: our components live on the variant, the vendor asset
            // is never touched, and re-importing the pack still propagates through.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.sourcePrefab);
            instance.name = $"CU_{definition.displayName}";

            var body = ComponentUtility.GetOrAdd<Rigidbody>(instance);
            body.mass = definition.mass;
            body.linearDamping = definition.linearDamping;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var capsule = ComponentUtility.GetOrAdd<CapsuleCollider>(instance);
            capsule.direction = 1;
            capsule.radius = definition.colliderRadius;
            capsule.height = definition.colliderHeight;
            capsule.center = new Vector3(0f, definition.colliderCenterY, 0f);

            // Locomotion belongs to the motor, never to the animation.
            //
            // Twelve of the fifteen vendor rigs ship with applyRootMotion enabled, which lets the
            // Animator drive the transform directly and fight the velocity the motor writes every
            // FixedUpdate. The effect is unmistakable once measured: those units peaked at 0.2 m/s
            // against a 6.2 m/s target and never left the spawn, while the only three rigs that
            // happened to ship with it disabled - Cacti, Cactus and Burrow - ran normally, and
            // Burrow was the single unit that reached the finish. The animation is purely visual
            // here; the Rigidbody owns the position.
            var rigAnimator = instance.GetComponentInChildren<Animator>(true);
            if (rigAnimator != null) rigAnimator.applyRootMotion = false;

            ComponentUtility.GetOrAdd<ChallengeUnitMotor>(instance);
            ComponentUtility.GetOrAdd<ChallengeUnitRagdoll>(instance);
            // Measures joint separation and peak bone velocity during ragdoll. Read-only and cheap
            // (one loop over ~15 joints per physics step, and only one unit is ever active), but it
            // is the only way to tell whether a limb "looks detached" or actually is.
            ComponentUtility.GetOrAdd<RagdollStretchProbe>(instance);
            var unit = ComponentUtility.GetOrAdd<ChallengeUnit>(instance);
            unit.SetDefinition(definition);

            int bones = RagdollBuilder.Build(instance, definition);

            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            definition.gameplayPrefab = saved;
            EditorUtility.SetDirty(definition);

            Debug.Log($"[ChallengeShow] {definition.displayName}: {bones} ragdoll bodies, " +
                      $"mass {definition.mass:0.0}, capsule r={definition.colliderRadius:0.00} h={definition.colliderHeight:0.00}");
            return true;
        }

        // --- measurement ---

        private struct Metrics
        {
            public float height, width, depth;
            public int boneCount;
            public bool isHumanoid;
            public string runState, idleState;
            public bool hasLegs;
        }

        private static Metrics Measure(GameObject prefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }

            var animator = inst.GetComponentInChildren<Animator>(true);
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);

            var m = new Metrics
            {
                height = bounds.size.y,
                width = bounds.size.x,
                depth = bounds.size.z,
                boneCount = smr != null ? smr.bones.Length : 0,
                isHumanoid = animator != null && animator.isHuman,
                runState = ResolveState(animator, RunStateCandidates),
                idleState = ResolveState(animator, new[] { "Idle", "Idle Happy", "Still" }),
                hasLegs = inst.GetComponentsInChildren<Transform>(true)
                              .Any(t => t.name.Contains("Leg") || t.name.Contains("Thigh"))
            };

            Object.DestroyImmediate(inst);
            return m;
        }

        private static string ResolveState(Animator animator, string[] candidates)
        {
            var controller = animator != null ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
            if (controller == null || controller.layers.Length == 0) return candidates[0];

            var names = new HashSet<string>(controller.layers[0].stateMachine.states.Select(s => s.state.name));
            foreach (var c in candidates)
                if (names.Contains(c)) return c;
            return candidates[0];
        }

        private static ChallengeUnitDefinition CreateOrUpdateUnit(string name, GameObject prefab, Metrics m, int stage)
        {
            var def = LoadOrCreate<ChallengeUnitDefinition>($"{DataRoot}/Units/Unit_{Sanitise(name)}.asset");

            def.displayName = name;
            def.sourcePrefab = prefab;
            def.evolutionStage = stage;
            def.height = m.height;

            // Arms and wings inflate the X extent, so body width uses the narrower horizontal axis.
            def.bodyWidth = Mathf.Min(m.width, m.depth);

            def.colliderRadius = Mathf.Clamp(def.bodyWidth * 0.35f, 0.12f, m.height * 0.45f);
            def.colliderHeight = Mathf.Max(m.height, def.colliderRadius * 2f + 0.01f);
            def.colliderCenterY = def.colliderHeight * 0.5f;

            // Mass scales with height squared rather than true volume. A cubic law gave a 55x
            // spread across the roster, which pushed both extremes outside anything the arm could
            // produce a watchable result against; a squared law keeps a clear ~11x spread that the
            // arm's mass compensation can still work with.
            def.mass = Mathf.Round(Mathf.Clamp(12f * m.height * m.height, 8f, 150f));

            def.locomotion = !m.hasLegs ? ChallengeUnitDefinition.Locomotion.Hopper
                           : m.depth > m.width * 1.05f ? ChallengeUnitDefinition.Locomotion.Quadruped
                           : ChallengeUnitDefinition.Locomotion.Biped;

            // Bigger units stride further per second but accelerate more slowly.
            def.moveSpeed = Mathf.Round(Mathf.Clamp(2.6f + m.height * 0.85f, 3f, 6.5f) * 10f) / 10f;
            def.acceleration = Mathf.Round(Mathf.Clamp(26f / Mathf.Max(0.6f, m.height), 4f, 20f) * 10f) / 10f;

            // Bigger bodies need a larger speed change before they go down; smaller ones are
            // launched harder by the impulse that does land.
            def.stabilityVelocity = Mathf.Round(Mathf.Clamp(2.2f + m.height * 1.1f, 2.5f, 7f) * 10f) / 10f;

            // Durability. The arm delivers roughly 5.5-9.2 m/s of dV across the roster, so the
            // damage threshold sits well under the light end (everyone takes real hits) and
            // fullHitDeltaV sits near the middle of that band, which keeps a clean strike inside
            // the 0.75-1.25 clamp and makes ~3 strikes the normal outcome.
            def.hitDamageThreshold = Mathf.Round(Mathf.Clamp(2.0f + m.height * 0.35f, 2.0f, 4.0f) * 10f) / 10f;
            def.fullHitDeltaV = Mathf.Round(Mathf.Clamp(6.0f + m.height * 0.55f, 6.0f, 9.0f) * 10f) / 10f;
            // Heavier units soak one extra strike; nothing here decides a winner, it only sets how
            // long each monster stays entertaining.
            def.toughness = Mathf.Round(Mathf.Clamp(2.9f + m.height * 0.22f, 2.9f, 3.9f) * 10f) / 10f;

            // Recovery. Legless flyers hover instead of standing on the deck.
            // Long enough for the launch to actually finish travelling. At 0.45 s recovery fired
            // while units were still sliding and cut the knockback from ~10 m to ~4.5 m,
            // which put the crystal wall permanently out of reach.
            def.recoveryMinimumRagdollTime = 0.85f;
            def.recoveryImmunity = 0.95f;
            def.recoverGroundOffset = m.hasLegs ? 0f : Mathf.Round(m.height * 0.12f * 100f) / 100f;
            def.knockbackMultiplier = Mathf.Round(Mathf.Clamp(1.35f - m.height * 0.12f, 0.75f, 1.3f) * 100f) / 100f;
            def.ragdollImpulseMultiplier = Mathf.Round(Mathf.Clamp(1.5f - m.height * 0.12f, 0.85f, 1.4f) * 100f) / 100f;

            // Only one unit competes at a time, so a slightly larger budget costs nothing and lets
            // symmetric pairs (both collarbones, both arms) finish instead of cutting off mid-level.
            def.ragdollBoneBudget = Mathf.Clamp(Mathf.RoundToInt(m.boneCount * 0.4f), 6, 16);
            def.runStateName = m.runState;
            def.idleStateName = m.idleState;

            EditorUtility.SetDirty(def);
            return def;
        }

        // --- helpers ---

        private static GameObject FindMonsterPrefab(string monsterName)
        {
            string expected = $"/{monsterName}.prefab";
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { MonsterPackRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(expected)) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.LogWarning($"[ChallengeShow] Prefab not found for '{monsterName}'.");
            return null;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitise(string s) => s.Replace(" ", "");
    }
}
