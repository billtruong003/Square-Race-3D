using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CubeSim.Racers;
using CubeSim.Arena;
using CubeSim.Visuals;

namespace CubeSim.Core
{
    /// <summary>
    /// Turns single episodes into a multi-round video: N rounds across N arenas, one winner each,
    /// stitched together with the story cards - intro bet, round card, winner card, final podium.
    /// This is the shape of the reference channel's uploads ("17 cubes, 6 arenas, one winner per
    /// round").
    ///
    /// The director never touches simulation internals. It builds each round through the same
    /// config path everything else uses, pauses stepping while a card is up, and reads results
    /// from <see cref="SimulationResult"/>. Same rounds + same seeds = the same video, cut for cut.
    /// </summary>
    [RequireComponent(typeof(SimulationBootstrap))]
    public sealed class EpisodeDirector : MonoBehaviour
    {
        [System.Serializable]
        public class RoundSpec
        {
            public string arenaId = "Arena5v5";
            public int seed = 1;
            public WinCondition winCondition = WinCondition.LastAlive;

            [Tooltip("Hard cap for the round, seconds. 0 keeps the config's own cap.")]
            public float maxDuration = 165f;

            [Header("Pressure override (applied to every slab; 0 / negative keeps the config)")]
            [Tooltip("Where the squeeze stops. On a chamber map this is what pins the field " +
                     "against the doors instead of crushing it outside them.")]
            public float pressureTargetInset = 0f;

            public float pressureStartDelay = -1f;
            public float pressureSpeed = 0f;

            [Header("Pressure profile - what the squeeze is FOR on this map")]
            [Tooltip("Config: keep the scene's slabs (the overrides above still apply). Park: walls " +
                     "never move. Chase: one wall sweeps from behind the spawn toward +X and halts " +
                     "short of the goal. Corridor: left and right close on a lane. Collapse: all " +
                     "four sides close on a central pit so the field is forced into a melee.")]
            public PressureProfile pressureProfile = PressureProfile.Config;

            [Tooltip("Collapse only: how far the front/back walls travel (X uses pressureTargetInset).")]
            public float pressureTargetInsetZ = 12f;

            [Header("Format overrides (zero / negative keeps the config)")]
            [Tooltip("Hearts per racer. 1 = sudden death.")]
            public float maxHealth = 0f;

            [Tooltip("Weapons on the map. 0 is a valid override (no weapons); -1 keeps the config.")]
            public int weaponCount = -1;

            [Tooltip("Racers this round. 0 keeps the config; -1 spawns nobody (asset review scenes).")]
            public int racerCount = 0;

            [Tooltip("Split the field into this many colour teams (Blocks assignment).")]
            public int teamCount = 0;

            [Tooltip("Portrait (9:16) round: the court is framed on the full width with no HUD reserve.")]
            public bool portrait = false;

            [Tooltip("ReachGoal only: how many racers must finish before the round ends (winner is still the first). 0 = keep the config value.")]
            public int requiredFinishers = 0;

            [Header("Mode")]
            [Tooltip("Extra rule set on top of the win condition (Infection, Hot Potato, Lucky Block, Paint War).")]
            public ModeKind modeKind = ModeKind.None;

            [Tooltip("Optional rule line for the HUD strip and round card.")]
            public string ruleLabel = "";

            [Tooltip("Knockout formats: palette slots of the racers in this round (empty = all).")]
            public List<int> paletteIndices = new List<int>();

            [Tooltip("Elimination Race: how many non-finishers are out after this round. 0 = not a knockout round.")]
            public int eliminateCount = 0;

            [Tooltip("Grand Prix: award 10-8-6-5-4-3-2-1 by finish order and carry a standings table across rounds.")]
            public bool grandPrix = false;
        }

        /// <summary>The squeeze's job on a given map. See RoundSpec.pressureProfile.</summary>
        public enum PressureProfile
        {
            Config = 0,
            Park = 1,
            Chase = 2,
            Corridor = 3,
            Collapse = 4,
            /// <summary>Portrait courts: one wall sweeps down from the top edge (+Z) toward the goal at the bottom.</summary>
            ChaseDown = 5,
        }

        /// <summary>Team colours dealt when a round asks for teams. Red vs blue first, always.</summary>
        private static readonly (string, Color)[] TeamColors =
        {
            ("RED",    new Color(1f, 0.2f, 0.2f)),
            ("BLUE",   new Color(0.25f, 0.45f, 1f)),
            ("GREEN",  new Color(0.15f, 0.85f, 0.3f)),
            ("YELLOW", new Color(1f, 0.9f, 0.2f)),
        };

