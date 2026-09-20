using TreeGuardians.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeGuardians.SceneFlow
{
    /// Editor/dev convenience: when a non-Boot scene is played directly, instantiate the pre-authored Boot_Root prefab.
    public static class ServicesFallback
    {
        public const string ResourcePath = "TG_BootRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureServices()
        {
            if (Services.IsBootstrapped || Services.Has<GameBootstrapper>()) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.name.StartsWith("00_")) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                TGLog.Warn($"ServicesFallback: Resources/{ResourcePath}.prefab not found; services unavailable in scene '{scene.name}'.");
                return;
            }
            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            TGLog.Info($"ServicesFallback: instantiated {ResourcePath} for scene '{scene.name}'.");
        }
    }
}
