using TreeGuardians.Core;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Audio
{
    /// Platform-safe haptics. No-op in the Editor and when disabled in settings.
    /// Android: short one-shot pulses of different length/strength (VibrationEffect on API 26+).
    /// iOS: Handheld.Vibrate is a long buzz, so only Heavy uses it. The rate limit only counts pulses that actually fired.
    public sealed class HapticService : MonoBehaviour
    {
        [SerializeField] float minIntervalSeconds = 0.06f;
#pragma warning disable 0414 // Android-only tunables: unused on other platforms
        [Header("Android pulse (ms / amplitude 1-255)")]
        [SerializeField] int lightMs = 12;
        [SerializeField] int mediumMs = 25;
        [SerializeField] int heavyMs = 50;
        [SerializeField] [Range(1, 255)] int lightAmplitude = 60;
        [SerializeField] [Range(1, 255)] int mediumAmplitude = 140;
        [SerializeField] [Range(1, 255)] int heavyAmplitude = 255;
#pragma warning restore 0414

        SettingsSaveData settings;
        float lastTime = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject vibrator;
        AndroidJavaClass vibrationEffect;
        int sdkInt;
        bool androidReady;
#endif

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
#if UNITY_ANDROID && !UNITY_EDITOR
            vibrator?.Dispose();
            vibrationEffect?.Dispose();
#endif
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
            if (lastTime >= 0f && Time.unscaledTime - lastTime < minIntervalSeconds) return;
            if (Fire(strength)) lastTime = Time.unscaledTime;
        }

        bool Fire(int strength)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int ms = strength >= 2 ? heavyMs : strength == 1 ? mediumMs : lightMs;
            int amp = strength >= 2 ? heavyAmplitude : strength == 1 ? mediumAmplitude : lightAmplitude;
            return AndroidPulse(ms, amp);
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength < 2) return false;
            Handheld.Vibrate();
            return true;
#else
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        bool AndroidPulse(long ms, int amplitude)
        {
            try
            {
                if (!androidReady)
                {
                    androidReady = true;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdkInt = version.GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (sdkInt >= 26) vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                }
                if (vibrator == null) return StockBuzz(ms);
                if (vibrationEffect != null)
                {
                    using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", ms, Mathf.Clamp(amplitude, 1, 255)))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", ms);
                return true;
            }
            catch (System.Exception)
            {
                vibrator = null;
                return StockBuzz(ms);
            }
        }

        /// Java bridge unavailable: the stock (long) buzz, for strong pulses only.
        /// The Handheld.Vibrate reference also makes Unity add the VIBRATE permission to the Android manifest.
        bool StockBuzz(long ms)
        {
            if (ms < heavyMs) return false;
            Handheld.Vibrate();
            return true;
        }
#endif
    }
}