        [SerializeField] private List<RoundSpec> rounds = new List<RoundSpec>();

        [Header("Card timing (seconds)")]
        [SerializeField] private float introDuration = 3.5f;
        [SerializeField] private float roundCardDuration = 2.2f;
        [SerializeField] private float winnerCardDuration = 3f;

        private SimulationBootstrap _bootstrap;
        private EpisodeCardOverlay _cards;
        private readonly List<Color> _roundWinners = new List<Color>();

        // Knockout / points state, keyed by palette slot so identities survive across rounds.
        private List<int> _activeSlots;
        private readonly Dictionary<int, int> _points = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _slotNames = new Dictionary<int, string>();
        private readonly Dictionary<int, Color> _slotColors = new Dictionary<int, Color>();
        private static readonly int[] GrandPrixPoints = { 10, 8, 6, 5, 4, 3, 2, 1 };

        public int CurrentRound { get; private set; }
        public bool Finished { get; private set; }
        public IReadOnlyList<RoundSpec> Rounds => rounds;

        public void SetRounds(List<RoundSpec> value) => rounds = value;

        private void Awake()
        {
            _bootstrap = GetComponent<SimulationBootstrap>();
            _bootstrap.SetBuildOnStart(false);
        }

        private void Start()
        {
            _cards = EpisodeCardOverlay.Create(transform);
            if (rounds.Count > 0 && rounds[0].portrait) _cards.SetPortrait(true);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (rounds.Count == 0)
            {
                Debug.LogWarning("[CubeSim] EpisodeDirector has no rounds; building the plain episode.");
                _bootstrap.Build();
                yield break;
            }

            for (int i = 0; i < rounds.Count; i++)
            {
                CurrentRound = i + 1;
                RoundSpec round = rounds[i];

                SimulationConfig config = _bootstrap.ResolveConfigTemplate();
                config.seed = round.seed;

                if (round.eliminateCount > 0)
                {
                    if (_activeSlots == null)
                    {
                        int total = round.racerCount > 0 ? round.racerCount : config.racers.count;
                        _activeSlots = new List<int>();
                        for (int s = 0; s < total; s++) _activeSlots.Add(s);
                    }
                    round.paletteIndices = new List<int>(_activeSlots);
                    round.requiredFinishers = Mathf.Max(1, _activeSlots.Count - round.eliminateCount);
                }
                config.arena.mode = ArenaMode.Authored;
                config.arena.arenaId = round.arenaId;
                config.endRules.winCondition = round.winCondition;
                config.endRules.eliminateCount = round.eliminateCount;
                config.endRules.loopOnEnd = false;
                if (round.maxDuration > 0f) config.endRules.maxDuration = round.maxDuration;
                if (round.requiredFinishers > 0) config.endRules.requiredFinishers = round.requiredFinishers;
                config.mode.kind = round.modeKind;
                if (!string.IsNullOrEmpty(round.ruleLabel)) config.mode.ruleLabel = round.ruleLabel;
                if (round.paletteIndices != null && round.paletteIndices.Count > 0)
                {
                    config.racers.paletteIndices = new List<int>(round.paletteIndices);
                    config.racers.count = round.paletteIndices.Count;
                }

                ApplyPressureProfile(config, round);

                if (round.portrait)
                {
                    // A 9:16 frame has no room for a side column; the court fills the width.
                    config.camera.leftReserve = 0f;
                    // Reserve exactly what the HUD strip needs: 150 px top margin, one 44 px row
                    // per three racers, the 76 px rule strip and a little air, on a 1920 px frame.
                    int hudRacers = round.racerCount > 0 ? round.racerCount : Mathf.Max(1, config.racers.count);
                    int hudLines = Mathf.CeilToInt(hudRacers / 3f);
                    config.camera.topReserve = Mathf.Clamp((150f + hudLines * 44f + 76f + 24f) / 1920f, 0.16f, 0.34f);
                    config.camera.margin = 1.03f;
                }
                _bootstrap.SetPortraitHud(round.portrait);

                // Format overrides: the same scene can host a sudden-death round, a weapon
                // frenzy, or a team battle without a different base config.
                if (round.maxHealth > 0f)
                {
                    config.racers.maxHealth = round.maxHealth;
                }

                if (round.weaponCount >= 0) config.weapons.count = round.weaponCount;
                if (round.racerCount > 0) config.racers.count = round.racerCount;
                else if (round.racerCount < 0) config.racers.count = 0;

                if (round.teamCount > 0)
                {
                    int teamCount = Mathf.Min(round.teamCount, TeamColors.Length);
                    config.racers.teams = new List<Racers.TeamDefinition>();
                    for (int t = 0; t < teamCount; t++)
                    {
                        config.racers.teams.Add(new Racers.TeamDefinition(TeamColors[t].Item1, TeamColors[t].Item2));
                    }

                    // RoundRobin matches the spawn dealer (racer i -> area i % areas), so with two
                    // teams and two pens every RED lands left and every BLUE lands right. Blocks
                    // would scatter both teams across both pens.
                    config.racers.teamAssignment = Racers.TeamAssignment.RoundRobin;
                    config.racers.colorSource = Racers.RacerColorSource.Team;
                    // A team war without this is just FFA in matching shirts.
                    config.weapons.friendlyFire = false;
                }

                _bootstrap.RunConfig(config);
                _bootstrap.Paused = true;

                if (_bootstrap.Runner != null)
                {
                    foreach (Racer racer in _bootstrap.Runner.Racers)
                    {
                        _slotNames[racer.PaletteIndex] = racer.DisplayName;
                        _slotColors[racer.PaletteIndex] = racer.Color;
                        if (round.grandPrix && _points.TryGetValue(racer.PaletteIndex, out int pts)) racer.Score = pts;
                    }
                }

                // The opener runs over the freshly built (frozen) first arena, so the bet is placed
                // while the field is already on screen.
                if (i == 0)
                {
                    _cards.ShowIntro(RacerColors());
                    yield return new WaitForSecondsRealtime(introDuration);
                }

                _cards.ShowRound(CurrentRound, rounds.Count, round.arenaId, _bootstrap != null ? _bootstrap.RuleLine : null);
                yield return new WaitForSecondsRealtime(roundCardDuration);

                _cards.Hide();
                _bootstrap.Paused = false;

                while (_bootstrap.Runner != null && !_bootstrap.Runner.Finished) yield return null;
                if (_bootstrap.Runner == null) yield break;

                SimulationResult result = _bootstrap.Runner.Result;
                Color winner = result.winnerColor;
                _roundWinners.Add(winner);

                _bootstrap.Paused = true;
                _cards.ShowWinner(winner, DescribeOutcome(result), result.winnerName);
                yield return new WaitForSecondsRealtime(winnerCardDuration);

                if (round.grandPrix)
                {
                    var finishers = _bootstrap.Runner.Goals != null ? _bootstrap.Runner.Goals.Finishers : null;
                    if (finishers != null)
                    {
                        for (int f = 0; f < finishers.Count && f < GrandPrixPoints.Length; f++)
                        {
                            int slot = finishers[f].PaletteIndex;
                            _points[slot] = (_points.TryGetValue(slot, out int p) ? p : 0) + GrandPrixPoints[f];
                        }
                    }
                    _cards.ShowStandings(Standings(), CurrentRound, rounds.Count);
                    yield return new WaitForSecondsRealtime(winnerCardDuration);
                }

                if (round.eliminateCount > 0)
                {
                    var outNames = new List<string>();
                    var outColors = new List<Color>();
                    foreach (Racer racer in _bootstrap.Runner.Racers)
                    {
                        if (racer.ReachedGoal) continue;
                        _activeSlots.Remove(racer.PaletteIndex);
                        outNames.Add(racer.DisplayName);
                        outColors.Add(racer.Color);
                    }
                    if (outNames.Count > 0)
                    {
                        _cards.ShowEliminated(outNames, outColors, _activeSlots.Count);
                        yield return new WaitForSecondsRealtime(winnerCardDuration);
                    }
                    if (_activeSlots.Count <= 1) break;
                }
            }

            // The last round's winner takes the episode. A one-round short already ended on its
            // winner card; a second "champion" card would only repeat it.
            Color champion = _roundWinners.Count > 0 ? _roundWinners[_roundWinners.Count - 1] : Color.white;
            if (_points.Count > 0)
            {
                int bestSlot = -1, bestPts = -1;
                foreach (var kv in _points) if (kv.Value > bestPts) { bestPts = kv.Value; bestSlot = kv.Key; }
                if (bestSlot >= 0 && _slotColors.TryGetValue(bestSlot, out Color c)) champion = c;
            }
            else if (_activeSlots != null && _activeSlots.Count == 1 && _slotColors.TryGetValue(_activeSlots[0], out Color last))
            {
                champion = last;
            }
            if (rounds.Count > 1) _cards.ShowPodium(champion, _roundWinners);
            Finished = true;
        }

