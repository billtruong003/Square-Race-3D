using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Records the game camera without a Game view, for batch-mode (headless) capture.
    ///
    /// Every frame the main camera renders into an off-screen texture; screen-space overlay
    /// canvases are switched to camera space so the HUD lands in the same image. Frames are read
    /// back and piped raw into ffmpeg at a locked 60 fps (Time.captureFramerate), so a frame of
    /// video is exactly a frame of simulation regardless of how slowly the machine renders.
    /// </summary>
    public sealed class HeadlessFrameGrabber : MonoBehaviour
    {
        public int Width = 1080;
        public int Height = 1920;
        public int Fps = 60;
        public string OutputPath;

        public int FramesWritten { get; private set; }
        public bool Failed { get; private set; }

        private RenderTexture _target;
        private Texture2D _readback;
        private Process _ffmpeg;
        private Stream _pipe;
        private Camera _camera;
        private byte[] _buffer;
        private bool _warnedNoCamera;

        private void Start()
        {
            Time.captureFramerate = Fps;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "HeadlessCapture" };
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var info = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {Width}x{Height} -r {Fps} -i - " +
                            $"-vf vflip -c:v libx264 -preset veryfast -crf 18 -threads 2 -pix_fmt yuv420p \"{OutputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            try
            {
                _ffmpeg = Process.Start(info);
                _ffmpeg.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[ffmpeg] " + e.Data); };
                _ffmpeg.BeginErrorReadLine();
                _pipe = _ffmpeg.StandardInput.BaseStream;
            }
            catch (System.Exception e)
            {
                Failed = true;
                Debug.LogError("[CubeSim] Headless capture could not start ffmpeg: " + e.Message);
                return;
            }

        }

        // Batch mode has no player loop "end of frame", so the camera is rendered by hand here,
        // after every Update has posed the scene for this frame.
        private void LateUpdate()
        {
            if (_pipe == null) return;

            // Camera.main can come back null in batch mode; take the tagged camera, else any camera.
            Camera cam = _camera != null ? _camera : FindCamera();
            if (cam == null)
            {
                if (!_warnedNoCamera) Debug.LogWarning("[CubeSim] Headless capture: no MainCamera yet.");
                _warnedNoCamera = true;
                return;
            }
            if (cam != _camera)
            {
                if (_camera != null) { _camera.targetTexture = null; _camera.enabled = true; }
                _camera = cam;
                _camera.enabled = false; // rendered manually below, exactly once per frame
                BuildUiCamera(cam);
            }

            AttachCanvases(_uiCamera != null ? _uiCamera : cam);
            Canvas.ForceUpdateCanvases();
            _camera.targetTexture = _target;
            _camera.Render();
            if (_uiCamera != null && _composite != null)
            {
                _uiCamera.Render();
                Graphics.Blit(_uiTarget, _target, _composite);
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = previous;

            var data = _readback.GetRawTextureData<byte>();
            if (_buffer == null || _buffer.Length != data.Length) _buffer = new byte[data.Length];
            data.CopyTo(_buffer);
            try
            {
                _pipe.Write(_buffer, 0, _buffer.Length);
                FramesWritten++;
                if (FramesWritten % 300 == 0)
                    Debug.Log($"[CubeSim] Headless capture: {FramesWritten} frames ({Time.realtimeSinceStartup:F0}s wall)");
            }
            catch (System.Exception e)
            {
                Failed = true;
                Debug.LogError("[CubeSim] Headless capture pipe broke: " + e.Message);
                _pipe = null;
            }
        }

        private Camera _uiCamera;
        private RenderTexture _uiTarget;
        private Material _composite;

        /// <summary>
        /// The HUD goes through its own overlay camera with post-processing off, stacked on the game
        /// camera: drawn through the game camera, bloom and tonemapping dulled every colour and the
        /// outline inked the text. It sits far below the court so it never sees the scene.
        /// </summary>
        private void BuildUiCamera(Camera baseCamera)
        {
            var go = new GameObject("HeadlessUiCamera");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, -5000f, 0f);
            _uiCamera = go.AddComponent<Camera>();
            _uiCamera.orthographic = true;
            _uiCamera.clearFlags = CameraClearFlags.Depth;
            _uiCamera.nearClipPlane = 0.01f;
            _uiCamera.farClipPlane = 10f;
            _uiCamera.enabled = false;

            // The HUD renders into its own transparent texture (no post-processing, no outline),
            // then is composited over the finished game frame. Cameras that skip clearing still
            // clear in URP, and a stacked overlay is not rendered by a manual Render of its base.
            _uiCamera.clearFlags = CameraClearFlags.SolidColor;
            _uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _uiCamera.allowHDR = false;
            _uiCamera.allowMSAA = false;
            var uiData = go.AddComponent<UniversalAdditionalCameraData>();
            uiData.renderType = CameraRenderType.Base;
            uiData.renderPostProcessing = false;
            uiData.antialiasing = AntialiasingMode.None;
            _uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "HeadlessHud" };
            _uiCamera.targetTexture = _uiTarget;
            Shader composite = Shader.Find("Hidden/CubeSim/UiComposite");
            if (composite != null) _composite = new Material(composite);
            else Debug.LogWarning("[CubeSim] UiComposite shader missing; HUD will not be captured.");
        }

        private static Camera FindCamera()
        {
            Camera any = null;
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.CompareTag("MainCamera")) return c;
                if (any == null) any = c;
            }

            return any;
        }

        /// <summary>Overlay canvases never reach a render texture; camera-space ones do.</summary>
        private static void AttachCanvases(Camera cam)
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                int order = canvas.sortingOrder;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f + 0.01f * (100 - Mathf.Clamp(order, 0, 99));
                canvas.sortingOrder = order;
            }
        }

        /// <summary>Closes the stream and waits for ffmpeg to finish the file.</summary>
        public void Finish()
        {
            try { _pipe?.Flush(); _pipe?.Close(); } catch { /* already closed */ }
            _pipe = null;
            if (_ffmpeg != null)
            {
                _ffmpeg.WaitForExit(120000);
                _ffmpeg.Dispose();
                _ffmpeg = null;
            }

            if (_camera != null) { _camera.targetTexture = null; _camera.enabled = true; }
            Time.captureFramerate = 0;
        }

        private void OnDestroy()
        {
            if (_pipe != null) Finish();
            if (_target != null) _target.Release();
        }
    }
}
