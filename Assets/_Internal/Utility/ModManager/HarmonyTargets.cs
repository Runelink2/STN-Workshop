namespace STN.ModSDK {
  using System;
  using System.Reflection;
  using HarmonyLib;
  using System.Collections.Generic;

  public static class HarmonyTargets {
    // Lean reflection helpers for modders.
    // Generic, cached, and tolerant. No project-specific shortcuts.

    static readonly Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
    static readonly Dictionary<string, MethodBase> s_MethodCache = new Dictionary<string, MethodBase>(StringComparer.Ordinal);
    static readonly Dictionary<string, FieldInfo> s_FieldCache = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
    static readonly Dictionary<string, PropertyInfo> s_PropertyCache = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

    // colonSig like "TypeName:Method"; args optional (defaults to no-args)
    public static MethodBase Method(string colonSig, Type[] args = null) {
      args ??= Type.EmptyTypes;

      // 1) colon form
      var m = AccessTools.Method(colonSig, args);
      if (m != null) return m;

      // 2) split and resolve type by name; tolerate Assembly-CSharp fallback
      var idx = colonSig?.IndexOf(':') ?? -1;
      if (idx <= 0) return null;
      var typeName = colonSig.Substring(0, idx);
      var method = colonSig.Substring(idx + 1);

      var t = AccessTools.TypeByName(typeName)
              ?? Type.GetType($"{typeName}, Assembly-CSharp", throwOnError: false);
      if (t == null) return null;

      // 3) exact signature
      m = AccessTools.Method(t, method, args);
      if (m != null) return m;

      // 4) last chance: first overload by name when args==no-args
      if (args.Length == 0)
        m = AccessTools.FirstMethod(t, mi => mi.Name == method && mi.GetParameters().Length == 0);

      return m;
    }

    // Cached version of Method(colonSig)
    public static MethodBase MethodCached(string colonSig, Type[] args = null) {
      args ??= Type.EmptyTypes;
      var key = colonSig + "|" + string.Join(",", Array.ConvertAll(args, t => t?.FullName ?? "null"));
      if (s_MethodCache.TryGetValue(key, out var cached)) return cached;
      var m = Method(colonSig, args);
      if (m != null) s_MethodCache[key] = m;
      return m;
    }

    // Resolve Type by short name or AQN, tolerant of Assembly-CSharp
    public static Type ResolveType(string nameOrAQN) {
      if (string.IsNullOrEmpty(nameOrAQN)) return null;
      if (s_TypeCache.TryGetValue(nameOrAQN, out var t)) return t;
      t = AccessTools.TypeByName(nameOrAQN)
          ?? System.Type.GetType(nameOrAQN, throwOnError: false)
          ?? System.Type.GetType(nameOrAQN + ", Assembly-CSharp", throwOnError: false);
      if (t != null) s_TypeCache[nameOrAQN] = t;
      return t;
    }

    // Resolve MethodInfo on a given Type (public/non-public instance by default)
    public static MethodInfo Method(Type type, string name, Type[] args = null) {
      if (type == null || string.IsNullOrEmpty(name)) return null;
      args ??= Type.EmptyTypes;
      var key = type.FullName + ":" + name + "|" + string.Join(",", Array.ConvertAll(args, t => t?.FullName ?? "null"));
      if (s_MethodCache.TryGetValue(key, out var cached)) return cached as MethodInfo;
      var mi = AccessTools.Method(type, name, args);
      if (mi != null) s_MethodCache[key] = mi;
      return mi as MethodInfo;
    }

    // Resolve FieldInfo on a given Type
    public static FieldInfo Field(Type type, string name) {
      if (type == null || string.IsNullOrEmpty(name)) return null;
      var key = type.FullName + "." + name + "#field";
      if (s_FieldCache.TryGetValue(key, out var cached)) return cached;
      var fi = AccessTools.Field(type, name);
      if (fi != null) s_FieldCache[key] = fi;
      return fi;
    }

    // Resolve PropertyInfo on a given Type
    public static PropertyInfo Property(Type type, string name) {
      if (type == null || string.IsNullOrEmpty(name)) return null;
      var key = type.FullName + "." + name + "#prop";
      if (s_PropertyCache.TryGetValue(key, out var cached)) return cached;
      var pi = AccessTools.Property(type, name);
      if (pi != null) s_PropertyCache[key] = pi;
      return pi;
    }
  }
}