using System.Diagnostics;
using System.IO;
using UnityEngine;
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
                            $"-vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{OutputPath}\"",
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

            Camera cam = Camera.main;
            if (cam == null) return;
            if (cam != _camera)
            {
                if (_camera != null) { _camera.targetTexture = null; _camera.enabled = true; }
                _camera = cam;
                _camera.enabled = false; // rendered manually below, exactly once per frame
            }

            AttachCanvases(cam);
            Canvas.ForceUpdateCanvases();
            _camera.targetTexture = _target;
            _camera.Render();

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
            }
            catch (System.Exception e)
            {
                Failed = true;
                Debug.LogError("[CubeSim] Headless capture pipe broke: " + e.Message);
                _pipe = null;
            }
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
                canvas.planeDistance = cam.nearClipPlane + 0.05f + 0.001f * (100 - Mathf.Clamp(order, 0, 99));
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
