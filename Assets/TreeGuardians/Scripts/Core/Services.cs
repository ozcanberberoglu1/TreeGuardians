using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Core
{
    /// Explicit service registry populated by the Boot scene. No string lookups, no scene searches.
    public static class Services
    {
        static readonly Dictionary<Type, object> map = new Dictionary<Type, object>(32);

        public static bool IsBootstrapped { get; internal set; }

        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) return;
            // First live registration wins: a duplicate Boot_Root (e.g. re-entering the Boot scene) must not hijack the registry.
            if (map.TryGetValue(typeof(T), out var existing) && !ReferenceEquals(existing, instance))
            {
                if (existing is UnityEngine.Object uo ? uo != null : existing != null) return;
            }
            map[typeof(T)] = instance;
        }

        public static void Unregister<T>(T instance) where T : class
        {
            if (map.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, instance))
                map.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            return map.TryGetValue(typeof(T), out var o) ? (T)o : null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (map.TryGetValue(typeof(T), out var o)) { service = (T)o; return true; }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class => map.ContainsKey(typeof(T));

        public static void Clear()
        {
            map.Clear();
            IsBootstrapped = false;
        }

        public static T Require<T>() where T : class
        {
            var s = Get<T>();
            if (s == null) Debug.LogError($"[Services] Missing service {typeof(T).Name}. Start from the Boot scene or add the ServicesFallback prefab.");
            return s;
        }
    }
}
