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

        private const string Polygon = "Assets/POLYGON weapons/Assets_official/";

        /// <summary>
        /// One weapon's visual tuning. The packs disagree wildly about model size and local axes -
        /// a POLYGON pistol is 0.39 m long down +Z, an RPG spear is 1.37 m tall up +Y - so every
        /// value here is measured per weapon rather than shared. <see cref="Scale"/> normalises the
        /// model to a common ~1.5 m length - about a third of a racer, which is the smallest a weapon
        /// can be and still be identifiable from directly above - and the pickup and equipped
        /// multipliers then size it for each presentation. None of this touches attack range or damage.
        /// </summary>
        private struct Spec
        {
            public string Id;
            public string Path;
            public float Scale;
            public Vector3 Orientation;
            public float PickupScale;
            public float EquippedScale;
            public Vector3 EquippedEuler;
            public Vector3 Offset;

            public string ProjectilePath;
            public float ProjectileScale;
            public Vector3 ProjectileEuler;
        }

        // The arsenal is exactly two knives - the POLYGON combat knife and the Kenney cooking
        // knife - by request: no other melee and no guns on any map. Both are laid flat with the
        // full profile facing the top-down camera; scales target ~2m of model, half a racer.
        private static readonly Spec[] Melee =
        {
            new Spec
            {
                Id = "Knife", Path = Polygon + "knife_prefab.prefab",
                Scale = 5.2f, Orientation = new Vector3(90f, 0f, 0f),
                PickupScale = 1.1f, EquippedScale = 0.95f, EquippedEuler = Vector3.zero,
            },
            new Spec
            {
                Id = "Cleaver", Path = "Assets/KenneyDungeon/FBX format 1/cooking-knife.prefab",
                Scale = 2.8f, Orientation = new Vector3(0f, -90f, 0f),
                PickupScale = 1.1f, EquippedScale = 1.0f, EquippedEuler = Vector3.zero,
            },
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
        /// Orientation is measured, not typed: the model's longest bounds axis becomes +Z (the aim)
        /// and its thinnest becomes +Y (facing the camera) - so every weapon, from any pack, lies
        /// flat with its full profile up. The hand-tuned eulers this replaces kept half the arsenal
        /// edge-on.
        /// </summary>
        private static Vector3 MeasureOrientation(GameObject prefab)
        {
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

            if (longest == thinnest) return Vector3.zero;

            // LookRotation maps +Z onto the long axis and +Y onto the thin one; the inverse is the
            // rotation that brings the model into the canonical pose.
            Quaternion pose = Quaternion.Inverse(Quaternion.LookRotation(axes[longest], axes[thinnest]));
            return pose.eulerAngles;
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

                GameObject projectile = null;
                if (!string.IsNullOrEmpty(spec.ProjectilePath))
                {
                    projectile = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ProjectilePath);
                    if (projectile == null)
                    {
                        Debug.LogWarning($"[CubeSim] Bullet model missing: {spec.ProjectilePath}");
                    }
                }

                entries.Add(new WeaponVisualLibrary.Entry
                {
                    id = spec.Id,
                    prefab = prefab,
                    scale = spec.Scale,
                    pickupScale = Mathf.Max(0.01f, spec.PickupScale),
                    equippedScale = Mathf.Max(0.01f, spec.EquippedScale),
                    orientation = MeasureOrientation(prefab),
                    equippedEuler = spec.EquippedEuler,
                    offset = spec.Offset,
                    projectilePrefab = projectile,
                    projectileVisualScale = spec.ProjectileScale > 0f ? spec.ProjectileScale : 1f,
                    projectileEuler = spec.ProjectileEuler
                });
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
                    id = "Knife", category = WeaponCategory.Melee,
                    damage = 1f, attackCooldown = 0.7f, attackRange = 1.9f, attackArc = 130f,
                    color = new Color(0.85f, 0.88f, 0.92f),
                    releaseMode = WeaponReleaseMode.TimeBased, holdDuration = 8f
                },
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
