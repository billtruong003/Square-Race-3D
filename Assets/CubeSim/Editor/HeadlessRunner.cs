using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Command-line entry points for running CubeSim without an open editor window:
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod CubeSim.EditorTools.HeadlessRunner.BuildShorts
    ///             -cubesimMaps RC16,CR01 [-cubesimScout] -logFile build.log
    ///   ... -executeMethod CubeSim.EditorTools.HeadlessRunner.Lab -cubesimMaps RC13,SD01 -cubesimSeeds 12
    ///   ... -executeMethod CubeSim.EditorTools.HeadlessRunner.Record -cubesimScenes S_RC16,S_SD01
    ///
    /// No -quit: each method exits the editor itself when its work (or the recording queue) is done.
    /// Recording runs play mode in batch mode, renders the camera off-screen through
    /// HeadlessFrameGrabber and mixes the audio offline, so the output matches the editor recorder.
    /// </summary>
    [InitializeOnLoad]
    public static class HeadlessRunner
    {
        private const string QueueFile = "Temp/cubesim_headless_queue.txt";
        private const string ActiveKey = "CubeSim.Headless.Active";
        private const int MaxFrames = 60 * 360;

        private static HeadlessFrameGrabber _grabber;
        private static OfflineAudioTrack _audio;
        private static string _videoPath;
        private static int _finishedAtFrame = -1;

        static HeadlessRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Watch;
        }

        // ---------------------------------------------------------------- arguments

        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static bool Flag(string name) => System.Environment.GetCommandLineArgs().Contains(name);

        private static HashSet<string> Maps()
        {
            string raw = Arg("-cubesimMaps");
            if (string.IsNullOrEmpty(raw)) return null;
            return new HashSet<string>(raw.Split(',').Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0));
        }

        // ---------------------------------------------------------------- build

        public static void BuildShorts()
        {
            HashSet<string> maps = Maps();
            try
            {
                ShortsPlanBuilder.Build(maps == null ? (System.Func<string, bool>)null : m => maps.Contains(m), Flag("-cubesimScout"));
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CubeSim] Headless build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- lab

        /// <summary>Runs the seed scout on each map and writes Docs/ModeLab/headless_lab.txt.</summary>
        public static void Lab()
        {
            HashSet<string> maps = Maps();
            int seeds = int.TryParse(Arg("-cubesimSeeds"), out int n) ? n : 12;
            var report = new StringBuilder($"Headless lab {System.DateTime.Now:yyyy-MM-dd HH:mm}  seeds/map={seeds}\n");

            try
            {
                string[] templates = Directory.GetFiles(ShortsPlanBuilder.TemplateFolder, "V_*.txt")
                    .Select(p => Path.GetFileNameWithoutExtension(p)).OrderBy(p => p).ToArray();

                foreach (string id in templates)
                {
                    string map = id.Substring(2);
                    if (maps != null && !maps.Contains(map)) continue;

                    EditorSceneManager.OpenScene(CubeSimSceneBuilder.EpisodeScenePath, OpenSceneMode.Single);
                    ShortsPlanBuilder.Tuning tune = ShortsPlanBuilder.TuningFor(map);
                    List<SeedScout.Result> all = SeedScout.Evaluate(id, tune, 1000, seeds);
                    if (all == null || all.Count == 0) { report.AppendLine($"{map,-6} FAILED"); continue; }

                    SeedScout.Result best = all.OrderByDescending(r => r.Score).First();
                    int timeouts = all.Count(r => r.TimedOut);
                    float median = all.Select(r => r.Seconds).OrderBy(x => x).ElementAt(all.Count / 2);
                    float lead = (float)all.Average(r => r.LeadChanges);
                    float lull = all.Select(r => r.LongestLull).OrderBy(x => x).ElementAt(all.Count / 2);
                    report.AppendLine($"{map,-6} median {median,5:F0}s  timeouts {timeouts,2}/{all.Count}  lead avg {lead,4:F1}  lull med {lull,4:F0}s  best seed {best.Seed} ({best.Summary})");
                    Debug.Log("[CubeSim] lab " + map + " done");
                }

                Directory.CreateDirectory("Docs/ModeLab");
                File.WriteAllText("Docs/ModeLab/headless_lab.txt", report.ToString());
                Debug.Log(report.ToString());
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CubeSim] Headless lab failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- record

        public static void Record()
        {
            string raw = Arg("-cubesimScenes");
            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogError("[CubeSim] -cubesimScenes S_RC16,S_SD01 is required.");
                EditorApplication.Exit(1);
                return;
            }

            // Silent machine: the video's audio is mixed offline from clip data, never from the speakers.
            EditorUtility.audioMasterMute = true;
            AudioListener.volume = 0f;
            Directory.CreateDirectory("Temp");
            File.WriteAllLines(QueueFile, raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
            Next();
        }

        private static void Next()
        {
            List<string> queue = File.Exists(QueueFile) ? File.ReadAllLines(QueueFile).Where(l => l.Trim().Length > 0).ToList() : new List<string>();
            if (queue.Count == 0)
            {
                Debug.Log("[CubeSim] Headless recording queue done.");
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.Exit(0);
                return;
            }

            string scene = queue[0];
            File.WriteAllLines(QueueFile, queue.Skip(1));

            string path = FindScene(scene);
            if (path == null)
            {
                Debug.LogError($"[CubeSim] Scene {scene} not found; skipped.");
                Next();
                return;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            Debug.Log($"[CubeSim] Headless recording {scene}");
            EditorApplication.isPlaying = true;
        }

        private static string FindScene(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:Scene {name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return path;
            }

            return null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;

            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    StartCapture();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    StopCapture();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    EditorApplication.delayCall += Next;
                    // delayCall can starve in batch mode; update always ticks.
                    EpisodeRecorderTicks(2, () => { EditorApplication.delayCall -= Next; Next(); });
                    break;
            }
        }

        private static void StartCapture()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings", "review");
            Directory.CreateDirectory(folder);
            string stem = Path.Combine(folder, $"{scene}_{System.DateTime.Now:yyyyMMdd_HHmmss}");

            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            bool portrait = director != null && director.Rounds.Count > 0 && director.Rounds[0].portrait;

            var go = new GameObject("HeadlessCapture");
            _grabber = go.AddComponent<HeadlessFrameGrabber>();
            _grabber.Width = portrait ? 1080 : 1920;
            _grabber.Height = portrait ? 1920 : 1080;
            _grabber.OutputPath = stem + "_silent.mp4";
            _videoPath = stem + ".mp4";
            Time.timeScale = 1f;
            EditorUtility.audioMasterMute = true;
            AudioListener.volume = 0f;
            _audio = OfflineAudioTrack.Begin(stem + ".wav", 60);
            _finishedAtFrame = -1;
        }

        private static void Watch()
        {
            if (_grabber == null || !EditorApplication.isPlaying) return;

            bool done = _grabber.Failed || _grabber.FramesWritten > MaxFrames;
            var director = Object.FindFirstObjectByType<EpisodeDirector>();
            if (director != null && director.Finished)
            {
                if (_finishedAtFrame < 0) _finishedAtFrame = _grabber.FramesWritten;
                // One second of tail after the director wraps up; the edit trims the rest.
                done |= _grabber.FramesWritten - _finishedAtFrame > 60;
            }

            if (done) EditorApplication.isPlaying = false;
        }

        private static void StopCapture()
        {
            if (_grabber == null) return;

            int frames = _grabber.FramesWritten;
            string silent = _grabber.OutputPath;
            _grabber.Finish();
            Object.DestroyImmediate(_grabber.gameObject);
            _grabber = null;

            string wav = null;
            if (_audio != null)
            {
                _audio.Finish();
                wav = _audio.OutputPath;
                _audio = null;
            }

            if (wav != null && File.Exists(wav) && File.Exists(silent))
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -loglevel error -i \"{silent}\" -i \"{wav}\" -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -shortest \"{_videoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(info)) p.WaitForExit(120000);
                if (File.Exists(_videoPath) && new FileInfo(_videoPath).Length > new FileInfo(silent).Length / 2)
                {
                    File.Delete(silent);
                    File.Delete(wav);
                }
            }

            Debug.Log($"[CubeSim] Headless capture closed: {frames} frames -> {_videoPath}");
        }

        private static void EpisodeRecorderTicks(int ticks, System.Action action)
        {
            int count = 0;
            EditorApplication.CallbackFunction cb = null;
            cb = () =>
            {
                if (++count < ticks) return;
                EditorApplication.update -= cb;
                action();
            };
            EditorApplication.update += cb;
        }
    }
}
