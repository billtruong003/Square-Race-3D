using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CubeSim.Arena.Authored;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// The device markers of the ASCII map contract, built into an authored arena prefab:
    ///
    ///   S  saw blade   - a square rect spins in place; an elongated rect is a rail the blade rides
    ///   P  crusher     - the rect is the travel; a slab half its length slams end to end
    ///   T  spike trap  - every cell gets a spike plate on the trap's clock
    ///   U  bumper      - one barrel per rect, radius from the rect
    ///   &gt; &lt; ^ v  conveyor floor dragging in that direction
    ///   K  locked gate - a wall that drops when a key is taken (Kenney gate model)
    ///   k  key         - one per rect
    ///   $  coin        - one per cell
    ///   +  potion      - one per rect
    ///   1 2 teleporter pads - pairs by digit
    ///
    /// Visuals are baked here (models fitted to the cells, props material) so the runtime
    /// <see cref="Core.ArenaDeviceSystem"/> only ever animates transforms it finds by name.
    /// </summary>
    public static class AsciiDeviceBuilder
    {
        private const string SawPrefab = "Assets/SawBlade/model_0.prefab";
        private const string Trap = "Assets/KenneyDungeon/trap.fbx";
        private const string Barrel = "Assets/KenneyDungeon/barrel.fbx";
        private const string GateModel = "Assets/KenneyDungeon/gate.fbx";
        private const string KeyModel = "Assets/KenneyDungeon/key.fbx";
        private const string CoinModel = "Assets/KenneyDungeon/coin.fbx";
        private const string PotionModel = "Assets/KenneyDungeon/potion.fbx";
        private const string Colormap = "Assets/KenneyDungeon/Textures/colormap.png";
        private const string Pack2 = "Assets/KenneyDungeon/FBX format 2/";
        private const string Colormap2 = Pack2 + "Textures/colormap.png";
        private const string ConveyorTexture = Pack2 + "Textures/images.jpg";   // herringbone rubber belt (user pick)
        private const string DoorModel = Pack2 + "gate-door.fbx";
        private const string LockedGateModel = Pack2 + "gate-lasers.fbx";
        private const string Props2MaterialPath = "Assets/CubeSim/Visuals/Props/CubeSimProps2.mat";
        private const string ConveyorMaterialPath = "Assets/CubeSim/Visuals/Props/ConveyorBelt.mat";
        private const string PortalMaterialPath = "Assets/CubeSim/Visuals/Props/Portal.mat";
        private const string PropMaterialPath = "Assets/CubeSim/Visuals/Props/CubeSimProps.mat";
        private const string BladeMaterialPath = "Assets/CubeSim/Visuals/Rotors/Blade.mat";

        public static void Build(Transform root, string[] grid, int columns, int rows,
            float cellW, float cellH, AsciiArenaBuilder.Settings settings)
        {
            var holder = new GameObject("Devices").transform;
            holder.SetParent(root, false);

            float cell = Mathf.Min(cellW, cellH);
            Material props = GetPropMaterial();
            int index = 0;

            // ---------------------------------------------------------------- S saw blades
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'S'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                float shortSide = Mathf.Min(rect.width, rect.height);
                float longSide = Mathf.Max(rect.width, rect.height);
                bool rail = longSide - shortSide > cell * 0.5f;
                Vector3 axis = rect.width >= rect.height ? Vector3.right : Vector3.forward;
                float radius = shortSide * 0.5f;

                var centre = new Vector3(rect.center.x, 0f, rect.center.y);
                Vector3 start = rail ? centre - axis * (longSide * 0.5f - radius) : centre;
                Vector3 end = rail ? centre + axis * (longSide * 0.5f - radius) : centre;

                var go = new GameObject($"Saw_{index:D2}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(start.x, 0.35f, start.z);

                var blade = go.AddComponent<SawBlade>();
                blade.Configure(radius, index % 2 == 0 ? 420f : -420f, start, end, 5f, index * 1.7f);
                blade.SetDamage(settings.SawDamage);

                var spin = new GameObject("Spin").transform;
                spin.SetParent(go.transform, false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SawPrefab);
                if (prefab != null)
                {
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    model.name = "Blade";
                    StripColliders(model);
                    model.transform.SetParent(spin, false);
                    model.transform.localScale = Vector3.one * (radius * 2f / 0.69f);
                }

                // A dark hub so a rail blade reads as a machine, not a floating disc.
                GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hub.name = "Hub";
                StripColliders(hub);
                hub.transform.SetParent(go.transform, false);
                hub.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                hub.transform.localScale = new Vector3(radius * 0.5f, 0.12f, radius * 0.5f);
                Material bladeMat = GetBladeMaterial();
                if (bladeMat != null) hub.GetComponent<MeshRenderer>().sharedMaterial = bladeMat;

                if (rail) BuildRailTrack(holder, start, end, radius, bladeMat);
                index++;
            }

            // ---------------------------------------------------------------- P crushers
            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'P'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                bool alongX = rect.width >= rect.height;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                float longSide = Mathf.Max(rect.width, rect.height);
                float shortSide = Mathf.Min(rect.width, rect.height);
                float bodyLength = longSide * 0.5f;

                var centre = new Vector3(rect.center.x, settings.WallHeight * 0.5f, rect.center.y);
                Vector3 rest = centre - axis * (longSide * 0.5f - bodyLength * 0.5f);
                Vector3 travel = axis * (longSide - bodyLength);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = $"Crusher_{index:D2}";
                body.transform.SetParent(holder, false);
                body.transform.localPosition = rest;
                body.transform.localScale = alongX
                    ? new Vector3(bodyLength, settings.WallHeight, shortSide)
                    : new Vector3(shortSide, settings.WallHeight, bodyLength);

                var crusher = body.AddComponent<Crusher>();
                crusher.Configure(rest, travel, 3.6f, index * 0.9f);

                Material bladeMat = GetBladeMaterial();
                if (bladeMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = bladeMat;
                index++;
            }

            // ---------------------------------------------------------------- T spike traps
            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'T'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                GameObject region = AsciiArenaBuilder.Region<SpikeTrap>(holder, $"Spikes_{index:D2}", rect);
                region.GetComponent<SpikeTrap>().Configure(1.4f, 2.4f, 0.6f, (cells.xMin * 0.37f + cells.yMin * 0.61f) % 4.4f);

                // Cells are children of the region but must not inherit its footprint scale.
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Plate";
                StripColliders(plate);
                plate.transform.SetParent(region.transform, false);
                plate.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                plate.transform.localScale = new Vector3(1f, 0.08f, 1f);
                plate.GetComponent<MeshRenderer>().sharedMaterial = GetPlateMaterial("SpikePlate", new Color(0.22f, 0.2f, 0.22f));

                var trapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Trap);
                for (int y = cells.yMin; y < cells.yMax; y++)
                for (int x = cells.xMin; x < cells.xMax; x++)
                {
                    Rect c = AsciiArenaBuilder.CellRect(new RectInt(x, y, 1, 1), columns, rows, cellW, cellH);
                    var cellGo = new GameObject("Cell");
                    cellGo.transform.SetParent(holder, false);
                    cellGo.transform.localPosition = new Vector3(c.center.x, 0f, c.center.y);
                    cellGo.transform.SetParent(region.transform, true);

                    var spike = new GameObject("Spike").transform;
                    spike.SetParent(cellGo.transform, false);
                    spike.localScale = new Vector3(1f, 0.04f, 1f);

                    if (trapPrefab != null)
                    {
                        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(trapPrefab);
                        model.name = "Trap";
                        StripColliders(model);
                        ApplyMaterial(model, props);
                        model.transform.SetParent(spike, false);
                        float s = cell * 0.95f / 0.79f;
                        model.transform.localScale = new Vector3(s, 6f, s);
                    }
                }

                index++;
            }

            // ---------------------------------------------------------------- U bumpers
            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'U'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                float radius = Mathf.Min(rect.width, rect.height) * 0.45f;

                var go = new GameObject($"Bumper_{index:D2}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(rect.center.x, 0f, rect.center.y);
                go.AddComponent<Bumper>().Configure(radius);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                PlaceProp(Barrel, visual, radius * 2f, props);
                index++;
            }

            // ---------------------------------------------------------------- conveyors
            index = 0;
            foreach (var (marker, dir) in new[] { ('>', Vector2.right), ('<', Vector2.left), ('^', Vector2.up), ('v', Vector2.down) })
            {
                foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == marker))
                {
                    Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                    GameObject region = AsciiArenaBuilder.Region<ConveyorArea>(holder, $"Conveyor_{index:D2}", rect);
                    region.GetComponent<ConveyorArea>().Configure(dir, 6f);

                    // The belt plate lives beside the region (a rotated child of a non-uniformly
                    // scaled parent would skew); its local X runs along the drag so the chevron
                    // texture points the way and the device system can scroll it.
                    var d3 = new Vector3(dir.x, 0f, dir.y);
                    float length = Mathf.Abs(dir.x) > 0f ? rect.width : rect.height;
                    float width = Mathf.Abs(dir.x) > 0f ? rect.height : rect.width;
                    var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plate.name = $"ConveyorPlate_{index:D2}";
                    StripColliders(plate);
                    plate.transform.SetParent(holder, false);
                    plate.transform.localPosition = new Vector3(rect.center.x, -0.02f, rect.center.y);
                    plate.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(d3, Vector3.up) * -1f, Vector3.up);
                    plate.transform.localScale = new Vector3(length, 0.08f, width);
                    var beltRenderer = plate.GetComponent<MeshRenderer>();
                    beltRenderer.sharedMaterial = GetConveyorMaterial();
                    region.GetComponent<ConveyorArea>().SetPlate(beltRenderer);

                    index++;
                }
            }

            // ---------------------------------------------------------------- K gates, k keys
            index = 0;
            foreach (RectInt merged in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'K'))
            foreach (RectInt cells in GateTiles(merged))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                GameObject go = AsciiArenaBuilder.MakeWall(holder, $"Gate_{index:D2}", rect, settings.WallHeight);

                var wall = go.AddComponent<ArenaWall>();
                var so = new SerializedObject(wall);
                so.FindProperty("wallType").enumValueIndex = (int)ArenaWallType.Internal;
                so.FindProperty("fillMode").enumValueIndex = (int)WallFillMode.FixedThickness;
                so.ApplyModifiedPropertiesWithoutUndo();

                go.AddComponent<LockedGate>().Configure("A");
                // No model: a glowing gold block reads as "locked" from above better than any
                // door mesh, and it is exactly what the mechanic is - a plug that drops away.
                go.GetComponent<MeshRenderer>().sharedMaterial =
                    GetPlateMaterial("LockedGateBlock", new Color(0.95f, 0.75f, 0.2f), new Color(1f, 0.7f, 0.15f) * 0.9f);
                AsciiArenaBuilder.MarkCustomVisual(go);
                index++;
            }

            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == 'k'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                var go = new GameObject($"Key_{index:D2}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(rect.center.x, 0f, rect.center.y);
                go.AddComponent<KeyPickup>().Configure("A");

                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                visual.localPosition = new Vector3(0f, 0.6f, 0f);
                PlaceProp(KeyModel, visual, 1.6f, props, floatUp: true);
                index++;
            }

            // ---------------------------------------------------------------- $ coins (one per cell)
            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == '$'))
            {
                for (int y = cells.yMin; y < cells.yMax; y++)
                for (int x = cells.xMin; x < cells.xMax; x++)
                {
                    Rect c = AsciiArenaBuilder.CellRect(new RectInt(x, y, 1, 1), columns, rows, cellW, cellH);
                    var go = new GameObject($"Coin_{index:D2}");
                    go.transform.SetParent(holder, false);
                    go.transform.localPosition = new Vector3(c.center.x, 0f, c.center.y);
                    go.AddComponent<CoinPickup>().Configure(1, 8f);

                    var visual = new GameObject("Visual").transform;
                    visual.SetParent(go.transform, false);
                    visual.localPosition = new Vector3(0f, 0.6f, 0f);
                    PlaceProp(CoinModel, visual, 1.1f, props, floatUp: true, standUp: true);
                    index++;
                }
            }

            // ---------------------------------------------------------------- + potions
            index = 0;
            foreach (RectInt cells in AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == '+'))
            {
                Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                var go = new GameObject($"Potion_{index:D2}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(rect.center.x, 0f, rect.center.y);
                go.AddComponent<PotionPickup>();

                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                visual.localPosition = new Vector3(0f, 0.6f, 0f);
                PlaceProp(PotionModel, visual, 1.3f, props, floatUp: true);
                index++;
            }

            // ---------------------------------------------------------------- 1 2 teleporters
            // Pairing rule: a marker drawn twice (two '1' pads) links to itself. A marker drawn once
            // links to the other single marker - the map sets write "1 ... 2" meaning one two-way
            // link, and a pad with no twin never fires (RB11/CR08/SD17 shipped that way once).
            var padRects = new Dictionary<char, List<RectInt>>();
            foreach (char marker in new[] { '1', '2' })
                padRects[marker] = new List<RectInt>(AsciiArenaBuilder.MergeRects(grid, columns, rows, c => c == marker));
            int singles = 0;
            foreach (var kv in padRects) if (kv.Value.Count == 1) singles++;

            foreach (char marker in new[] { '1', '2' })
            {
                string pairId = padRects[marker].Count == 1 && singles == 2 ? "12" : marker.ToString();
                foreach (RectInt cells in padRects[marker])
                {
                    Rect rect = AsciiArenaBuilder.CellRect(cells, columns, rows, cellW, cellH);
                    GameObject region = AsciiArenaBuilder.Region<Teleporter>(holder, $"Teleporter_{marker}", rect);
                    region.GetComponent<Teleporter>().Configure(pairId);

                    var portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    portal.name = "Portal";
                    StripColliders(portal);
                    portal.transform.SetParent(region.transform, false);
                    portal.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                    portal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    portal.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                    portal.GetComponent<MeshRenderer>().sharedMaterial = GetPortalMaterial();

                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = "Ring";
                    StripColliders(ring);
                    ring.transform.SetParent(region.transform, false);
                    ring.transform.localPosition = new Vector3(0f, -0.17f, 0f);
                    ring.transform.localScale = new Vector3(1.05f, 0.03f, 1.05f);
                    ring.GetComponent<MeshRenderer>().sharedMaterial = GetPlateMaterial("PortalRing", new Color(0.2f, 0.1f, 0.35f), new Color(0.6f, 0.3f, 1f) * 0.6f);
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A long gate wall becomes a row of two-cell gate leaves along its length.</summary>
        private static IEnumerable<RectInt> GateTiles(RectInt rect)
        {
            bool alongY = rect.height >= rect.width;
            int length = alongY ? rect.height : rect.width;
            for (int i = 0; i < length; i += 2)
            {
                int span = Mathf.Min(2, length - i);
                yield return alongY
                    ? new RectInt(rect.xMin, rect.yMin + i, rect.width, span)
                    : new RectInt(rect.xMin + i, rect.yMin, span, rect.height);
            }
        }

        private static void BuildRailTrack(Transform holder, Vector3 start, Vector3 end, float radius, Material material)
        {
            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "Rail";
            StripColliders(track);
            track.transform.SetParent(holder, false);
            Vector3 mid = (start + end) * 0.5f;
            Vector3 d = end - start;
            track.transform.localPosition = new Vector3(mid.x, 0.03f, mid.z);
            track.transform.localScale = Mathf.Abs(d.x) > Mathf.Abs(d.z)
                ? new Vector3(d.magnitude + radius * 2f, 0.06f, 0.35f)
                : new Vector3(0.35f, 0.06f, d.magnitude + radius * 2f);
            if (material != null) track.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void BuildChevron(Transform holder, Transform parent, Vector3 position, Vector3 dir, float size)
        {
            var chevron = new GameObject("Chevron");
            chevron.transform.SetParent(holder, false);
            chevron.transform.localPosition = new Vector3(position.x, 0.02f, position.z);
            chevron.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

            Material mat = GetPlateMaterial("ConveyorArrow", new Color(0.7f, 1f, 1f), new Color(0.5f, 1f, 1f) * 0.9f);
            foreach (float sign in new[] { -1f, 1f })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Bar";
                StripColliders(bar);
                bar.transform.SetParent(chevron.transform, false);
                bar.transform.localPosition = new Vector3(sign * size * 0.25f, 0f, -size * 0.2f);
                bar.transform.localRotation = Quaternion.Euler(0f, -sign * 45f, 0f);
                bar.transform.localScale = new Vector3(size * 0.7f, 0.04f, size * 0.18f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            chevron.transform.SetParent(parent, true);
        }

        /// <summary>Instantiates a pack model under a parent, fitted to a longest side, resting on y=0.</summary>
        private static void PlaceProp(string path, Transform parent, float targetSize, Material material,
            bool floatUp = false, bool standUp = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.position = Vector3.zero;
            model.transform.rotation = standUp ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Object.DestroyImmediate(model); return; }
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
            float s = targetSize / longest;

            StripColliders(model);
            ApplyMaterial(model, material);
            model.transform.SetParent(parent, false);
            model.transform.localScale = Vector3.one * s;
            model.transform.localPosition = new Vector3(-b.center.x * s, (floatUp ? -b.center.y : -b.min.y) * s, -b.center.z * s);
        }

        /// <summary>Hides a wall cube and fits a model into its box, the way rocks dress breakables.</summary>
        internal static void FitModelIntoBox(GameObject wallGo, string path, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            wallGo.GetComponent<MeshRenderer>().enabled = false;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Visual";
            model.transform.position = Vector3.zero;
            model.transform.rotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Object.DestroyImmediate(model); return; }
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            // The gate model is thin along Z; turn it to face the wall's thin axis.
            Vector3 box = wallGo.transform.localScale;
            bool thinX = box.x < box.z;
            if (thinX) model.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            renderers = model.GetComponentsInChildren<Renderer>(true);
            b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            Vector3 size = Vector3.Max(b.size, Vector3.one * 0.001f);
            var fit = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);

            StripColliders(model);
            ApplyMaterial(model, material);
            model.transform.SetParent(wallGo.transform, false);
            model.transform.localScale = fit;
            model.transform.localPosition = -Vector3.Scale(b.center, fit);
            AsciiArenaBuilder.MarkCustomVisual(wallGo);
        }

        private static void StripColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            }
        }

        /// <summary>The dungeon pack's colormap on the toon shader, shared by every prop.</summary>
        private static Material GetPropMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(PropMaterialPath));
            material = new Material(shader) { name = "CubeSimProps" };
            material.SetColor("_BaseColor", Color.white);
            var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(Colormap);
            if (colormap != null) material.SetTexture("_BaseMap", colormap);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.55f, 0.5f, 0.5f));
            AssetDatabase.CreateAsset(material, PropMaterialPath);
            return material;
        }

        /// <summary>Format-2 pack colormap on the toon shader (doors, laser gates).</summary>
        internal static Material GetPropMaterial2()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Props2MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(Props2MaterialPath));
            material = new Material(shader) { name = "CubeSimProps2" };
            material.SetColor("_BaseColor", Color.white);
            var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(Colormap2);
            if (colormap != null) material.SetTexture("_BaseMap", colormap);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.55f, 0.5f, 0.5f));
            AssetDatabase.CreateAsset(material, Props2MaterialPath);
            return material;
        }

        /// <summary>Tileable chevron belt texture; the device system scrolls the offset at run time.</summary>
        private static Material GetConveyorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ConveyorMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CubeSim/ConveyorPlanar");
            if (shader == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(ConveyorMaterialPath));
            material = new Material(shader) { name = "ConveyorBelt" };
            ConfigureConveyorMaterial(material);
            AssetDatabase.CreateAsset(material, ConveyorMaterialPath);
            return material;
        }

        /// <summary>
        /// World-projected belt: the pattern keeps its physical size on every plate, is lifted
        /// from the near-black source with a teal tint plus emission so it glows under bloom, and
        /// leaves rotation/scroll to the device system per belt.
        /// </summary>
        internal static void ConfigureConveyorMaterial(Material material)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ConveyorTexture);
            if (tex != null) material.SetTexture("_BaseMap", tex);
            material.SetColor("_Color", new Color(1.2f, 2.2f, 2.4f));
            material.SetColor("_Emission", new Color(1.6f, 6.5f, 7.5f));
            material.SetFloat("_MetresPerRepeat", 5.6f);
            material.SetFloat("_AngleOffset", 0f);
        }

        private static Material GetPortalMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PortalMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CubeSim/Portal");
            if (shader == null) return GetPlateMaterial("TeleportPlate", new Color(0.45f, 0.2f, 0.85f), new Color(0.6f, 0.3f, 1f) * 0.8f);

            Directory.CreateDirectory(Path.GetDirectoryName(PortalMaterialPath));
            material = new Material(shader) { name = "Portal" };
            AssetDatabase.CreateAsset(material, PortalMaterialPath);
            return material;
        }

        private static Material GetBladeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(BladeMaterialPath));
            material = new Material(shader) { name = "Blade" };
            material.SetColor("_BaseColor", new Color(0.62f, 0.08f, 0.1f));
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", new Color(0.3f, 0.03f, 0.05f));
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.9f, 0.05f, 0.05f) * 0.6f);
            }
            AssetDatabase.CreateAsset(material, BladeMaterialPath);
            return material;
        }

        internal static Material GetPlateMaterialShared(string name, Color color, Color emission = default) => GetPlateMaterial(name, color, emission);

        private static Material GetPlateMaterial(string name, Color color, Color emission = default)
        {
            string path = $"Assets/CubeSim/Visuals/Props/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", color * 0.5f);
            if (emission != default && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
