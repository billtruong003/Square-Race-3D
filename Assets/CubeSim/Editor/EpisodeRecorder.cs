#if CUBESIM_RECORDER
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// One-click episode capture: flags a recording, enters play mode, and starts a 1080p60 MP4
    /// capture of the Game view once play has actually begun - the Recorder API only accepts
    /// PrepareRecording inside play mode, and the flag has to survive the domain reload on the way
    /// in, which is why it lives in SessionState rather than a static.
    ///
    /// When an EpisodeDirector is present, play exits by itself a few seconds after the podium
    /// card, closing the file - a full multi-round upload renders unattended. Same scene + same
    /// seeds = the same video, cut for cut. Output lands in Recordings/ next to Assets.
    /// </summary>
    [InitializeOnLoad]
    public static class EpisodeRecorder
    {
        private const string PendingKey = "CubeSim.RecordPending";

        private static RecorderController _controller;
        private static AudioCaptureTap _audioTap;
        private static string _videoPath;
        private static double _finishedAt;

        static EpisodeRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += WatchForEpisodeEnd;
        }

        [MenuItem("CubeSim/Record Episode (Play + Capture)", priority = 30)]
        public static void RecordEpisode()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CubeSim] Already in play mode; stop it first.");
                return;
            }

            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode when SessionState.GetBool(PendingKey, false):
                    SessionState.SetBool(PendingKey, false);
                    StartRecording();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    StopRecording();
                    break;
            }
        }

        private static void StartRecording()
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings");
            Directory.CreateDirectory(folder);

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string output = Path.Combine(folder, $"{sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}");

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "CubeSim Episode";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080
            };
            // The Recorder's own audio path delivers silence on this setup (its AudioRenderer
            // capture never sees the mix), so it stays off; the AudioCaptureTap on the listener
            // records the real mix to a WAV and the two are muxed after the run.
            movie.CaptureAudio = false;
            movie.OutputFile = output;

            settings.AddRecorderSettings(movie);

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            _controller.StartRecording();
            _finishedAt = 0;

            _videoPath = output + ".mp4";
            _audioTap = AudioCaptureTap.Begin(output + ".wav");

            Debug.Log($"[CubeSim] Recording to {output}.mp4 (audio via listener tap)");
        }

        private static void StopRecording()
        {
            if (_controller == null) return;

            if (_controller.IsRecording()) _controller.StopRecording();
            _controller = null;

            if (_audioTap != null)
            {
                _audioTap.Finish();
                string wavPath = _audioTap.OutputPath;
                _audioTap = null;

                // The video file is finalised on the editor frame after play exits; mux then.
                string video = _videoPath;
                EditorApplication.delayCall += () => Mux(video, wavPath);
            }

            Debug.Log("[CubeSim] Recording stopped.");
        }

        /// <summary>
        /// Marries the tapped audio into the video with stream copy - a couple of seconds of
        /// ffmpeg. On success the silent video is replaced in place and the WAV removed; on any
        /// failure both files stay side by side so nothing is ever lost.
        /// </summary>
        private static void Mux(string videoPath, string wavPath)
        {
            if (!File.Exists(videoPath) || !File.Exists(wavPath))
            {
                Debug.LogWarning($"[CubeSim] Mux skipped; missing {(File.Exists(videoPath) ? wavPath : videoPath)}");
                return;
            }

            string merged = videoPath.Replace(".mp4", "_muxed.mp4");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{videoPath}\" -i \"{wavPath}\" -map 0:v -map 1:a " +
                            $"-c:v copy -c:a aac -b:a 192k -shortest \"{merged}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            try
            {
                using var process = System.Diagnostics.Process.Start(startInfo);
                process.StandardError.ReadToEnd();
                process.WaitForExit(120000);

                if (process.ExitCode == 0 && File.Exists(merged) && new FileInfo(merged).Length > 0)
                {
                    File.Delete(videoPath);
                    File.Move(merged, videoPath);
                    File.Delete(wavPath);
                    Debug.Log($"[CubeSim] Audio muxed into {videoPath}");
                }
                else
                {
                    Debug.LogWarning($"[CubeSim] Mux failed (exit {process.ExitCode}); " +
                                     $"audio left beside the video at {wavPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CubeSim] Mux failed ({e.Message}); audio left at {wavPath}");
            }
        }

        /// <summary>Ends play a few seconds after the director's podium, closing the recording.</summary>
        private static void WatchForEpisodeEnd()
        {
            if (_controller == null || !EditorApplication.isPlaying) return;

            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director == null || !director.Finished) return;

            if (_finishedAt == 0)
            {
                _finishedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - _finishedAt > 5.0)
            {
                Debug.Log("[CubeSim] Episode finished; stopping play and closing the file.");
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
