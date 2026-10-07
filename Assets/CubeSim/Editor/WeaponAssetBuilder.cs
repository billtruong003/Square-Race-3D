using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Combat;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the weapon catalogue and its model library from the project's asset packs.
    ///
    /// The packs ship materials on the built-in Standard shader, which is broken under URP. The
    /// packs are left untouched; the conversion happens at spawn time in WeaponVisualFactory.
    /// </summary>
    public static class WeaponAssetBuilder
    {
        public const string LibraryPath = "Assets/CubeSim/Data/WeaponVisualLibrary.asset";

        /// <summary>
        /// One weapon's visual tuning. Base scale and pose are measured off the model's bounds at
        /// build time (packs disagree wildly about size and local axes), so only the per-context
        /// multipliers live here. None of this touches attack range or damage.
        /// </summary>
        private struct Spec
        {
            public string Id;
            public string Path;
            public float PickupScale;
            public float EquippedScale;
            public Vector3 EquippedEuler;
            public Vector3 Offset;
            /// <summary>Set to replace the measured pose (chunky models like a glove have no thin axis).</summary>
            public bool PoseOverride;
            public Vector3 Pose;

            public string ProjectilePath;
            public float ProjectileScale;
            public Vector3 ProjectileEuler;
            /// <summary>Metres the bullet model is normalised to along its long axis (0 = use ProjectileScale as-is).</summary>
            public float ProjectileLength;

            /// <summary>Renderer names to keep; every other part (sights, lamps, spare shells) is stripped into a clean prefab.</summary>
            public string[] Keep;

            /// <summary>Held as a mirrored pair, one each side (gloves).</summary>
            public bool Pair;
            public float PairOffset;

            /// <summary>
            /// Real-world length in metres. When set, the model is scaled to RealLength * WorldScale
            /// instead of the flat TargetLength, so a knife stays a knife next to a shotgun.
            /// </summary>
            public float RealLength;
        }

        /// <summary>
        /// On-screen length from real length, compressed with a square root so a knife (0.4 m) still
        /// reads at 2.4 m beside a 2 m cube while a rifle (1.2 m) stops at ~4.2 m instead of 7.
        /// </summary>
        private static float ScreenLength(float realMetres) => 5.0f * Mathf.Sqrt(realMetres);

        /// <summary>Every weapon is normalised to this length - reads big and juicy from above.</summary>
        private const float TargetLength = 6.0f;   // doubled by request: the knife must read from the top-down shot

        // The arsenal is exactly one weapon - the Kenney cooking knife - by request: no combat
        // knife, no other melee, no guns on any map. Laid flat with the full profile facing the
        // top-down camera; scale is measured off the model's bounds, never typed.
        private static readonly Spec[] Melee =
        {
            new Spec
            {
                Id = "Cleaver", Path = "Assets/KenneyDungeon/FBX format 1/cooking-knife.prefab",
                PickupScale = 1.0f, EquippedScale = 1.0f, EquippedEuler = Vector3.zero,
            },
            // Weapon Arena arsenal. Every model is normalised to TargetLength and laid flat by
            // MeasurePose, so a pad pickup and a held weapon both read from the top-down shot.
            new Spec { Id = "Knife",      Path = "Assets/POLYGON weapons/Assets_official/knife_prefab.prefab",            PickupScale = 0.9f, EquippedScale = 1f, RealLength = 0.4f },
            new Spec { Id = "Axe",        Path = "Assets/POLYGON weapons/Assets_official/fireaxe_prefab.prefab",          PickupScale = 0.9f,  EquippedScale = 1f, RealLength = 0.9f },
            new Spec { Id = "LaserSword", Path = "Assets/SciFiSword/Sword.prefab", PickupScale = 0.9f, EquippedScale = 1f, RealLength = 1.05f },
            new Spec { Id = "Chainsaw",   Path = "Assets/SawBlade/model_0.prefab",                                 PickupScale = 0.9f,  EquippedScale = 1f, RealLength = 0.7f },
            new Spec { Id = "Glove", Path = "Assets/BoxingGloves/Glove.prefab", PickupScale = 0.9f, EquippedScale = 1f, PoseOverride = true, Pose = new Vector3(0f, 0f, 180f),
                Pair = true, PairOffset = 0.85f, Offset = new Vector3(0f, -0.5f, 0f), RealLength = 0.16f },
            new Spec { Id = "Revolver", Path = "Assets/POLYGON weapons/Assets_official/revolver_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f, Keep = new[] { "revolver_mesh" },
                ProjectilePath = "Assets/POLYGON weapons/Assets_official/bullet_shells_assets/magnum_prefab.prefab", ProjectileLength = 0.9f, RealLength = 0.33f },
            new Spec { Id = "Shotgun", Path = "Assets/POLYGON weapons/Assets_official/pumpgun_a_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f, Keep = new[] { "pumpgun_a" },
                ProjectilePath = "Assets/POLYGON weapons/Assets_official/bullet_shells_assets/12gauge_prefab.prefab", ProjectileLength = 0.7f, RealLength = 0.78f },
            new Spec { Id = "SMG", Path = "Assets/POLYGON weapons/Assets_official/Buzzsub_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f, Keep = new[] { "vector" },
                ProjectilePath = "Assets/POLYGON weapons/Assets_official/bullet_shells_assets/9mm_prefab.prefab", ProjectileLength = 0.6f, RealLength = 0.65f },
            new Spec { Id = "Sniper", Path = "Assets/POLYGON weapons/Assets_official/Russian_sniper_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f, Keep = new[] { "SVD", "scope", "glass" }, RealLength = 1.22f },
            new Spec { Id = "Rocket", Path = "Assets/POLYGON weapons/Assets_official/rocket_launcher_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f,
                ProjectilePath = "Assets/POLYGON weapons/Assets_official/rocket_launcher_rocket_prefab.prefab", ProjectileLength = 1.8f, RealLength = 1.3f },
            new Spec { Id = "Grenade", Path = "Assets/POLYGON weapons/Assets_official/grenade_launcher_prefab.prefab", PickupScale = 0.9f, EquippedScale = 1f,
                Keep = new[] { "m32", "grenades_A", "grenades_B", "grenades_C", "grenades_D", "grenades_E", "grenades_F", "cap", "cap.001", "cap.002", "cap.003", "cap.004", "cap.005" },
                ProjectilePath = "Assets/POLYGON weapons/Assets_official/grenade_launcher_40mm_grenade_prefab.prefab", ProjectileLength = 0.8f, RealLength = 0.75f },
            new Spec { Id = "BombVest",   Path = "Assets/POLYGON weapons/Assets_official/Blast_set_prefab.prefab",        PickupScale = 0.9f, EquippedScale = 1f, RealLength = 0.55f },
            new Spec { Id = "Shield",     Path = "Assets/CubeSim/Prefabs/Weapons/EnergyShield.prefab", PickupScale = 0.55f, EquippedScale = 1f, RealLength = 0.8f, PoseOverride = true, Pose = Vector3.zero, Offset = new Vector3(0f, -0.9f, 0f) },
        };

        [MenuItem("CubeSim/Build Weapon Assets", priority = 12)]
        public static WeaponVisualLibrary BuildLibrary()
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var entries = new List<WeaponVisualLibrary.Entry>();
            AddEntries(entries, Melee);

            var library = AssetDatabase.LoadAssetAtPath<WeaponVisualLibrary>(LibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<WeaponVisualLibrary>();

            library.SetEntries(entries);

            if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CubeSim] Weapon library built with {entries.Count} models.");
            return AssetDatabase.LoadAssetAtPath<WeaponVisualLibrary>(LibraryPath);
        }

        /// <summary>
        /// Pose and size are measured, not typed: the model's longest bounds axis becomes +Z (the
        /// aim) and its thinnest becomes +Y (facing the camera) - so every weapon, from any pack,
        /// lies flat with its full profile up - and the longest extent comes back so the caller can
        /// normalise the model to <see cref="TargetLength"/> metres regardless of pack scale.
        /// </summary>
        private static Vector3 MeasurePose(GameObject prefab, out float longestExtent)
        {
            longestExtent = 1f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(instance);
                return Vector3.zero;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Object.DestroyImmediate(instance);

            Vector3 size = bounds.size;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            float[] extents = { size.x, size.y, size.z };

            int longest = 0, thinnest = 0;
            for (int i = 1; i < 3; i++)
            {
                if (extents[i] > extents[longest]) longest = i;
                if (extents[i] < extents[thinnest]) thinnest = i;
            }

            longestExtent = Mathf.Max(0.01f, extents[longest]);
            if (longest == thinnest) return Vector3.zero;

            // LookRotation maps +Z onto the long axis and +Y onto the thin one; the inverse is the
            // rotation that brings the model into the canonical pose.
            Quaternion pose = Quaternion.Inverse(Quaternion.LookRotation(axes[longest], axes[thinnest]));
            return pose.eulerAngles;
        }

        /// <summary>
        /// A pack gun ships with every attachment it can take (sights, lamps, spare shells). The
        /// clean variant keeps only the named parts and lives under Prefabs/Weapons.
        /// </summary>
        private static GameObject CleanPrefab(string id, GameObject source, string[] keep)
        {
            Directory.CreateDirectory("Assets/CubeSim/Prefabs/Weapons");
            string path = $"Assets/CubeSim/Prefabs/Weapons/{id}_clean.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var keepSet = new HashSet<string>(keep);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || keepSet.Contains(renderer.name)) continue;
                // A part is dropped unless a kept part hangs below it (the revolver's shell holder
                // is a child of the body, so "under a kept parent" is not a reason to keep it).
                bool holdsKept = false;
                foreach (Renderer child in renderer.GetComponentsInChildren<Renderer>(true))
                    if (child != renderer && keepSet.Contains(child.name)) { holdsKept = true; break; }
                if (!holdsKept) Object.DestroyImmediate(renderer.gameObject);
            }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        /// <summary>Exact-name prefab lookup (Epic Toon FX missiles live deep in the pack).</summary>
        private static GameObject FindPrefabByName(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static void AddEntries(List<WeaponVisualLibrary.Entry> entries, Spec[] specs)
        {
            foreach (Spec spec in specs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[CubeSim] Weapon model missing: {spec.Path}");
                    continue;
                }
                if (spec.Keep != null && spec.Keep.Length > 0) prefab = CleanPrefab(spec.Id, prefab, spec.Keep);

                GameObject projectile = null;
                if (!string.IsNullOrEmpty(spec.ProjectilePath))
                {
                    projectile = spec.ProjectilePath.StartsWith("Assets/")
                        ? AssetDatabase.LoadAssetAtPath<GameObject>(spec.ProjectilePath)
                        : FindPrefabByName(spec.ProjectilePath);
                    if (projectile == null)
                    {
                        Debug.LogWarning($"[CubeSim] Bullet model missing: {spec.ProjectilePath}");
                    }
                }

                Vector3 orientation = MeasurePose(prefab, out float longestExtent);
                if (spec.PoseOverride) orientation = spec.Pose;

                // Bullets are normalised the same way: long axis forward, sized in metres.
                float projectileScale = spec.ProjectileScale;
                Vector3 projectileEuler = spec.ProjectileEuler;
                if (projectile != null && spec.ProjectileLength > 0f)
                {
                    projectileEuler = MeasurePose(projectile, out float bulletExtent);
                    projectileScale = spec.ProjectileLength / bulletExtent;
                }

                float modelScale = spec.RealLength > 0f ? ScreenLength(spec.RealLength) / longestExtent : TargetLength / longestExtent;
                entries.Add(new WeaponVisualLibrary.Entry
                {
                    id = spec.Id,
                    prefab = prefab,
                    scale = modelScale,
                    pickupScale = Mathf.Max(0.01f, spec.PickupScale),
                    equippedScale = Mathf.Max(0.01f, spec.EquippedScale),
                    orientation = orientation,
                    equippedEuler = spec.EquippedEuler,
                    offset = spec.Offset,
                    projectilePrefab = projectile,
                    pair = spec.Pair,
                    pairOffset = spec.PairOffset > 0f ? spec.PairOffset : 1.5f,
                    projectileVisualScale = projectileScale > 0f ? projectileScale : 1f,
                    projectileEuler = projectileEuler
                });

                Debug.Log($"[CubeSim] Weapon '{spec.Id}': native {longestExtent:F2}m -> " +
                          $"scale {TargetLength / longestExtent:F2} for {TargetLength:F1}m.");
            }
        }

        /// <summary>
        /// The gameplay catalogue that matches the model library. Each entry keeps the archetype
        /// tuning; only the id ties it to a model.
        /// </summary>
        public static List<WeaponDefinition> BuildCatalog()
        {
            return new List<WeaponDefinition>
            {
                new WeaponDefinition
                {
                    id = "Cleaver", category = WeaponCategory.Melee,
                    damage = 1f, attackCooldown = 1.0f, attackRange = 2.0f, attackArc = 120f,
                    color = new Color(0.9f, 0.75f, 0.5f),
                    releaseMode = WeaponReleaseMode.AmmoBased, ammo = 7
                },
            };
        }
    }
}
