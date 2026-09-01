using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Thin wrapper around the Kenney mini-dungeon FBX kit.
    ///
    /// Everything the arena builder needs to know about the vendor package lives here so the
    /// builder can talk in terms of "put a wall at this world position" instead of repeating the
    /// kit's quirks at every call site. Three of those quirks matter:
    ///
    /// 1. Every pivot is at the BASE CENTRE, so a piece placed at y occupies y .. y + height*scale.
    ///    Hanging a chunk beneath a deck therefore needs the height backed off the pivot, which is
    ///    what <see cref="PutHanging"/> exists for. Getting this wrong makes understructure erupt
    ///    through the floor.
    /// 2. Pieces are authored on a 1-unit grid with no colliders and scaleFactor 1.
    /// 3. Each FBX embeds its own material instance even though all 30 share one 512px atlas, so a
    ///    naive build produces 30 materials and 30 draw calls. Placement re-points every renderer
    ///    at one project-owned material instead. The vendor import settings are left untouched.
    /// </summary>
    public static class KenneyKit
    {
        public const string Root = "Assets/KenneyDungeon";
        private const string TexturePath = Root + "/Textures/colormap.png";
        private const string SharedMaterialPath = "Assets/ChallengeShow/Materials/DungeonKit.mat";

        // --- piece names, so typos fail at compile time rather than silently placing nothing ---
        public const string Floor = "floor";
        public const string FloorDetail = "floor-detail";
        public const string Dirt = "dirt";
        public const string Rocks = "rocks";
        public const string Stones = "stones";
        public const string Wall = "wall";
        public const string WallHalf = "wall-half";
        public const string WallNarrow = "wall-narrow";
        public const string WallOpening = "wall-opening";
        public const string Gate = "gate";
        public const string Column = "column";
        public const string Stairs = "stairs";
        public const string WoodStructure = "wood-structure";
        public const string WoodSupport = "wood-support";
        public const string Banner = "banner";
        public const string Barrel = "barrel";
        public const string Pot = "pot";
        public const string Chest = "chest";

        /// <summary>
        /// Native bounds heights, measured from the imported meshes during the package audit.
        ///
        /// Cached as constants rather than measured per call because a build places well over a
        /// thousand pieces and every lookup would otherwise instantiate and destroy a prefab.
        /// </summary>
        private static readonly Dictionary<string, float> Heights = new()
        {
            { Floor, 0.001f }, { FloorDetail, 0.05f }, { Dirt, 0.90f }, { Rocks, 0.50f },
            { Stones, 0.45f }, { Wall, 1.10f }, { WallHalf, 1.00f }, { WallNarrow, 1.00f },
            { WallOpening, 1.00f }, { Gate, 0.75f }, { Column, 1.10f }, { Stairs, 0.90f },
            { WoodStructure, 1.00f }, { WoodSupport, 1.00f }, { Banner, 0.65f },
            { Barrel, 0.60f }, { Pot, 0.45f }, { Chest, 0.55f },
        };

        /// <summary>
        /// Which tinted variant of the kit atlas a piece should wear.
        ///
        /// The whole kit shares one texture, so without this every surface in the arena renders the
        /// same grey and the eye has nothing to latch onto - the lane, the bastions and the debris
        /// all read as one undifferentiated mass. Two tints over the same atlas cost one extra
        /// material and give the shot a foreground/background split.
        /// </summary>
        public enum Palette
        {
            /// <summary>Cool blue-grey. Bastions, courts, gates - everything that is not the lane.</summary>
            Stone,
            /// <summary>Warmer, slightly darker. The trial bridge, so the running line reads as its own object.</summary>
            Lane,
        }

        private static readonly Dictionary<Palette, Material> Materials = new();

        /// <summary>Native height of a piece at scale 1, used for pivot-correct vertical placement.</summary>
        public static float HeightOf(string piece) => Heights.TryGetValue(piece, out float h) ? h : 1f;

        /// <summary>
        /// One URP/Lit material over the kit's atlas, shared by every placed piece.
        ///
        /// Created in the project rather than edited inside the vendor FBXs, so re-importing the
        /// package cannot clobber it and the vendor folder stays pristine.
        /// </summary>
        public static Material SharedMaterial(Palette palette = Palette.Stone)
        {
            if (Materials.TryGetValue(palette, out var cached) && cached != null) return cached;

            string path = palette == Palette.Lane
                ? SharedMaterialPath.Replace(".mat", "_Lane.mat")
                : SharedMaterialPath;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader) { name = palette == Palette.Lane ? "DungeonKit_Lane" : "DungeonKit" };
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            // The kit is flat-shaded stylised art; specular response fights the toon-lit monsters.
            mat.SetFloat("_Smoothness", 0.06f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetColor("_BaseColor", palette == Palette.Lane
                ? new Color(1.00f, 0.90f, 0.78f)     // warm, so the bridge advances
                : new Color(0.76f, 0.83f, 1.00f));   // cool, so the architecture recedes

            Materials[palette] = mat;
            return mat;
        }

        private static GameObject Source(string piece) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{piece}.fbx");

        /// <summary>
        /// Place a kit piece. Returns null (and warns once) if the package is missing the piece, so
        /// a partial package degrades into a sparser arena rather than a null-reference build.
        /// </summary>
        public static GameObject Put(Transform parent, string piece, Vector3 localPos,
                                     float yaw = 0f, float scale = 1f, Palette palette = Palette.Stone)
        {
            var src = Source(piece);
            if (src == null)
            {
                Debug.LogWarning($"[ChallengeShow] Kenney piece missing: {piece}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            Repaint(go, palette);
            return go;
        }

        /// <summary>Place with a full euler, for rubble that should tilt off-axis.</summary>
        public static GameObject Put(Transform parent, string piece, Vector3 localPos,
                                     Vector3 euler, Vector3 scale, Palette palette = Palette.Stone)
        {
            var src = Source(piece);
            if (src == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Repaint(go, palette);
            return go;
        }

        /// <summary>
        /// Place a piece so its TOP lands at <paramref name="topY"/> instead of its base.
        ///
        /// This is the one the understructure uses: chunks have to hang below a deck, and with a
        /// base pivot that means solving y = topY - height*scale rather than just setting y.
        /// </summary>
        public static GameObject PutHanging(Transform parent, string piece, float x, float topY, float z,
                                            float yaw, float scale, Palette palette = Palette.Stone)
        {
            float y = topY - HeightOf(piece) * scale;
            return Put(parent, piece, new Vector3(x, y, z), yaw, scale, palette);
        }

        /// <summary>Re-point every renderer at the shared material and strip vendor colliders.</summary>
        private static void Repaint(GameObject go, Palette palette)
        {
            var mat = SharedMaterial(palette);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
            }
            // The kit imports without colliders, but instantiating a prefab variant could carry
            // one; the arena's collision is authored deliberately and must stay the only source.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
    }
}
