using System;

namespace TreeGuardians.Core
{
    /// Runs a callback once services are bootstrapped; immediately if they already are.
    public static class BootAwaiter
    {
        public static void WhenReady(Action callback)
        {
            if (callback == null) return;
            if (Services.IsBootstrapped) { callback(); return; }
            Action<ServicesReadyEvent> handler = null;
            handler = _ =>
            {
                GameEventBus.Unsubscribe(handler);
                callback();
            };
            GameEventBus.Subscribe(handler);
        }
    }
}
