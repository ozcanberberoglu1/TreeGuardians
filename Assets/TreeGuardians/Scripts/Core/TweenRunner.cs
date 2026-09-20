using UnityEngine;

namespace TreeGuardians.Core
{
    /// Lightweight coroutine host for TGTween. Lives on Boot_Root; helpers fall back to instant apply when absent.
    public sealed class TweenRunner : MonoBehaviour
    {
        public static TweenRunner Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Services.Register(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Services.Unregister(this);
        }
    }
}