        private List<(string name, Color color, int points)> Standings()
        {
            var list = new List<(string, Color, int)>();
            foreach (var kv in _points)
            {
                _slotNames.TryGetValue(kv.Key, out string name);
                _slotColors.TryGetValue(kv.Key, out Color color);
                list.Add((name ?? ("#" + kv.Key), color, kv.Value));
            }
            list.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            return list;
        }

        /// <summary>
        /// Builds the slab set a profile asks for. Chase and Collapse replace the scene's slabs
        /// outright (a Collapse needs four sides, the scene ships two); Config only nudges what
        /// is already there, which keeps the older hand-tuned scenes behaving as before.
        /// </summary>
        public static void ApplyPressureProfile(SimulationConfig config, RoundSpec round)
        {
            float delay = round.pressureStartDelay >= 0f ? round.pressureStartDelay : 6f;
            float speed = round.pressureSpeed > 0f ? round.pressureSpeed : 0.2f;
            float insetX = round.pressureTargetInset > 0f ? round.pressureTargetInset : 22f;
            float insetZ = round.pressureTargetInsetZ > 0f ? round.pressureTargetInsetZ : 12f;

            PressureSlabConfig Slab(PressureSide side, float target) => new PressureSlabConfig
            {
                side = side, startInset = 0.5f, targetInset = target, startDelay = delay, speed = speed
            };

            switch (round.pressureProfile)
            {
                case PressureProfile.Park:
                    foreach (PressureSlabConfig slab in config.pressure.slabs)
                    {
                        slab.startDelay = 100000f;
                        slab.speed = 0f;
                    }
                    break;

                case PressureProfile.Chase:
                    // One wall behind the spawn line, herding the field at the goal (+X) and
                    // stopping short of it - a symmetric squeeze would roll over the finish.
                    config.pressure.mode = Arena.PressureMode.LinearSlabs;
                    config.pressure.slabs = new List<PressureSlabConfig> { Slab(PressureSide.Left, insetX) };
                    break;

                case PressureProfile.ChaseDown:
                    config.pressure.mode = Arena.PressureMode.LinearSlabs;
                    config.pressure.slabs = new List<PressureSlabConfig> { Slab(PressureSide.Front, insetX) };
                    break;

                case PressureProfile.Corridor:
                    config.pressure.mode = Arena.PressureMode.LinearSlabs;
                    config.pressure.slabs = new List<PressureSlabConfig>
                    {
                        Slab(PressureSide.Left, insetX), Slab(PressureSide.Right, insetX),
                    };
                    break;

                case PressureProfile.Collapse:
                    // All four sides close on a central pit: nobody gets to orbit the edges.
                    config.pressure.mode = Arena.PressureMode.LinearSlabs;
                    config.pressure.slabs = new List<PressureSlabConfig>
                    {
                        Slab(PressureSide.Left, insetX), Slab(PressureSide.Right, insetX),
                        Slab(PressureSide.Back, insetZ), Slab(PressureSide.Front, insetZ),
                    };
                    break;

                default:
                    foreach (PressureSlabConfig slab in config.pressure.slabs)
                    {
                        if (round.pressureTargetInset > 0f) slab.targetInset = round.pressureTargetInset;
                        if (round.pressureStartDelay >= 0f) slab.startDelay = round.pressureStartDelay;
                        if (round.pressureSpeed > 0f) slab.speed = round.pressureSpeed;
                    }
                    break;
            }
        }

        private List<Color> RacerColors()
        {
            var colors = new List<Color>();
            if (_bootstrap.Runner == null) return colors;

            foreach (Racers.Racer racer in _bootstrap.Runner.Racers) colors.Add(racer.Color);
            return colors;
        }

        private static string DescribeOutcome(SimulationResult result)
        {
            switch (result.outcome)
            {
                case SimulationOutcome.GoalReached: return "first to the goal";
                case SimulationOutcome.Winner: return string.IsNullOrEmpty(result.winnerTeamName) ? "last one standing" : "last team standing";
                case SimulationOutcome.TimeLimit: return "survived the clock";
                default: return "";
            }
        }
    }
}
