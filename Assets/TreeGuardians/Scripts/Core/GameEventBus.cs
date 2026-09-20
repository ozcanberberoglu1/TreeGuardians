using System;
using System.Collections.Generic;

namespace TreeGuardians.Core
{
    /// Typed, allocation-free-on-publish event bus for struct events.
    public static class GameEventBus
    {
        static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>(64);

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            handlers.TryGetValue(typeof(T), out var existing);
            handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            if (!handlers.TryGetValue(typeof(T), out var existing)) return;
            var next = Delegate.Remove(existing, handler);
            if (next == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = next;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (handlers.TryGetValue(typeof(T), out var d))
                ((Action<T>)d)?.Invoke(evt);
        }

        public static void Clear() => handlers.Clear();
    }
}
