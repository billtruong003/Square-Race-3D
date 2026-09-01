using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The collision-plays-the-song instrument: every qualifying impact performs the NEXT note of a
    /// pre-arranged melody, so the simulation itself plays the music. Sparse wandering at the start
    /// makes sparse notes; the finale's contact storm races through the theme - tension raises the
    /// tempo for free, because tempo IS the event rate.
    ///
    /// The tune is "In the Hall of the Mountain King" (Grieg, public domain), picked precisely
    /// because it is built to accelerate. Chords punctuate story beats: a kill lands a low minor
    /// stab, a goal lands the major resolution.
    ///
    /// No audio assets: the pluck is synthesized once at startup (harmonics with exponential
    /// decay - an electric-piano/marimba hybrid that fits the toon look), and every pitch is the
    /// same clip resampled. Cosmetic only; nothing here touches simulation state or its RNG.
    /// </summary>
    public sealed class CollisionMelody
    {
        private readonly MelodySongs.Song _song;

        /// <summary>Minor stab under a death, major lift under a goal. Offsets from the root.</summary>
        private static readonly int[] KillChord = { -12, -9, -5 };
        private static readonly int[] GoalChord = { 0, 4, 7, 12 };

        private const int SampleRate = 32000;

        /// <summary>Two notes closer than this are one gesture; the later one is dropped, not queued.</summary>
        private const float MinNoteSpacing = 0.075f;

        /// <summary>
        /// Hard tempo ceiling. Without it a block-heavy map turns the theme into a drone - fifty
        /// racers grinding a mega-block would fire notes as fast as the spacing gate allows,
        /// forever. Above this rate extra impacts are simply dropped, so the music tops out at
        /// "frantic" and stays there instead of climbing into noise.
        /// </summary>
        private const float MaxNotesPerSecond = 7f;

        private readonly AudioSource[] _voices;
        private readonly AudioClip _pluck;
        private int _step;
        private int _nextVoice;
        private float _lastNoteTime;
        private float _time;
        private float _noteBudget = MaxNotesPerSecond;

        public int NotesPlayed { get; private set; }
        public string SongName => _song.Name;

        /// <summary>Each episode hands in a song - the "random track per play" of the reference channels.</summary>
        public CollisionMelody(Transform parent, MelodySongs.Song song, int voices = 10)
        {
            _song = song ?? MelodySongs.Book[0];

            var root = new GameObject("Melody").transform;
            root.SetParent(parent, false);

            _pluck = SynthesizePluck(_song.RootFrequency);

            _voices = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Note_" + i);
                go.transform.SetParent(root, false);
                _voices[i] = go.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
                _voices[i].clip = _pluck;
            }
        }

        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            _noteBudget = Mathf.Min(MaxNotesPerSecond, _noteBudget + MaxNotesPerSecond * deltaTime);
        }

        /// <summary>An impact performs the next melody note. Returns false when it was swallowed.</summary>
        public bool PlayNextNote(float volume = 0.4f)
        {
            if (_time - _lastNoteTime < MinNoteSpacing) return false;
            if (_noteBudget < 1f) return false;
            _noteBudget -= 1f;

            int[] notes = _song.Notes;
            int semitone = notes[_step];
            _step = (_step + 1) % notes.Length;
            if (semitone == 99)
            {
                // A phrase breath: consume the rest and sound the following note instead.
                semitone = notes[_step];
                _step = (_step + 1) % notes.Length;
            }

            _lastNoteTime = _time;
            PlaySemitone(semitone, volume);
            NotesPlayed++;
            return true;
        }

        /// <summary>Story-beat chords, exempt from the melody's spacing gate.</summary>
        public void PlayKillChord() => PlayChord(KillChord, 0.5f);

        public void PlayGoalChord() => PlayChord(GoalChord, 0.55f);

        private void PlayChord(int[] semitones, float volume)
        {
            for (int i = 0; i < semitones.Length; i++)
            {
                PlaySemitone(semitones[i], volume * 0.7f);
            }
        }

        private void PlaySemitone(int semitone, float volume)
        {
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.pitch = Mathf.Pow(2f, semitone / 12f);
            voice.volume = volume;
            voice.Play();
        }

        /// <summary>
        /// One synthesized pluck at the root pitch: a handful of harmonics with a fast exponential
        /// decay and a soft attack. Deliberately simple - it has to survive being resampled two
        /// octaves in both directions.
        /// </summary>
        private static AudioClip SynthesizePluck(float rootFrequency)
        {
            const float duration = 1.1f;
            int samples = (int)(SampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // Soft 4ms attack, then a two-stage decay: a bright transient and a mellow body.
                float attack = Mathf.Clamp01(t / 0.004f);
                float body = Mathf.Exp(-3.2f * t);
                float transientDecay = Mathf.Exp(-18f * t);

                float phase = 2f * Mathf.PI * rootFrequency * t;
                float value =
                    Mathf.Sin(phase) * 0.60f * body +
                    Mathf.Sin(phase * 2f) * 0.22f * body +
                    Mathf.Sin(phase * 3f) * 0.10f * transientDecay +
                    Mathf.Sin(phase * 4f) * 0.08f * transientDecay;

                data[i] = value * attack * 0.62f;
            }

            var clip = AudioClip.Create("CubeSimPluck", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
