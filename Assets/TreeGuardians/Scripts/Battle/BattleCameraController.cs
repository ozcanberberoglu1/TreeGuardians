using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Mostly static camera with short, bounded shakes and a tiny zoom pulse on big hits.
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float maxAmplitude = 0.4f;
        [SerializeField] float zoomPulse = 0.12f;
        [Tooltip("Her en-boy oranında görünür kalacak dünya genişliğinin yarısı.")]
        [SerializeField] float fitHalfWidth = 11.8f;
        [SerializeField] float minOrthoSize = 6.2f;

        Vector3 basePosition;
        float baseSize;
        float amplitude;
        float remaining;
        float duration;
        float pulse;
        float lastAspect = -1f;

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            basePosition = transform.position;
            Fit();
        }

        void Fit()
        {
            if (cam == null) return;
            lastAspect = cam.aspect;
            baseSize = Mathf.Max(minOrthoSize, fitHalfWidth / Mathf.Max(0.1f, cam.aspect));
            cam.orthographicSize = baseSize;
        }

        void OnEnable() => GameEventBus.Subscribe<ScreenShakeEvent>(OnShake);
        void OnDisable() => GameEventBus.Unsubscribe<ScreenShakeEvent>(OnShake);

        void OnShake(ScreenShakeEvent e)
        {
            if (QualityApplier.ReduceMotion) return;
            float a = Mathf.Min(maxAmplitude, e.amplitude);
            if (a < amplitude * (remaining / Mathf.Max(0.01f, duration))) return;
            amplitude = a;
            duration = Mathf.Clamp(e.duration, 0.05f, 0.25f);
            remaining = duration;
            if (a > 0.2f) pulse = zoomPulse;
        }

        void LateUpdate()
        {
            if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect)) Fit();
            if (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                float k = Mathf.Clamp01(remaining / duration);
                var offset = new Vector3(Mathf.Sin(Time.time * 47f), Mathf.Cos(Time.time * 39f), 0f) * amplitude * k;
                transform.position = basePosition + offset;
                if (remaining <= 0f) transform.position = basePosition;
            }
            if (cam != null)
            {
                pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 0.6f);
                cam.orthographicSize = baseSize - pulse;
            }
        }
    }
}
