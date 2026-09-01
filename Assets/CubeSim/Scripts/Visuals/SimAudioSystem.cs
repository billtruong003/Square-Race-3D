using System.Collections.Generic;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Plays the episode's sound. There is deliberately no music bed: the collision-performed
    /// piano IS the soundtrack - heard blind, an episode is one long fast piano piece - and the
    /// only one-shots are the four story beats (hitmarker, kill sting, eat pop, win sparkle).
    ///
    /// Purely cosmetic: reads simulation state, never writes it, driven from Update.
    /// </summary>
    public sealed class SimAudioSystem
    {
        /// <summary>Events that always play, regardless of gating. The story beats.</summary>
        private static readonly HashSet<SimSoundId> AlwaysPlay = new HashSet<SimSoundId>
        {
            SimSoundId.RacerHit, SimSoundId.RacerDeath, SimSoundId.CrushDeath,
            SimSoundId.GoalReached, SimSoundId.FoodEaten
        };

        private const int Voices = 12;

        /// <summary>Ambient one-shots allowed per second once the gate opens.</summary>
        private const float AmbientRateLimit = 2.5f;

        /// <summary>Wall bounces stay silent until this few racers remain - the duel phase.</summary>
        private const int BounceAudibleAliveCount = 3;

        private readonly AudioLibrary _library;
        private readonly AudioSource[] _voices;
        private readonly Transform _root;
        private readonly CollisionMelody _melody;

        /// <summary>The collision-performed melody. Bounces play it; story beats chord under it.</summary>
        public CollisionMelody Melody => _melody;

        private readonly int[] _bounceCounts = new int[64];
        private readonly float[] _lastPlayTimes = new float[32];

        /// <summary>
        /// Same-sound retrigger floor. Two racers eating on the same frame, or two kills in one
        /// step, would land the identical sample twice as one doubled transient - the buzz the
        /// deadzones taught us about. One copy per gesture is enough.
        /// </summary>
        private const float SameSoundSpacing = 0.06f;
        private Racer[] _racers;
        private int _nextVoice;
        private float _ambientBudget;
        private float _lastAmbientTime;
        private float _time;

        public bool Enabled => _library != null;
        public int Played { get; private set; }

        public SimAudioSystem(AudioLibrary library, Transform parent)
        {
            _library = library;

            _root = new GameObject("Audio").transform;
            _root.SetParent(parent, false);

            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice_" + i);
                go.transform.SetParent(_root, false);
                _voices[i] = go.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f; // top-down camera: flat 2D mix, like the reference
            }

            // A different tune every play, like the reference channels rotating their tracks.
            // UnityEngine.Random is fine here: song choice is presentation, not simulation.
            _melody = new CollisionMelody(_root, MelodySongs.Pick(Random.Range(0, 1024)));
        }

        /// <summary>Hooks the per-racer state this system polls for bounce sounds.</summary>
        public void Bind(Racer[] racers)
        {
            _racers = racers;
            if (racers == null) return;

            for (int i = 0; i < racers.Length && i < _bounceCounts.Length; i++)
            {
                _bounceCounts[i] = racers[i].BounceCount;
            }
        }

        /// <summary>Event one-shot. Gating decides whether it actually sounds.</summary>
        public void Play(SimSoundId id)
        {
            if (_library == null || id == SimSoundId.None) return;

            AudioLibrary.Entry entry = _library.Find(id);
            if (entry?.clip == null) return;

            int slot = (int)id;
            if (slot >= 0 && slot < _lastPlayTimes.Length)
            {
                if (_time - _lastPlayTimes[slot] < SameSoundSpacing && _lastPlayTimes[slot] > 0f) return;
                _lastPlayTimes[slot] = _time;
            }

            if (!AlwaysPlay.Contains(id) && !TrySpendAmbientBudget()) return;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
            voice.PlayOneShot(entry.clip, entry.volume);
            Played++;
        }

        /// <summary>
        /// Runs the render-clock work: refills the ambient budget and performs the melody. Every
        /// bounce plays the NEXT note of the theme, so the piece speeds up exactly as the action
        /// does - a sparse plink while the field wanders, a sprint through the phrase in a finale
        /// contact storm. Polling the counters keeps the simulation loop free of audio concerns.
        /// </summary>
        public void Tick(float deltaTime, int aliveCount)
        {
            _time += deltaTime;
            _ambientBudget = Mathf.Min(2f, _ambientBudget + AmbientRateLimit * deltaTime);
            _melody?.Tick(deltaTime);

            if (_racers == null) return;

            for (int i = 0; i < _racers.Length && i < _bounceCounts.Length; i++)
            {
                Racer racer = _racers[i];
                int previous = _bounceCounts[i];
                _bounceCounts[i] = racer.BounceCount;

                if (!racer.IsActive || racer.BounceCount == previous) continue;

                // The last racers alive strike harder: same song, heavier hand.
                _melody?.PlayNextNote(aliveCount <= BounceAudibleAliveCount ? 0.55f : 0.32f);
            }
        }

        private bool TrySpendAmbientBudget()
        {
            // A minimum spacing on top of the budget, so two simultaneous ambient events do not
            // land as one double-loud transient.
            if (_time - _lastAmbientTime < 0.12f) return false;
            if (_ambientBudget < 1f) return false;

            _ambientBudget -= 1f;
            _lastAmbientTime = _time;
            return true;
        }
    }
}
