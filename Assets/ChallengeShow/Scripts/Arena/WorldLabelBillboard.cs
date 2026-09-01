using UnityEngine;
using UnityEngine.Rendering;

namespace ChallengeShow
{
    /// <summary>
    /// Keeps a world-space label turned toward whatever camera is about to draw it.
    ///
    /// Orienting once per frame in LateUpdate is not enough here: the label has to read correctly in
    /// the Game view and the Scene view at the same time, and picking one camera left the other
    /// showing the text mirrored. Hooking the render callback means each camera gets the label
    /// facing it for its own draw.
    /// </summary>
    [ExecuteAlways]
    public class WorldLabelBillboard : MonoBehaviour
    {
        [Tooltip("Rotate only around Y, so labels stay upright instead of tipping with the camera.")]
        [SerializeField] private bool yAxisOnly = true;

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += FaceCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= FaceCamera;

        private void FaceCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null) return;

            // Text faces its own +Z, so the label's forward points the same way the camera looks.
            Vector3 forward = transform.position - cam.transform.position;
            if (yAxisOnly) forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
