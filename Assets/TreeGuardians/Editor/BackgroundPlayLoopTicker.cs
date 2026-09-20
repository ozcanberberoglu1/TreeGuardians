using UnityEditor;
using UnityEditorInternal;

namespace TreeGuardians.Editor
{
    /// Keeps Play Mode ticking while the Editor window is not focused (automation / MCP driven tests).
    [InitializeOnLoad]
    public static class BackgroundPlayLoopTicker
    {
        const string PrefKey = "TreeGuardians.BackgroundPlayTicker";
        const string MenuPath = "Tree Guardians/Background Play Ticker";
        static double last;

        static BackgroundPlayLoopTicker()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static void Tick()
        {
            if (!Enabled) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (InternalEditorUtility.isApplicationActive) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - last < 1.0 / 60.0) return;
            last = now;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [MenuItem(MenuPath, priority = 40)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
