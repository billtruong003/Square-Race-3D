using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Picks the Epic Toon FX prefabs CubeSim uses and writes them into a library.
    ///
    /// Prefabs are resolved by name rather than by path: the pack nests them several folders deep
    /// (Combat/Explosions/SparkleExplosion/...) and hard-coded paths broke immediately. Nothing in
    /// the pack is modified.
    ///
    /// Scales are large because the pack is authored for a first-person camera; at this top-down
    /// framing the stock sizes are a handful of pixels. The four combat effects run larger still -
    /// they are the ones a viewer has to read to follow who shot whom.
    /// </summary>
    public static class VfxAssetBuilder
    {
        public const string LibraryPath = "Assets/CubeSim/Data/VfxLibrary.asset";

        private struct Pick
        {
            public VfxId Id;
            public string PrefabName;
            public float Scale;
            public float Lifetime;
            public VfxTintMode Tint;
            public float Height;
        }

        private static readonly Pick[] Picks =
        {
            // Ranged flow: flash at the muzzle, spark on a wall, coloured burst on a racer.
            new Pick { Id = VfxId.MuzzleFlash,       PrefabName = "StandardMuzzleYellow",   Scale = 5.0f, Lifetime = 0.5f,  Tint = VfxTintMode.Accent, Height = 0.8f },
            new Pick { Id = VfxId.ProjectileHitWall, PrefabName = "HitDustExplosion",       Scale = 3.0f, Lifetime = 1.0f,  Tint = VfxTintMode.None,   Height = 0.7f },
            new Pick { Id = VfxId.ProjectileHitRacer,PrefabName = "RoundHitYellow",         Scale = 5.0f, Lifetime = 0.9f,  Tint = VfxTintMode.Accent, Height = 0.9f },

            // Melee flow: a slash on every swing, a hit burst only when damage lands.
            new Pick { Id = VfxId.MeleeSlash,        PrefabName = "SwordSlashThinWhite",    Scale = 6.0f, Lifetime = 0.7f,  Tint = VfxTintMode.Full,   Height = 0.9f },
            // Blood, not sparks: a directional splat on every hit that lands.
            new Pick { Id = VfxId.MeleeHit,          PrefabName = "BloodSplatDirectional",  Scale = 4.5f, Lifetime = 1.2f,  Tint = VfxTintMode.None,   Height = 0.9f },

            // Weapon circulation.
            new Pick { Id = VfxId.WeaponPickup,      PrefabName = "SparkleExplosionYellow", Scale = 3.0f, Lifetime = 1.2f,  Tint = VfxTintMode.Accent, Height = 0.8f },
            new Pick { Id = VfxId.WeaponDrop,        PrefabName = "SparkleExplosionBlue",   Scale = 2.4f, Lifetime = 1.0f,  Tint = VfxTintMode.Accent, Height = 0.6f },

            // Eliminations and outcomes.
            new Pick { Id = VfxId.RacerDeath,        PrefabName = "BloodExplosion",         Scale = 4.0f, Lifetime = 1.8f,  Tint = VfxTintMode.None,   Height = 0.8f },
            // The pool stays on the floor for most of a round - the arena remembers its dead.
            new Pick { Id = VfxId.BloodPool,         PrefabName = "BloodPoolGrowing",       Scale = 3.5f, Lifetime = 40f,  Tint = VfxTintMode.None,   Height = 0.06f },
            new Pick { Id = VfxId.CrushDeath,        PrefabName = "HitDustExplosion",       Scale = 4.5f, Lifetime = 1.3f,  Tint = VfxTintMode.None,   Height = 0.7f },
            new Pick { Id = VfxId.GoalReached,       PrefabName = "ConfettiBlastRainbow",   Scale = 3.5f, Lifetime = 3.0f,  Tint = VfxTintMode.None,   Height = 1.2f },
            new Pick { Id = VfxId.WallBreak,         PrefabName = "HitDustExplosion",       Scale = 5.5f, Lifetime = 1.4f,  Tint = VfxTintMode.None,   Height = 1.0f },
        };

        [MenuItem("CubeSim/Build VFX Library", priority = 13)]
        public static VfxLibrary BuildLibrary()
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var entries = new List<VfxLibrary.Entry>();
            int missing = 0;

            foreach (Pick pick in Picks)
            {
                GameObject prefab = FindPrefab(pick.PrefabName);
                if (prefab == null)
                {
                    Debug.LogWarning($"[CubeSim] Epic Toon FX prefab '{pick.PrefabName}' not found; " +
                                     $"{pick.Id} will be silent.");
                    missing++;
                    continue;
                }

                entries.Add(new VfxLibrary.Entry
                {
                    id = pick.Id,
                    prefab = prefab,
                    scale = pick.Scale,
                    lifetime = pick.Lifetime,
                    tint = pick.Tint,
                    heightOffset = pick.Height
                });
            }

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<VfxLibrary>();

            library.SetEntries(entries);

            if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CubeSim] VFX library built with {entries.Count} effects ({missing} missing).");
            return AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
        }

        /// <summary>Exact-name lookup inside the Epic Toon FX pack, wherever it happens to sit.</summary>
        private static GameObject FindPrefab(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:GameObject", new[] { "Assets/Epic Toon FX" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }
    }
}
