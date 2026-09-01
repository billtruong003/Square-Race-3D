using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        }

        [SerializeField] private List<RoundSpec> rounds = new List<RoundSpec>();

        [Header("Card timing (seconds)")]
        [SerializeField] private float introDuration = 3.5f;
        [SerializeField] private float roundCardDuration = 2.2f;
        [SerializeField] private float winnerCardDuration = 3f;

        private SimulationBootstrap _bootstrap;
        private EpisodeCardOverlay _cards;
        private readonly List<Color> _roundWinners = new List<Color>();

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
                config.arena.mode = ArenaMode.Authored;
                config.arena.arenaId = round.arenaId;
                config.endRules.winCondition = round.winCondition;
                config.endRules.loopOnEnd = false;
                if (round.maxDuration > 0f) config.endRules.maxDuration = round.maxDuration;

                foreach (PressureSlabConfig slab in config.pressure.slabs)
                {
                    if (round.pressureTargetInset > 0f) slab.targetInset = round.pressureTargetInset;
                    if (round.pressureStartDelay >= 0f) slab.startDelay = round.pressureStartDelay;
                    if (round.pressureSpeed > 0f) slab.speed = round.pressureSpeed;
                }

                _bootstrap.RunConfig(config);
                _bootstrap.Paused = true;

                // The opener runs over the freshly built (frozen) first arena, so the bet is placed
                // while the field is already on screen.
                if (i == 0)
                {
                    _cards.ShowIntro(RacerColors());
                    yield return new WaitForSecondsRealtime(introDuration);
                }

                _cards.ShowRound(CurrentRound, rounds.Count, round.arenaId);
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
            }

            // The last round's winner takes the episode.
            _cards.ShowPodium(_roundWinners[_roundWinners.Count - 1], _roundWinners);
            Finished = true;
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
                case SimulationOutcome.Winner: return "last one standing";
                case SimulationOutcome.TimeLimit: return "survived the clock";
                default: return "";
            }
        }
    }
}
