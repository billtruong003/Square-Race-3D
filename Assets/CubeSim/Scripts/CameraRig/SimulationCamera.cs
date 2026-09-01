using UnityEngine;
using UnityEngine.Rendering.Universal;
using CubeSim.Core;
using CubeSim.Visuals;

namespace CubeSim.CameraRig
{
    /// <summary>
    /// Frames the whole arena from above. Everything is derived from arena bounds, so an automated
    /// pipeline gets correct framing for any arena size without hand-placing the camera.
    /// </summary>
    public static class SimulationCamera
    {
        public static void Frame(Camera camera, CameraDefinition definition, Rect arenaRect, float groundY)
            => Frame(camera, definition, arenaRect, groundY, null);

        public static void Frame(Camera camera, CameraDefinition definition, Rect arenaRect,
            float groundY, PostProcessingConfig post)
        {
            if (camera == null) return;

            ApplyPostProcessing(camera, post);

            camera.orthographic = definition.orthographic;
            camera.backgroundColor = definition.backgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;

            float aspect = camera.aspect > 0.01f ? camera.aspect : 16f / 9f;
            float halfWidth = arenaRect.width * 0.5f * definition.margin;
            float halfDepth = arenaRect.height * 0.5f * definition.margin;

            float height;
            if (definition.orthographic)
            {
                camera.orthographicSize = Mathf.Max(halfDepth, halfWidth / aspect);
                height = Mathf.Max(halfWidth, halfDepth) * 2f + 10f;
            }
            else
            {
                camera.fieldOfView = definition.fieldOfView;
                float tanHalfV = Mathf.Tan(definition.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float distanceForDepth = halfDepth / tanHalfV;
                float distanceForWidth = halfWidth / (tanHalfV * aspect);
                height = Mathf.Max(distanceForDepth, distanceForWidth);
            }

            if (definition.heightOverride > 0f) height = definition.heightOverride;

            float tilt = Mathf.Clamp(definition.tiltDegrees, 0f, 45f);
            var center = new Vector3(arenaRect.center.x, groundY, arenaRect.center.y);

            // Pull back along -Z as the camera tilts so the arena stays centred in frame.
            float pitch = 90f - tilt;
            float back = height * Mathf.Tan(tilt * Mathf.Deg2Rad);

            camera.transform.SetPositionAndRotation(
                center + new Vector3(0f, height, -back),
                Quaternion.Euler(pitch, 0f, 0f));

            camera.nearClipPlane = Mathf.Max(0.1f, height * 0.05f);
            camera.farClipPlane = height * 4f + 100f;
        }

        /// <summary>
        /// A URP camera ignores volume overrides unless it opts in, so the profile alone is not
        /// enough - this is what actually switches the finishing on.
        /// </summary>
        private static void ApplyPostProcessing(Camera camera, PostProcessingConfig post)
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            bool on = post == null || post.enabled;
            data.renderPostProcessing = on;
            // FXAA rather than SMAA. This arena is nothing but long straight high-contrast wall
            // edges over a flat floor, and every frame is re-encoded by YouTube: FXAA's slight
            // softening survives that better than SMAA's sharper edges, which shimmer under
            // compression, and it costs a fraction as much per frame when rendering long episodes.
            data.antialiasing = on && (post == null || post.antialiasing)
                ? AntialiasingMode.FastApproximateAntialiasing
                : AntialiasingMode.None;

            // Bloom needs HDR, otherwise emissives clip before they can glow.
            camera.allowHDR = true;
        }
    }
}
