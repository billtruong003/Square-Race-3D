using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Builds the audio library. The palette is four one-shots and nothing else - the collision
    /// piano is the entire soundtrack, so there is no music bed and no ambient foley. Hits use the
    /// user-supplied hitmarker, kills the Among-Us sting, eating its pop, and reaching the goal the
    /// win sparkle from Epic Toon FX (which stays unmodified in its pack).
    /// </summary>
    public static class AudioAssetBuilder
    {
        public const string LibraryPath = "Assets/CubeSim/Data/AudioLibrary.asset";
        private const string EtfxSound = "Assets/Epic Toon FX/Sound/";
        private const string Sfx = "Assets/CubeSim/Audio/SFX/";
        private const string BgmFolder = "Assets/BGM_LOOP";

        private struct Pick
        {
            public SimSoundId Id;
            public string Clip;
            public float Volume;
            public float PitchMin;
            public float PitchMax;
        }

        private static readonly Pick[] Picks =
        {
            // A heart lost but the pet lives: the hitmarker, dead straight, no pitch wobble.
            new Pick { Id = SimSoundId.RacerHit,   Clip = Sfx + "Hitmarker.mp3",                Volume = 0.55f, PitchMin = 1.0f, PitchMax = 1.0f },

            // The final heart: the kill sting replaces the hitmarker entirely on that impact.
            new Pick { Id = SimSoundId.RacerDeath, Clip = Sfx + "AmongUsKill.wav",              Volume = 0.6f,  PitchMin = 1.0f, PitchMax = 1.0f },
            new Pick { Id = SimSoundId.CrushDeath, Clip = Sfx + "AmongUsKill.wav",              Volume = 0.6f,  PitchMin = 1.0f, PitchMax = 1.0f },

            // Eating. Swap the clip for the Among-Us eat sample once it lands in Audio/SFX.
            new Pick { Id = SimSoundId.FoodEaten,  Clip = Sfx + "AmongUsEat.wav",               Volume = 0.4f,  PitchMin = 1.25f, PitchMax = 1.45f },

            // The win at the end.
            new Pick { Id = SimSoundId.GoalReached, Clip = EtfxSound + "etfx_explosion_sparkle2.wav", Volume = 0.55f, PitchMin = 1.0f, PitchMax = 1.0f },

            // Breakable objects - the consumable/glass-style props. A bright pitched-up crack per
            // hit, and the user-supplied real glass shatter when the thing gives way.
            new Pick { Id = SimSoundId.WallHit,   Clip = EtfxSound + "etfx_target_hit.wav", Volume = 0.35f, PitchMin = 1.25f, PitchMax = 1.45f },
            new Pick { Id = SimSoundId.WallBreak, Clip = Sfx + "GlassBreak.mp3",            Volume = 0.55f, PitchMin = 1.0f,  PitchMax = 1.0f },
        };

        /// <summary>Stand-in for the eat pop until the real Among-Us eat sample is provided.</summary>
        private const string EatFallback = EtfxSound + "etfx_pop_balloon.wav";

        [MenuItem("CubeSim/Build Audio Library", priority = 14)]
        public static AudioLibrary BuildLibrary()
        {
            Directory.CreateDirectory("Assets/CubeSim/Data");

            var entries = new List<AudioLibrary.Entry>();
            int missing = 0;

            foreach (Pick pick in Picks)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pick.Clip);
                if (clip == null && pick.Id == SimSoundId.FoodEaten)
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EatFallback);
                }

                if (clip == null)
                {
                    Debug.LogWarning($"[CubeSim] Audio clip missing: {pick.Clip}; {pick.Id} will be silent.");
                    missing++;
                    continue;
                }

                entries.Add(new AudioLibrary.Entry
                {
                    id = pick.Id,
                    clip = clip,
                    volume = pick.Volume,
                    pitchMin = pick.PitchMin,
                    pitchMax = pick.PitchMax
                });
            }

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<AudioLibrary>();

            // Legacy single-track bed stays empty; music comes from the mode below.
            library.Configure(null, 0f, entries);

            // The BGM pool: every clip under Assets/BGM_LOOP, shuffled at runtime without
            // repeats. Flip the mode on the asset to CollisionMelody to get the piano back.
            var pool = new List<AudioClip>();
            if (Directory.Exists(BgmFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { BgmFolder }))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (clip != null) pool.Add(clip);
                }
            }

            library.ConfigureMusic(
                pool.Count > 0 ? AudioMusicMode.BackgroundPool : AudioMusicMode.CollisionMelody,
                pool, 0.1f);

            if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CubeSim] Audio library built: {entries.Count} sounds ({missing} missing), " +
                      $"bgm pool {pool.Count} tracks, mode {library.MusicMode}.");
            return AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        }
    }
}
