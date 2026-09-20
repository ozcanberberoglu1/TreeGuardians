using UnityEngine;

namespace TreeGuardians.Core
{
    /// Keeps a fixed world width visible on any aspect ratio (phones, tablets, editor windows).
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Tooltip("Görünür olması gereken dünya genişliğinin yarısı.")] public float halfWidth = 11f;
        [Tooltip("Ortho size bu değerin altına inmez (geniş ekranlarda).")] public float minOrthoSize = 5.4f;

        Camera cam;
        float lastAspect = -1f;

        public float FittedSize => cam != null ? Mathf.Max(minOrthoSize, halfWidth / Mathf.Max(0.1f, cam.aspect)) : minOrthoSize;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            Apply();
        }

        void Update()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect)) Apply();
        }

        public void Apply()
        {
            if (cam == null) return;
            lastAspect = cam.aspect;
            cam.orthographicSize = FittedSize;
        }
    }
}
