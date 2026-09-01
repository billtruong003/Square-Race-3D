using UnityEngine;
namespace CleanRender
{
    [DefaultExecutionOrder(-200)]
    public class ToonStyleApplier : MonoBehaviour
    {
        [SerializeField] private ToonStyleConfig config;
        [SerializeField] private bool applyEveryFrame = false;
        private void Awake()
        {
            if (config != null) config.Apply();
        }
        private void Update()
        {
            if (applyEveryFrame && config != null)
                config.Apply();
        }
        public void SwitchStyle(ToonStyleConfig newConfig, float transitionTime = 0f)
        {
            config = newConfig;
            config.Apply();
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (config != null) config.Apply();
        }
#endif
    }
}
