using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace TreeGuardians.Core
{
    public static class TGLog
    {
        const string Prefix = "<color=#4CAF50>[TG]</color> ";

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message) => Debug.Log(Prefix + message);

        public static void Warn(string message) => Debug.LogWarning(Prefix + message);

        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
