#if CUBESIM_RECORDER
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeSim.EditorTools
{
    /// <summary>
    /// Records every scene in the Batch folder back to back, unattended: opens a scene, hands it
    /// to <see cref="EpisodeRecorder"/>, and when the episode ends (play exits and the audio has
    /// been muxed) opens the next one - fifty videos on one click and an evening of wall time.
    ///
    /// The queue lives in SessionState so it survives the play-mode domain reloads. Stop it any
    /// time with the Stop menu; the video being recorded still finishes cleanly.
    /// </summary>
    [InitializeOnLoad]
    public static class BatchRecorder
    {
        private const string QueueKey = "CubeSim.BatchQueue";
        private const string IndexKey = "CubeSim.BatchIndex";

        static BatchRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("CubeSim/Record Batch (all Batch scenes)", priority = 31)]
        public static void StartBatch() => StartFolder(FormatPlanBuilder.SceneFolder);

        [MenuItem("CubeSim/Record Wave 1", priority = 30)]
        public static void StartWave1() => StartFolder(Wave1PlanBuilder.SceneFolder);

        [MenuItem("CubeSim/Record Shorts", priority = 30)]
        public static void StartShorts() => StartFolder(ShortsPlanBuilder.SceneFolder);

        private static void StartFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Debug.LogError($"[CubeSim] No scene folder at {folder}; build it first.");
                return;
            }

            string[] all = Directory.GetFiles(folder, "*.unity")
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path)
                .ToArray();

            if (all.Length == 0)
            {
                Debug.LogError($"[CubeSim] {folder} has no scenes.");
                return;
            }

            // Resume semantics: a scene that already produced a finished video is skipped, so
            // restarting after a crash picks up where the queue died instead of re-shooting
            // hours of footage. A half-written pair counts as unfinished and is shot again.
            string[] scenes = all.Where(scene => !AlreadyRecorded(scene)).ToArray();
            int skipped = all.Length - scenes.Length;

            if (scenes.Length == 0)
            {
                Debug.Log($"[CubeSim] Every scene in {folder} is already recorded; nothing to do.");
                return;
            }

            SessionState.SetString(QueueKey, string.Join(";", scenes));
            SessionState.SetInt(IndexKey, 0);
            Debug.Log($"[CubeSim] Batch recording started: {scenes.Length} scenes queued from {folder}" +
                      (skipped > 0 ? $" ({skipped} already recorded, skipped)." : "."));
            RecordNext();
        }

        /// <summary>
        /// True when Recordings/ holds a finished video for this scene: an MP4 named after it
        /// with no leftover WAV beside it (a successful mux consumes the WAV).
        /// </summary>
        private static bool AlreadyRecorded(string scenePath)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings");
            if (!Directory.Exists(folder)) return false;

            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            // The ledger outlives the files: a video that was uploaded, moved or renamed still
            // counts as shot. One line per scene, first token is the scene name.
            if (EpisodeRecorder.LedgerContains(sceneName)) return true;
            foreach (string video in Directory.GetFiles(folder, sceneName + "_*.mp4"))
            {
                if (video.EndsWith("_muxed.mp4")) continue;
                if (!File.Exists(Path.ChangeExtension(video, ".wav"))) return true;
            }

            return false;
        }

        [MenuItem("CubeSim/Record Batch Stop", priority = 32)]
        public static void StopBatch()
        {
            SessionState.EraseString(QueueKey);
            SessionState.EraseInt(IndexKey);
            Debug.Log("[CubeSim] Batch queue cleared. A recording in progress still finishes.");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (string.IsNullOrEmpty(SessionState.GetString(QueueKey, ""))) return;

            // Two delay hops: the first lets EpisodeRecorder's own delayCall run the audio mux,
            // the second opens the next scene on a clean editor frame.
            EpisodeRecorder.RunAfterTicks(8, RecordNext);
        }

        private static void RecordNext()
        {
            string queue = SessionState.GetString(QueueKey, "");
            if (string.IsNullOrEmpty(queue)) return;

            string[] scenes = queue.Split(';');
            int index = SessionState.GetInt(IndexKey, 0);

            if (index >= scenes.Length)
            {
                StopBatch();
                Debug.Log($"[CubeSim] Batch recording DONE: {scenes.Length} videos in Recordings/.");
                return;
            }

            SessionState.SetInt(IndexKey, index + 1);
            Debug.Log($"[CubeSim] Batch {index + 1}/{scenes.Length}: {Path.GetFileName(scenes[index])}");

            EditorSceneManager.OpenScene(scenes[index], OpenSceneMode.Single);
            EpisodeRecorder.RecordEpisode();
        }
    }
}
#endif
