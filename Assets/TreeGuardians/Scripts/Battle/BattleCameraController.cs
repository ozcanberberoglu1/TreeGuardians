using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Mostly static camera. Hits add "trauma" (0..1); the shake is trauma² × smooth noise, so small hits nudge and big
    /// ones rattle, and it decays on unscaled time (hit-stop freezes the world, not the shake). Big hits also pulse the zoom.
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [Tooltip("Tam travmada en büyük kayma (dünya birimi).")]
        [SerializeField] float maxOffset = 0.32f;
        [Tooltip("Tam travmada en büyük dönüş (derece).")]
        [SerializeField] float maxRoll = 1.6f;
        [SerializeField] float traumaDecay = 1.9f;
        [SerializeField] float noiseFrequency = 22f;
        [SerializeField] float zoomPulse = 0.14f;
        [Tooltip("Her en-boy oranında görünür kalacak dünya genişliğinin yarısı.")]
        [SerializeField] float fitHalfWidth = 11.8f;
        [SerializeField] float minOrthoSize = 6.2f;

        Vector3 basePosition;
        Quaternion baseRotation;
        float baseSize;
        float trauma;
        float pulse;
        float lastAspect = -1f;
        float seed;

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            basePosition = transform.position;
            baseRotation = transform.rotation;
            seed = Random.value * 100f;
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

        /// ScreenShakeEvent.amplitude is trauma (0..1).
        void OnShake(ScreenShakeEvent e)
        {
            if (QualityApplier.ReduceMotion) return;
            trauma = Mathf.Clamp01(trauma + Mathf.Clamp01(e.amplitude));
            if (e.amplitude >= 0.5f) pulse = Mathf.Max(pulse, zoomPulse * e.amplitude);
        }

        void LateUpdate()
        {
            if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect)) Fit();
            float dt = Time.unscaledDeltaTime;
            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
                float k = trauma * trauma;
                float t = Time.unscaledTime * noiseFrequency;
                float nx = Mathf.PerlinNoise(seed, t) * 2f - 1f;
                float ny = Mathf.PerlinNoise(seed + 17f, t) * 2f - 1f;
                float nr = Mathf.PerlinNoise(seed + 43f, t) * 2f - 1f;
                transform.position = basePosition + new Vector3(nx, ny, 0f) * (maxOffset * k);
                transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, nr * maxRoll * k);
                if (trauma <= 0f) { transform.position = basePosition; transform.rotation = baseRotation; }
            }
            if (cam != null)
            {
                pulse = Mathf.MoveTowards(pulse, 0f, dt * 0.7f);
                cam.orthographicSize = baseSize - pulse;
            }
        }
    }
}
