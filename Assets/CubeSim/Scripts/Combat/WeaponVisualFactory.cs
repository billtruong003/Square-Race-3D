using System.Collections.Generic;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.Combat
{
    /// <summary>
    /// Spawns weapon models from the asset packs.
    ///
    /// The packs ship their materials on the built-in <c>Standard</c> shader, which renders as broken
    /// magenta under URP. Rather than editing the source packs, every material is remapped onto
    /// ToonLit at spawn time, keeping each weapon's own colour and texture - so different guns still
    /// look like different guns instead of all sharing one stand-in material.
    /// </summary>
    public static class WeaponVisualFactory
    {
        private static readonly Dictionary<Material, Material> Remapped = new Dictionary<Material, Material>();
        private static Shader _toonShader;

        /// <summary>Clears the remap cache. Called when an episode tears down its materials.</summary>
        public static void ResetCache()
        {
            foreach (KeyValuePair<Material, Material> pair in Remapped)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Object.Destroy(pair.Value);
                else Object.DestroyImmediate(pair.Value);
            }

            Remapped.Clear();
        }

        /// <summary>Which presentation a weapon model is being built for.</summary>
        public enum Context
        {
            Pickup,
            Equipped
        }

        public static GameObject Create(WeaponDefinition definition, MaterialLibrary materials,
            Transform parent, WeaponVisualLibrary library, Context context = Context.Equipped)
        {
            WeaponVisualLibrary.Entry entry = library != null ? library.Find(definition.id) : null;

            GameObject root;
            if (entry != null && entry.prefab != null)
            {
                root = Object.Instantiate(entry.prefab);
                StripGameplayComponents(root);
                ConvertMaterials(root);

                float contextScale = context == Context.Pickup
                    ? Mathf.Max(0.01f, entry.pickupScale)
                    : Mathf.Max(0.01f, entry.equippedScale);

                Quaternion rotation = Quaternion.Euler(entry.orientation);
                if (context == Context.Equipped) rotation = Quaternion.Euler(entry.equippedEuler) * rotation;

                root.transform.SetParent(parent, false);
                root.transform.localRotation = rotation;
                root.transform.localPosition = entry.offset;
                root.transform.localScale = Vector3.one * (Mathf.Max(0.01f, entry.scale) * contextScale);
            }
            else
            {
                root = BuildPrimitive(definition, materials);
                root.transform.SetParent(parent, false);
            }

            root.name = "Weapon_" + definition.id;
            return root;
        }

        /// <summary>The projectile model for a weapon, already converted and scaled.</summary>
        public static GameObject CreateProjectile(WeaponDefinition definition, Transform parent,
            WeaponVisualLibrary library, out float visualScale)
        {
            WeaponVisualLibrary.Entry entry = library != null ? library.Find(definition.id) : null;
            visualScale = entry != null ? Mathf.Max(0.01f, entry.projectileVisualScale) : 1f;

            if (entry?.projectilePrefab == null) return null;

            GameObject root = Object.Instantiate(entry.projectilePrefab, parent);
            StripGameplayComponents(root);
            ConvertMaterials(root);

            root.transform.localRotation = Quaternion.Euler(entry.projectileEuler);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one * visualScale;
            return root;
        }

        /// <summary>
        /// Pack prefabs bring colliders, rigidbodies and scripts. None of that belongs in a purely
        /// cosmetic weapon - a stray collider would show up in the movement casts.
        /// </summary>
        private static void StripGameplayComponents(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) Destroy(light);
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true)) Destroy(particles);

            // The POLYGON pack nests a camera (with an audio listener) inside some gun prefabs.
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) Destroy(listener);
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) Destroy(camera);
        }

        /// <summary>Remaps every material onto ToonLit, preserving its colour and texture.</summary>
        private static void ConvertMaterials(GameObject root)
        {
            if (_toonShader == null)
            {
                _toonShader = Shader.Find("CleanRender/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                var converted = new Material[source.Length];

                for (int i = 0; i < source.Length; i++) converted[i] = Convert(source[i]);
                renderer.sharedMaterials = converted;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static Material Convert(Material source)
        {
            if (source == null) return null;

            // Already in the project's toon family, or a URP material: leave it alone.
            if (source.shader == _toonShader) return source;

            if (Remapped.TryGetValue(source, out Material cached) && cached != null) return cached;

            var material = new Material(_toonShader) { name = source.name + "_Toon" };

            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color")
                : Color.white;

            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex")
                : null;

            // Packs disagree about property names; when the usual two miss, take the first texture
            // that is not a normal/metallic/emission map. No weapon ships flat-coloured.
            if (texture == null)
            {
                foreach (string property in source.GetTexturePropertyNames())
                {
                    string lower = property.ToLowerInvariant();
                    if (lower.Contains("bump") || lower.Contains("normal") ||
                        lower.Contains("metallic") || lower.Contains("emission")) continue;

                    Texture candidate = source.GetTexture(property);
                    if (candidate == null) continue;

                    texture = candidate;
                    break;
                }
            }

            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", color * 0.45f);
            if (material.HasProperty("_Threshold")) material.SetFloat("_Threshold", 0.45f);

            // Carry over emission so glowing pack parts keep glowing under the toon shader.
            Color emission = source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black;
            if (emission.maxColorComponent > 0.01f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 1f);
                material.SetColor("_EmissionColor", emission);
            }

            // Only carry alpha clipping when the source actually used it - forcing it on turns solid
            // pack materials into holes.
            if (source.HasProperty("_Cutoff") && source.IsKeywordEnabled("_ALPHATEST_ON") &&
                material.HasProperty("_Cutoff"))
            {
                material.EnableKeyword("_ALPHATEST_ON");
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            }

            Remapped[source] = material;
            return material;
        }

        private static GameObject BuildPrimitive(WeaponDefinition definition, MaterialLibrary materials)
        {
            var root = new GameObject("WeaponVisual");
            Material material = materials.GetWeaponMaterial(definition);

            AddBox(root.transform, "Body", Vector3.zero, definition.visualSize, material);

            if (definition.category == WeaponCategory.Melee)
            {
                AddBox(root.transform, "Guard",
                    new Vector3(0f, 0f, -definition.visualSize.z * 0.42f),
                    new Vector3(definition.visualSize.x * 3.5f, definition.visualSize.y * 0.6f, definition.visualSize.x * 1.2f),
                    material);
            }
            else
            {
                AddBox(root.transform, "Barrel",
                    new Vector3(0f, 0f, definition.visualSize.z * 0.6f),
                    new Vector3(definition.visualSize.x * 0.45f, definition.visualSize.x * 0.45f, definition.visualSize.z * 0.5f),
                    material);
            }

            return root;
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Destroy(Object component)
        {
            if (component == null) return;
            if (Application.isPlaying) Object.Destroy(component);
            else Object.DestroyImmediate(component);
        }
    }
}
