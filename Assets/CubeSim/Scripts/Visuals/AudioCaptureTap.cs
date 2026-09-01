using System;
using System.IO;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Records the final mixed audio to a WAV by sitting on the AudioListener's GameObject and
    /// tapping <c>OnAudioFilterRead</c> - the last stop in the DSP graph before output. This works
    /// no matter what the output device or the Unity Recorder are doing: the recorder's capture
    /// path proved to deliver silence on this setup, while the DSP graph itself demonstrably runs,
    /// so the episode pipeline writes its own audio and muxes it into the video afterwards.
    ///
    /// The callback runs on the audio thread; it only converts samples and hands them to a
    /// buffered stream. WAV header is back-filled on finish.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioCaptureTap : MonoBehaviour
    {
        private FileStream _stream;
        private BinaryWriter _writer;
        private int _sampleRate;
        private int _channels;
        private long _samplesWritten;
        private volatile bool _capturing;

        public string OutputPath { get; private set; }
        public bool Capturing => _capturing;
        public double CapturedSeconds => _channels > 0 && _sampleRate > 0
            ? (double)_samplesWritten / _channels / _sampleRate
            : 0;

        /// <summary>Attaches a tap to the scene's listener and starts writing.</summary>
        public static AudioCaptureTap Begin(string wavPath)
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                Debug.LogWarning("[CubeSim] No AudioListener to tap; audio capture skipped.");
                return null;
            }

            var tap = listener.GetComponent<AudioCaptureTap>();
            if (tap == null) tap = listener.gameObject.AddComponent<AudioCaptureTap>();

            tap.StartCapture(wavPath);
            return tap;
        }

        private void StartCapture(string wavPath)
        {
            Finish();

            OutputPath = wavPath;
            _sampleRate = AudioSettings.outputSampleRate;
            _channels = 0;
            _samplesWritten = 0;

            Directory.CreateDirectory(Path.GetDirectoryName(wavPath));
            _stream = new FileStream(wavPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _writer = new BinaryWriter(_stream);

            // Placeholder header; the real one lands in Finish once the sizes are known.
            _writer.Write(new byte[44]);
            _capturing = true;

            Debug.Log($"[CubeSim] Audio tap writing to {wavPath} ({_sampleRate} Hz).");
        }

        /// <summary>Closes the file and stamps the WAV header. Safe to call repeatedly.</summary>
        public void Finish()
        {
            if (_writer == null) return;

            _capturing = false;

            lock (_writer)
            {
                long dataBytes = _samplesWritten * 2;
                int channels = Mathf.Max(1, _channels);

                _stream.Seek(0, SeekOrigin.Begin);
                _writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                _writer.Write((int)(36 + dataBytes));
                _writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                _writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                _writer.Write(16);
                _writer.Write((short)1);                       // PCM
                _writer.Write((short)channels);
                _writer.Write(_sampleRate);
                _writer.Write(_sampleRate * channels * 2);     // byte rate
                _writer.Write((short)(channels * 2));          // block align
                _writer.Write((short)16);                      // bits per sample
                _writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                _writer.Write((int)dataBytes);

                _writer.Flush();
                _writer.Dispose();
                _writer = null;
                _stream = null;
            }

            Debug.Log($"[CubeSim] Audio tap closed: {CapturedSeconds:F1}s captured.");
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_capturing || _writer == null) return;

            _channels = channels;

            lock (_writer)
            {
                if (_writer == null) return;

                for (int i = 0; i < data.Length; i++)
                {
                    float clamped = Mathf.Clamp(data[i], -1f, 1f);
                    _writer.Write((short)(clamped * 32767f));
                }

                _samplesWritten += data.Length;
            }
        }

        private void OnDestroy() => Finish();
    }
}
