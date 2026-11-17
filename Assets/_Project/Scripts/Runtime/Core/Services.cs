using System;
using System.Collections.Generic;

namespace CarWash.Core
{
    /// <summary>Static service locator (TECH §2). Services register in Bootstrap and live for the whole session.</summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class => Map[typeof(T)] = service;
        public static void Unregister<T>() where T : class => Map.Remove(typeof(T));
        public static T Get<T>() where T : class => Map.TryGetValue(typeof(T), out var s) ? (T)s : null;
        public static bool Has<T>() where T : class => Map.ContainsKey(typeof(T));
        public static void Clear() => Map.Clear();
    }
}
