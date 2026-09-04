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
            // Shorts scenes declare a portrait round; everything else is the 16:9 long form.
            var director = UnityEngine.Object.FindFirstObjectByType<CubeSim.Core.EpisodeDirector>();
            bool portrait = director != null && director.Rounds.Count > 0 && director.Rounds[0].portrait;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = portrait ? 1080 : 1920,
                OutputHeight = portrait ? 1920 : 1080
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
                // Not delayCall: the editor only flushes delayCall on GUI events, so an unfocused
                // editor never muxed and the batch stalled. update keeps ticking in the background.
                RunAfterTicks(2, () => Mux(video, wavPath));
            }

            Debug.Log("[CubeSim] Recording stopped.");
        }

        /// <summary>
        /// Marries the tapped audio into the video with stream copy - a couple of seconds of
        /// ffmpeg. On success the silent video is replaced in place and the WAV removed; on any
        /// failure both files stay side by side so nothing is ever lost.
        /// </summary>
        // ------------------------------------------------------------------ recorded ledger
        // Recordings/recorded_shorts.txt: one line per finished scene ("S_RC13 2026-09-03 file.mp4").
        // The batch skips anything listed, so a map is never shot twice even after its video left
        // the folder. Only S_ (shorts) scenes are tracked; long formats reshoot freely.

        private static string LedgerPath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings", "recorded_shorts.txt");

        public static bool LedgerContains(string sceneName)
        {
            if (!File.Exists(LedgerPath)) return false;
            foreach (string line in File.ReadAllLines(LedgerPath))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                int space = trimmed.IndexOf(' ');
                string first = space < 0 ? trimmed : trimmed.Substring(0, space);
                if (first == sceneName) return true;
            }
            return false;
        }

        private static void LedgerAdd(string videoPath)
        {
            string file = Path.GetFileNameWithoutExtension(videoPath);
            if (!file.StartsWith("S_")) return;
            int cut = file.IndexOf("_20");
            string scene = cut > 0 ? file.Substring(0, cut) : file;
            if (LedgerContains(scene)) return;
            File.AppendAllText(LedgerPath, $"{scene} {System.DateTime.Now:yyyy-MM-dd} {file}.mp4" + System.Environment.NewLine);
            Debug.Log($"[CubeSim] Ledger: {scene} marked as recorded.");
        }

        /// <summary>
        /// Runs <paramref name="action"/> after the editor has ticked <paramref name="ticks"/> more
        /// update frames. Unlike delayCall this fires while the editor window is unfocused, which is
        /// exactly when a long unattended batch is running.
        /// </summary>
        public static void RunAfterTicks(int ticks, System.Action action)
        {
            int n = 0;
            EditorApplication.CallbackFunction cb = null;
            cb = () =>
            {
                if (++n < ticks) return;
                EditorApplication.update -= cb;
                action();
            };
            EditorApplication.update += cb;
        }

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
                    LedgerAdd(videoPath);
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
