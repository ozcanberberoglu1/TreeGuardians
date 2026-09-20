using TreeGuardians.Core;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Audio
{
    /// Platform-safe haptics. No-op in the Editor and when disabled in settings.
    public sealed class HapticService : MonoBehaviour
    {
        const float MinIntervalSeconds = 0.06f;

        SettingsSaveData settings;
        float lastTime = -1f;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        public void Initialize(SettingsSaveData saveSettings)
        {
            settings = saveSettings;
        }

        public void Light() => Vibrate(0);
        public void Medium() => Vibrate(1);
        public void Heavy() => Vibrate(2);

        void Vibrate(int strength)
        {
            if (settings == null || !settings.vibration) return;
            if (settings.reduceHaptics && strength < 2) return;
            if (Time.unscaledTime - lastTime < MinIntervalSeconds) return;
            lastTime = Time.unscaledTime;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (strength >= 1) Handheld.Vibrate();
#endif
        }
    }
}
