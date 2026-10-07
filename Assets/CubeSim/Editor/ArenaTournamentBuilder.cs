using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Anime Fight Arena tournament: six small trap arenas (48x28 m), fixed weapon pads with a set
    /// menu per arena, 20 cubes in five groups of four, winners meet in the Colosseum final.
    /// Prefabs come from Arenas/Templates/A1..A5, FINAL; this assigns each pad its weapon and
    /// writes the M_Arena scene. See Docs/ARENA_PLAN.md.
    /// </summary>
    public static class ArenaTournamentBuilder
    {
        public const string PrefabFolder = "Assets/CubeSim/Arenas/Arena";
        public const string ScenePath = ModeSceneBuilder.SceneFolder + "/M_Arena.unity";

        public const float RacerSpeed = 9f;
        public const float Health = 100f;
        public static readonly Vector2 CourseSize = new Vector2(48f, 28f);

        public sealed class ArenaPlan
        {
            public string Id;
            public string Title;
            /// <summary>Weapon ids by pad, pads sorted top-to-bottom then left-to-right.</summary>
            public string[] Pads;
            public EpisodeDirector.PressureProfile Pressure = EpisodeDirector.PressureProfile.Collapse;
            // Closes to an 8x8 m pit: four cubes with weapons cannot avoid each other there,
            // and only a round that still stalls gets crushed (~90 s).
            public float PressureDelay = 20f;
            public float PressureSpeed = 0.35f;
            public float InsetX = 20f;
            public float InsetZ = 10f;
            public Color Floor = new Color(0.36f, 0.36f, 0.38f);
            public Color Wall = new Color(0.30f, 0.30f, 0.33f);
        }

        public static readonly ArenaPlan[] Arenas =
        {
            // One colour per arena so the six rounds read as six places, not one room six times.
            // Bright floors: the cubes and guns are dark and must pop. The wall tint is also the surround.
            new ArenaPlan { Id = "A1", Title = "PISTON HALL", Pads = new[] { "Knife", "Revolver" },
                Floor = new Color(0.86f, 0.80f, 0.72f), Wall = new Color(0.66f, 0.52f, 0.42f) },
            new ArenaPlan { Id = "A2", Title = "SAW CORRIDOR", Pads = new[] { "Chainsaw", "Glove" },
                Floor = new Color(0.78f, 0.85f, 0.93f), Wall = new Color(0.50f, 0.60f, 0.74f) },
            new ArenaPlan { Id = "A3", Title = "BUMPER PIT", Pads = new[] { "Glove", "Shotgun", "Axe" },
                Floor = new Color(0.87f, 0.79f, 0.93f), Wall = new Color(0.62f, 0.50f, 0.72f) },
            new ArenaPlan { Id = "A4", Title = "SNIPER ALLEY", Pads = new[] { "Sniper", "SMG" },
                Floor = new Color(0.80f, 0.91f, 0.80f), Wall = new Color(0.50f, 0.68f, 0.52f) },
            new ArenaPlan { Id = "A5", Title = "BOMB YARD", Pads = new[] { "Shield", "Grenade", "BombVest" },
                Floor = new Color(0.93f, 0.87f, 0.70f), Wall = new Color(0.72f, 0.62f, 0.42f) },
            new ArenaPlan { Id = "FINAL", Title = "COLOSSEUM", Pads = new[] { "Sniper", "Knife", "Shotgun", "Rocket", "Axe", "LaserSword" },
                Pressure = EpisodeDirector.PressureProfile.Chase, PressureDelay = 20f, PressureSpeed = 0.3f, InsetX = 42f,
                Floor = new Color(0.90f, 0.90f, 0.92f), Wall = new Color(0.78f, 0.66f, 0.34f) },
        };

        public static ArenaPlan Find(string id) { foreach (var a in Arenas) if (a.Id == id) return a; return null; }

        public static string RuleLabel(ArenaPlan plan)
        {
            var sb = new StringBuilder(plan.Title).Append("  ·  ");
            for (int i = 0; i < plan.Pads.Length; i++) { if (i > 0) sb.Append(" + "); sb.Append(plan.Pads[i].ToLowerInvariant()); }
            return sb.Append(", last one standing").ToString();
        }

        [MenuItem("CubeSim/Arena/Build Arena Prefabs", priority = 20)]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            var library = AssetDatabase.LoadAssetAtPath<AuthoredArenaLibrary>(SerpentineMapBuilder.LibraryPath);
            if (library == null) { Debug.LogError("[CubeSim] No arena library."); return; }
            var entries = new List<AuthoredArenaLibrary.Entry>(library.Entries);
            var report = new StringBuilder("[CubeSim] Arena prefabs:\n");

            foreach (ArenaPlan plan in Arenas)
            {
                string template = $"Assets/CubeSim/Arenas/Templates/{plan.Id}.txt";
                if (!File.Exists(template)) { Debug.LogWarning($"[CubeSim] No template {template}"); continue; }
                string prefabPath = $"{PrefabFolder}/{plan.Id}.prefab";
                AsciiArenaBuilder.Build(template, new AsciiArenaBuilder.Settings
                {
                    ArenaId = plan.Id, CourseSize = CourseSize, WallHeight = 2.8f, VisualFillPadding = 22f,
                    DesignedCorridorWidth = 2.8f, BreakableHits = 3, MegaBlockHits = 60, RockTileCells = 2, RainbowLayerHits = 2,
                    // HP 100 scale: spikes strip a quarter bar per second, a saw cut is a quarter bar.
                    HazardDamagePerSecond = 25f, SawDamage = 20f,
                }, prefabPath);

                // Pads: the template only says where; the plan says what grows there.
                using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                {
                    var pads = new List<WeaponSpawnArea>(scope.prefabContentsRoot.GetComponentsInChildren<WeaponSpawnArea>(true));
                    pads.Sort((a, b) =>
                    {
                        if (Mathf.Abs(a.transform.position.z - b.transform.position.z) > 0.5f)
                            return b.transform.position.z.CompareTo(a.transform.position.z);
                        return a.transform.position.x.CompareTo(b.transform.position.x);
                    });
                    for (int i = 0; i < pads.Count; i++)
                    {
                        string id = i < plan.Pads.Length ? plan.Pads[i] : plan.Pads[plan.Pads.Length - 1];
                        pads[i].ConfigurePad(id, 4f);
                        pads[i].gameObject.name = "WeaponPad_" + id;
                    }
                    report.Append("  ").Append(plan.Id).Append(": ").Append(pads.Count).Append(" pads");
                    if (pads.Count != plan.Pads.Length) report.Append("  (plan lists ").Append(plan.Pads.Length).Append("!)");
                    report.Append('\n');
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                entries.RemoveAll(e => e.id == plan.Id);
                entries.Add(new AuthoredArenaLibrary.Entry { id = plan.Id, prefab = prefab });
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        public static EpisodeDirector.RoundSpec Round(ArenaPlan plan, int seed, List<int> slots, bool final)
        {
            return new EpisodeDirector.RoundSpec
            {
                arenaId = plan.Id,
                seed = seed,
                winCondition = WinCondition.LastAlive,
                maxDuration = 300f,
                maxHealth = Health,
                weaponCount = 0,
                racerCount = slots != null ? slots.Count : 4,
                paletteIndices = slots != null ? new List<int>(slots) : new List<int>(),
                finalOfWinners = final,
                racerSpeed = RacerSpeed,
                pressureProfile = plan.Pressure,
                pressureStartDelay = plan.PressureDelay,
                pressureSpeed = plan.PressureSpeed,
                pressureTargetInset = plan.InsetX,
                pressureTargetInsetZ = plan.InsetZ,
                modeKind = ModeKind.Arena,
                ruleLabel = RuleLabel(plan),
                floorColor = plan.Floor,
                wallColor = plan.Wall,
                portrait = false,
            };
        }

        [MenuItem("CubeSim/Arena/Build Tournament Scene", priority = 21)]
        public static void BuildScene()
        {
            BuildPrefabs();
            Directory.CreateDirectory(ModeSceneBuilder.SceneFolder);
            var draw = new System.Random(unchecked(System.Environment.TickCount));

            Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director == null) { Debug.LogError("[CubeSim] Episode scene has no director."); return; }

            var rounds = new List<EpisodeDirector.RoundSpec>();
            for (int g = 0; g < 5; g++)
            {
                var slots = new List<int> { g * 4, g * 4 + 1, g * 4 + 2, g * 4 + 3 };
                rounds.Add(Round(Arenas[g], draw.Next(100000, 999999), slots, false));
            }
            var finalRound = Round(Arenas[5], draw.Next(100000, 999999), null, true);
            finalRound.racerCount = 5;
            rounds.Add(finalRound);

            director.SetRounds(rounds);
            var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
            if (bootstrap != null)
            {
                var bso = new SerializedObject(bootstrap);
                bso.FindProperty("showLeaderboard").boolValue = true;
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
            }
            EditorUtility.SetDirty(director);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CubeSim] Arena tournament scene: 5 groups + final -> " + ScenePath);
        }

        public const string BatchFolder = ModeSceneBuilder.SceneFolder + "/ArenaBatch";

        /// <summary>
        /// Ten full tournaments, each with its own seeds and its own draw of which colours meet in
        /// which group, so the ten videos are ten different stories. Recorded with the batch recorder.
        /// </summary>
        [MenuItem("CubeSim/Arena/Build 10 Tournament Videos", priority = 24)]
        public static void BuildBatch() => BuildBatch(10);

        public static void BuildBatch(int count) => BuildBatch(count, 1, BatchFolder);

        public static void BuildBatch(int count, int firstIndex, string folder)
        {
            BuildPrefabs();
            Directory.CreateDirectory(folder);
            var draw = new System.Random(unchecked(System.Environment.TickCount));
            for (int v = firstIndex; v < firstIndex + count; v++)
            {
                Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
                var director = Object.FindFirstObjectByType<EpisodeDirector>();
                if (director == null) { Debug.LogError("[CubeSim] Episode scene has no director."); return; }

                var slots = new List<int>();
                for (int s = 0; s < 20; s++) slots.Add(s);
                for (int s = slots.Count - 1; s > 0; s--) { int k = draw.Next(s + 1); (slots[s], slots[k]) = (slots[k], slots[s]); }

                var rounds = new List<EpisodeDirector.RoundSpec>();
                for (int g = 0; g < 5; g++)
                    rounds.Add(Round(Arenas[g], draw.Next(100000, 999999), slots.GetRange(g * 4, 4), false));
                var finalRound = Round(Arenas[5], draw.Next(100000, 999999), null, true);
                finalRound.racerCount = 5;
                rounds.Add(finalRound);

                director.SetRounds(rounds);
                var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
                if (bootstrap != null)
                {
                    var bso = new SerializedObject(bootstrap);
                    bso.FindProperty("showLeaderboard").boolValue = true;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstrap);
                }
                EditorUtility.SetDirty(director);
                EditorSceneManager.SaveScene(scene, $"{folder}/M_Arena_T{v:D2}.unity");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CubeSim] {count} tournament scenes -> {folder}");
        }

        /// <summary>A short approval cut: two groups and a two-cube final, same rules as the full tournament.</summary>
        [MenuItem("CubeSim/Arena/Build Test Scene (2 groups + final)", priority = 23)]
        public static void BuildTestScene()
        {
            BuildPrefabs();
            Directory.CreateDirectory(ModeSceneBuilder.SceneFolder);
            var draw = new System.Random(unchecked(System.Environment.TickCount));
            Scene scene = EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director == null) { Debug.LogError("[CubeSim] Episode scene has no director."); return; }

            var rounds = new List<EpisodeDirector.RoundSpec>
            {
                Round(Arenas[0], draw.Next(100000, 999999), new List<int> { 0, 1, 2, 3 }, false),
                Round(Arenas[3], draw.Next(100000, 999999), new List<int> { 12, 13, 14, 15 }, false),
            };
            var finalRound = Round(Arenas[5], draw.Next(100000, 999999), null, true);
            finalRound.racerCount = 2;
            rounds.Add(finalRound);

            director.SetRounds(rounds);
            var bootstrap = Object.FindFirstObjectByType<SimulationBootstrap>();
            if (bootstrap != null)
            {
                var bso = new SerializedObject(bootstrap);
                bso.FindProperty("showLeaderboard").boolValue = true;
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
            }
            EditorUtility.SetDirty(director);
            EditorSceneManager.SaveScene(scene, ModeSceneBuilder.SceneFolder + "/M_ArenaTest.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[CubeSim] Arena test scene: 2 groups + final -> M_ArenaTest.unity");
        }

        /// <summary>
        /// The gate from ARENA_PLAN step A: 4-cube rounds 25-70 s, most deaths by weapon or trap,
        /// no weapon above 35 % of kills, no draws. Runs headless in the episode scene.
        /// </summary>
        [MenuItem("CubeSim/Arena/Lab Gate (20 seeds x 6 arenas)", priority = 22)]
        public static void LabGate() => LabGate(20);

        public static string LabGate(int seeds)
        {
            if (Object.FindFirstObjectByType<SimulationBootstrap>() == null)
                EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            var killsByWeapon = new Dictionary<string, int>();
            int rounds = 0, inWindow = 0, draws = 0, weaponDeaths = 0, otherDeaths = 0;

            foreach (ArenaPlan plan in Arenas)
            {
                bool final = plan.Id == "FINAL";
                var spec = new ModeLab.Spec
                {
                    ArenaId = plan.Id, Mode = ModeKind.Arena, Win = WinCondition.LastAlive,
                    Racers = final ? 5 : 4, Weapons = 0, MaxHealth = Health, MaxDuration = 300f,
                    Pressure = plan.Pressure, PressureDelay = plan.PressureDelay, PressureSpeed = plan.PressureSpeed,
                    InsetX = plan.InsetX, InsetZ = plan.InsetZ,
                    Tweak = c => { c.racers.speed = RacerSpeed; },
                };
                var stats = ModeLab.Run(spec, 1000, seeds, runner =>
                {
                    int wk = 0;
                    var parts = new StringBuilder();
                    if (runner.Combat != null)
                        foreach (var kv in runner.Combat.KillsByWeapon)
                        {
                            wk += kv.Value;
                            killsByWeapon[kv.Key] = killsByWeapon.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;
                            parts.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
                        }
                    return "weaponKills=" + wk + " [" + parts.ToString().Trim() + "]";
                });
                sb.Append(ModeLab.Summarize(plan.Id + " " + plan.Title, stats));
                foreach (var st in stats)
                {
                    rounds++;
                    if (st.Seconds >= 25f && st.Seconds <= 70f) inWindow++;
                    if (st.Outcome != SimulationOutcome.Winner) draws++;
                    int wk = 0;
                    int.TryParse(st.Extra.Split(' ')[0].Replace("weaponKills=", ""), out wk);
                    weaponDeaths += wk + st.Hazard;
                    otherDeaths += Mathf.Max(0, st.Deaths - wk - st.Hazard);
                }
            }

            int total = 0; foreach (var kv in killsByWeapon) total += kv.Value;
            sb.AppendLine("== GATE");
            sb.AppendLine("rounds " + rounds + "  in 25-70s: " + inWindow + " (" + (100f * inWindow / Mathf.Max(1, rounds)).ToString("F0") + "%)  draws/timeouts: " + draws);
            sb.AppendLine("deaths by weapon/trap: " + weaponDeaths + "  by crush/other: " + otherDeaths + "  (" + (100f * weaponDeaths / Mathf.Max(1, weaponDeaths + otherDeaths)).ToString("F0") + "% weapon/trap)");
            foreach (var kv in killsByWeapon) sb.AppendLine("  " + kv.Key.PadRight(12) + " " + kv.Value.ToString().PadLeft(4) + "  " + (100f * kv.Value / Mathf.Max(1, total)).ToString("F0") + "%");

            Directory.CreateDirectory("Docs/ModeLab");
            File.WriteAllText("Docs/ModeLab/arena_gate.txt", sb.ToString());
            Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
