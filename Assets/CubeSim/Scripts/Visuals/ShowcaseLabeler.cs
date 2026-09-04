using System.Collections.Generic;
using UnityEngine;
using CubeSim.Arena.Authored;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Review-mode overlay for the Showcase scene: hangs a flat name tag over every map element
    /// and every racer so each visual can be judged at a glance from the top-down camera.
    ///
    /// The arena and its systems are built at runtime, so this scans shortly after play starts
    /// (and keeps rescanning cheaply) and labels whatever it recognises: arena holders by name,
    /// weapon pickups, the food field, and the racers - whose tags ride along with them. Purely
    /// cosmetic: reads names and positions, writes nothing back.
    /// </summary>
    public sealed class ShowcaseLabeler : MonoBehaviour
    {
        private const float LabelHeight = 3.4f;
        private const float RescanInterval = 1f;

        private static readonly Dictionary<string, string> HolderLabels = new Dictionary<string, string>
        {
            { "Walls", "WALL" },
            { "Breakables", "BREAKABLE" },
            { "MegaBlocks", "MEGA BLOCK" },
            { "RainbowGates", "RAINBOW GATE" },
            { "Doors", "DOOR" },
            { "Rotors", "ROTOR" },
            { "GlassPanes", "WHITE GLASS" },
        };

        private static readonly Dictionary<string, string> RegionLabels = new Dictionary<string, string>
        {
            { "SpawnArea_Left", "SPAWN L" },
            { "SpawnArea_Right", "SPAWN R" },
            { "SpawnArea_Top", "SPAWN TOP" },
            { "SpawnArea_Bottom", "SPAWN BOTTOM" },
            { "Spikes_", "SPIKE TRAP" },
            { "Conveyor_", "CONVEYOR" },
            { "Teleporter_", "TELEPORTER" },
            { "GoalArea", "GOAL" },
            { "Hazard", "HAZARD" },
            { "WeaponArea", "WEAPON AREA" },
            { "FoodArea", "FOOD AREA" },
        };

        private readonly HashSet<Transform> _labeled = new HashSet<Transform>();
        private Font _font;
        private float _nextScan;

        private void Update()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + RescanInterval;

            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            LabelArena();
            LabelPickups();
            LabelRacers();
        }

        private void LabelArena()
        {
            var arena = FindFirstObjectByType<AuthoredArena>();
            if (arena == null) return;

            foreach (Transform holder in arena.transform)
            {
                if (holder.name == "Border")
                {
                    // One tag on the first border mass is enough; four would just repeat it.
                    if (holder.childCount > 0) Label(holder.GetChild(0), "BORDER", false);
                    continue;
                }

                if (HolderLabels.TryGetValue(holder.name, out string label))
                {
                    // One tag per element type. The smallest child is the exhibit itself - the
                    // Walls holder also carries the huge boundary masses, and a rainbow gate is
                    // several stacked layers that would each grab their own overlapping tag.
                    Label(SmallestChild(holder), label, false);
                    continue;
                }

                if (holder.name == "Devices")
                {
                    // One tag per device kind: the first child of each name prefix.
                    var seen = new HashSet<string>();
                    foreach (Transform device in holder)
                    {
                        string prefix = device.name.Split('_')[0];
                        if (!seen.Add(prefix)) continue;
                        string tag = prefix switch
                        {
                            "Saw" => "SAW BLADE", "Crusher" => "CRUSHER", "Spikes" => "SPIKE TRAP",
                            "Bumper" => "BUMPER", "Conveyor" => "CONVEYOR", "Gate" => "LOCKED GATE",
                            "Key" => "KEY", "Coin" => "COIN", "Potion" => "POTION",
                            "Teleporter" => "TELEPORTER", "Rail" => "SAW RAIL", _ => null,
                        };
                        if (tag != null) Label(device, tag, false);
                    }
                    continue;
                }

                if (holder.name == "Regions")
                {
                    foreach (Transform region in holder)
                    {
                        foreach (KeyValuePair<string, string> pair in RegionLabels)
                        {
                            if (!region.name.StartsWith(pair.Key)) continue;
                            Label(region, pair.Value, false);
                            break;
                        }
                    }
                }
            }
        }

        private void LabelPickups()
        {
            var combat = GameObject.Find("Combat");
            if (combat == null) return;

            foreach (Transform child in combat.transform)
            {
                if (child.name.StartsWith("Pickup_")) Label(child, "CLEAVER", true);
            }
        }

        private void LabelRacers()
        {
            var holder = GameObject.Find("Racers");
            if (holder == null) return;

            foreach (Transform racer in holder.transform)
            {
                var visual = racer.GetComponentInChildren<RacerVisual>();
                if (visual == null) continue;

                Label(racer, ColorNames.NameFor(visual.Color), true);
            }
        }

        private static Transform SmallestChild(Transform holder)
        {
            Transform best = null;
            float bestArea = float.MaxValue;

            foreach (Transform child in holder)
            {
                var renderer = child.GetComponentInChildren<Renderer>();
                if (renderer == null) continue;

                float area = renderer.bounds.size.x * renderer.bounds.size.z;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = child;
                }
            }

            return best;
        }

        /// <summary>One flat tag above the target; followers are parented so they ride along.</summary>
        private void Label(Transform target, string text, bool follow)
        {
            if (target == null || !_labeled.Add(target)) return;

            var go = new GameObject("Label_" + text);

            if (follow)
            {
                go.transform.SetParent(target, false);
                go.transform.localPosition = new Vector3(0f, LabelHeight, 0f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                go.transform.SetParent(transform, false);
                Vector3 center = target.position;
                var renderer = target.GetComponentInChildren<Renderer>();
                if (renderer != null) center = renderer.bounds.center;
                go.transform.position = new Vector3(center.x, LabelHeight, center.z);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = _font;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.fontSize = 64;
            mesh.characterSize = 0.2f; // ~1.3m capitals: readable when the camera frames the course
            mesh.color = Color.white;
        }
    }
}
