using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The recording's audio track, built from the event log instead of the live mix.
    ///
    /// The recorder caps the frame rate, so the video clock is "one frame = 1/60 s" no matter
    /// how long a frame took to render; the live DSP runs on the wall clock, which is why a tap
    /// on it drifted late, and AudioRenderer returns silence on this editor. The episode's audio
    /// is only a background track plus one-shots, so the track is rebuilt offline: every sound
    /// is logged with the capture frame it started on, and at the end the clips are mixed into a
    /// WAV whose length is exactly frames / fps. Sync cannot drift because both tracks share the
    /// frame counter.
    ///
    /// Clips must be readable (import load type Decompress On Load, which the packs use).
    /// </summary>
    public sealed class OfflineAudioTrack
    {
        private struct Shot { public AudioClip Clip; public float Volume; public float Pitch; public int Frame; }
        private struct Bgm  { public AudioClip Clip; public float Volume; public int Frame; public float Offset; }

        public static OfflineAudioTrack Active { get; private set; }

        private readonly string _path;
        private readonly int _fps;
        private readonly int _sampleRate;
        private readonly int _startFrame;
        private readonly List<Shot> _shots = new List<Shot>();
        private readonly List<Bgm> _bgm = new List<Bgm>();
        private int _endFrame = -1;

        public string OutputPath => _path;
        public int Frames => Mathf.Max(0, (_endFrame >= 0 ? _endFrame : Time.frameCount) - _startFrame);
        public double CapturedSeconds => (double)Frames / _fps;
        public int OneShots => _shots.Count;

        private OfflineAudioTrack(string path, int fps)
        {
            _path = path;
            _fps = fps;
            _sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _startFrame = Time.frameCount;
        }

        /// <summary>Starts logging. Call on the same frame the video recorder starts.</summary>
        public static OfflineAudioTrack Begin(string wavPath, int fps = 60)
        {
            Active = new OfflineAudioTrack(wavPath, fps);
            // A track already playing when the capture starts is logged from its current position.
            SimAudioSystem.Latest?.SeedOfflineTrack(Active);
            Debug.Log($"[CubeSim] Offline audio track logging to {wavPath} ({Active._sampleRate} Hz, {fps} fps clock).");
            return Active;
        }

        public void LogOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            _shots.Add(new Shot { Clip = clip, Volume = volume, Pitch = pitch <= 0f ? 1f : pitch, Frame = Time.frameCount - _startFrame });
        }

        /// <summary>A background track starting now. Tracks chain in video time in log order.</summary>
        public void LogBgm(AudioClip clip, float volume, float secondsAlreadyPlayed = 0f)
        {
            if (clip == null) return;
            _bgm.Add(new Bgm { Clip = clip, Volume = volume, Frame = Time.frameCount - _startFrame, Offset = Mathf.Max(0f, secondsAlreadyPlayed) });
        }

        /// <summary>Freezes the frame count, mixes and writes the WAV. Returns seconds written.</summary>
        public double Finish()
        {
            if (Active == this) Active = null;
            _endFrame = Time.frameCount;

            int frames = Frames;
            long totalSamples = (long)frames * _sampleRate / _fps;
            if (totalSamples <= 0) { Debug.LogWarning("[CubeSim] Offline audio: no frames captured."); return 0; }

            var mix = new float[totalSamples * 2];

            // Background: the first logged track starts on its frame; each next one starts where
            // the previous ended in video time, exactly as the pool player chains them.
            long cursor = 0;
            for (int i = 0; i < _bgm.Count; i++)
            {
                long start = i == 0 ? FrameToSample(_bgm[i].Frame) : cursor;
                if (start >= totalSamples) break;
                long written = MixClip(mix, _bgm[i].Clip, _bgm[i].Volume, 1f, start, _bgm[i].Offset);
                cursor = start + written;
            }

            for (int i = 0; i < _shots.Count; i++)
            {
                Shot s = _shots[i];
                MixClip(mix, s.Clip, s.Volume, s.Pitch, FrameToSample(s.Frame));
            }

            // Soft limiter so a bomb on top of the BGM cannot clip.
            float peak = 0f;
            for (int i = 0; i < mix.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
            float gain = peak > 0.98f ? 0.98f / peak : 1f;

            WriteWav(mix, gain);
            Debug.Log($"[CubeSim] Offline audio written: {CapturedSeconds:F2}s, {_shots.Count} one-shots, {_bgm.Count} bgm tracks, peak {peak:F2}.");
            return CapturedSeconds;
        }

        private long FrameToSample(int frame) => (long)Mathf.Max(0, frame) * _sampleRate / _fps;

        /// <summary>Adds one clip into the stereo mix at the given output sample. Returns samples covered.</summary>
        private long MixClip(float[] mix, AudioClip clip, float volume, float pitch, long start, float skipSeconds = 0f)
        {
            if (clip == null || clip.samples <= 0) return 0;
            long skip = (long)(skipSeconds * clip.frequency);
            int channels = clip.channels;
            var data = new float[clip.samples * channels];
            if (!clip.GetData(data, 0))
            {
                Debug.LogWarning($"[CubeSim] Offline audio: cannot read {clip.name} (set Load Type to Decompress On Load).");
                return 0;
            }

            // Source samples advanced per output sample: clip rate over output rate, times pitch.
            double step = (double)clip.frequency / _sampleRate * pitch;
            long outCount = (long)((clip.samples - skip) / step);
            if (outCount <= 0) return 0;
            long totalFrames = mix.Length / 2;
            long written = 0;

            for (long o = 0; o < outCount; o++)
            {
                long dst = start + o;
                if (dst >= totalFrames) break;
                if (dst < 0) continue;

                double pos = skip + o * step;
                int i0 = (int)pos;
                if (i0 >= clip.samples - 1) break;
                float t = (float)(pos - i0);

                float l, r;
                if (channels == 1)
                {
                    l = r = Mathf.Lerp(data[i0], data[i0 + 1], t);
                }
                else
                {
                    l = Mathf.Lerp(data[i0 * channels], data[(i0 + 1) * channels], t);
                    r = Mathf.Lerp(data[i0 * channels + 1], data[(i0 + 1) * channels + 1], t);
                }

                mix[dst * 2] += l * volume;
                mix[dst * 2 + 1] += r * volume;
                written++;
            }

            return written;
        }

        private void WriteWav(float[] mix, float gain)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using var stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            using var w = new BinaryWriter(stream);
            long dataBytes = (long)mix.Length * 2;
            const short channels = 2;

            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write((int)(36 + dataBytes));
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write(channels);
            w.Write(_sampleRate);
            w.Write(_sampleRate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write((int)dataBytes);

            for (int i = 0; i < mix.Length; i++)
            {
                float v = Mathf.Clamp(mix[i] * gain, -1f, 1f);
                w.Write((short)(v * 32767f));
            }
        }
    }
}
