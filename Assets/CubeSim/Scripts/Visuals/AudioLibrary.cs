using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>Every sound the presentation layer can play. Ids, not clips, cross system borders.</summary>
    public enum SimSoundId
    {
        None = 0,
        WallBounce = 1,
        RacerBounce = 2,
        WeaponPickup = 3,
        WeaponDrop = 4,
        RangedShot = 5,
        ProjectileHitWall = 6,
        ProjectileHitRacer = 7,
        MeleeHit = 8,
        RacerDeath = 9,
        CrushDeath = 10,
        GoalReached = 11,
        WallBreak = 12,
        WallHit = 13,
        RacerHit = 14,
        FoodEaten = 15
    }

    /// <summary>What carries the music of an episode.</summary>
    public enum AudioMusicMode
    {
        /// <summary>Collisions perform a famous tune note by note; no background track.</summary>
        CollisionMelody = 0,

        /// <summary>
        /// A quiet background track from the BGM pool, shuffled without repeats; collision notes
        /// are off and only the story-beat SFX speak over it.
        /// </summary>
        BackgroundPool = 1
    }

    /// <summary>
    /// Maps sound ids to clips, mirroring the VFX library. The mix numbers encode what was measured
    /// off the reference channel: a music bed around -30 LUFS with sparse one-shots spiking well
    /// above it, so per-entry volumes run high while the music volume runs very low.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "CubeSim/Audio Library", order = 5)]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SimSoundId id;
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 0.9f;

            [Tooltip("Random pitch range per play, so repeated events do not machine-gun one sample.")]
            public float pitchMin = 0.95f;

            public float pitchMax = 1.05f;
        }

        [Tooltip("Looping background track. Mixed far below the effects on purpose.")]
        [SerializeField] private AudioClip music;

        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.16f;

        [Tooltip("How music happens: collision melody, or a shuffled background pool.")]
        [SerializeField] private AudioMusicMode musicMode = AudioMusicMode.CollisionMelody;

        [Tooltip("Tracks the BackgroundPool mode shuffles through - no repeats until all played.")]
        [SerializeField] private List<AudioClip> bgmPool = new List<AudioClip>();

        [Tooltip("BGM level. Sits well under the SFX - background means background.")]
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.1f;

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public AudioClip Music => music;
        public float MusicVolume => musicVolume;
        public AudioMusicMode MusicMode => musicMode;
        public IReadOnlyList<AudioClip> BgmPool => bgmPool;
        public float BgmVolume => bgmVolume;
        public IReadOnlyList<Entry> Entries => entries;

        public Entry Find(SimSoundId id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id) return entries[i];
            }

            return null;
        }

        public void Configure(AudioClip musicClip, float musicLevel, List<Entry> value)
        {
            music = musicClip;
            musicVolume = musicLevel;
            entries = value;
        }

        public void ConfigureMusic(AudioMusicMode mode, List<AudioClip> pool, float volume)
        {
            musicMode = mode;
            bgmPool = pool ?? new List<AudioClip>();
            bgmVolume = volume;
        }
    }
}
