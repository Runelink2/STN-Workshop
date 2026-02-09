// ============================================================================
// STN.ModSDK - DO NOT MODIFY THIS FILE
// ============================================================================
// This file is part of the Mod SDK and compiles into STN.ModSDK.dll.
// Your mod REFERENCES this DLL - it does not include the source code.
// 
// Any changes you make here will NOT affect your mod at runtime because
// the game uses its own copy of STN.ModSDK.dll.
//
// If you need custom helpers, create them in your mod's Code/ folder instead.
// ============================================================================

namespace STN.ModSDK {
  using System;
  using System.Reflection;
  using HarmonyLib;
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Helper class for loading and instantiating mod content from Addressables.
  /// Uses reflection to call Addressables API so mods don't need compile-time Addressables dependency.
  /// </summary>
  /// <remarks>
  /// This is part of the SDK. Do not modify - create your own helpers in your mod's Code/ folder.
  /// </remarks>
  public static class ModContentLoader {
    public const string SdkVersion = "1.0.2";
    
    // AsyncOperationStatus.Succeeded = 1 (Unity Addressables)
    private const int STATUS_SUCCEEDED = 1;
    
    private static Type s_AddressablesType;
    private static MethodInfo s_LoadAssetAsync;
    private static MethodInfo s_InstantiateAsync;
    private static MethodInfo s_Release;
    private static bool s_initialized;
    private static string s_initError;

    /// <summary>Returns true if ModContentLoader is ready to use.</summary>
    public static bool IsInitialized => s_initialized && s_AddressablesType != null;
    
    /// <summary>Returns initialization error message if any.</summary>
    public static string InitializationError => s_initError;

    static void EnsureInitialized() {
      if (s_initialized) return;
      s_initialized = true;
      s_initError = null;
      try {
        // Try direct type resolution first
        s_AddressablesType = Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables", throwOnError: false);
        
        // Fallback: scan loaded assemblies
        if (s_AddressablesType == null) {
          foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            if (asm.FullName.StartsWith("Unity.Addressables")) {
              s_AddressablesType = asm.GetType("UnityEngine.AddressableAssets.Addressables");
              if (s_AddressablesType != null) break;
            }
          }
        }
        
        if (s_AddressablesType == null) {
          s_initError = "Addressables assembly not loaded. Ensure Unity Addressables package is installed.";
          Debug.LogWarning($"[ModContentLoader] {s_initError}");
          return;
        }

        var methods = s_AddressablesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        // Find LoadAssetAsync<T>(object key) - exactly 1 parameter, generic
        foreach (var m in methods) {
          if (m.Name == "LoadAssetAsync" && m.IsGenericMethodDefinition) {
            var parms = m.GetParameters();
            if (parms.Length == 1 && parms[0].ParameterType == typeof(object)) {
              s_LoadAssetAsync = m;
              break;
            }
          }
        }
        
        // Find InstantiateAsync(object key, Vector3, Quaternion, Transform) - prefer 4-param version
        MethodInfo fallbackInstantiate = null;
        foreach (var m in methods) {
          if (m.Name == "InstantiateAsync" && !m.IsGenericMethodDefinition) {
            var parms = m.GetParameters();
            // Exact match: (object, Vector3, Quaternion, Transform)
            if (parms.Length == 4 && 
                parms[0].ParameterType == typeof(object) &&
                parms[1].ParameterType == typeof(Vector3) && 
                parms[2].ParameterType == typeof(Quaternion) &&
                parms[3].ParameterType == typeof(Transform)) {
              s_InstantiateAsync = m;
              break;
            }
            // Fallback: single-param version (object key)
            if (fallbackInstantiate == null && parms.Length == 1 && parms[0].ParameterType == typeof(object)) {
              fallbackInstantiate = m;
            }
          }
        }
        if (s_InstantiateAsync == null) s_InstantiateAsync = fallbackInstantiate;
        
        // Find Release<T>(T obj) for memory management
        foreach (var m in methods) {
          if (m.Name == "Release" && m.IsGenericMethodDefinition) {
            var parms = m.GetParameters();
            if (parms.Length == 1) {
              s_Release = m;
              break;
            }
          }
        }
        
        Debug.Log($"[ModContentLoader] v{SdkVersion} initialized. Load={s_LoadAssetAsync != null}, Instantiate={s_InstantiateAsync != null}, Release={s_Release != null}");
      } catch (Exception ex) {
        s_initError = ex.Message;
        Debug.LogWarning($"[ModContentLoader] Init failed: {ex.Message}");
      }
    }

    /// <summary>Load a GameObject asset by Addressable address.</summary>
    public static void LoadAssetAsync(string address, Action<GameObject> onLoaded) {
      LoadAssetAsync<GameObject>(address, onLoaded);
    }

    /// <summary>Load an asset of type T by Addressable address.</summary>
    public static void LoadAssetAsync<T>(string address, Action<T> onLoaded) where T : UnityEngine.Object {
      EnsureInitialized();
      if (s_LoadAssetAsync == null) {
        Debug.LogWarning($"[ModContentLoader] LoadAssetAsync not available. Cannot load '{address}'.");
        onLoaded?.Invoke(null);
        return;
      }

      try {
        var genericMethod = s_LoadAssetAsync.MakeGenericMethod(typeof(T));
        var handle = genericMethod.Invoke(null, new object[] { address });
        if (handle == null) {
          Debug.LogWarning($"[ModContentLoader] Null handle for '{address}'.");
          onLoaded?.Invoke(null);
          return;
        }

        var completedEvent = handle.GetType().GetEvent("Completed");
        if (completedEvent != null) {
          var wrapper = new CompletedHandler<T>(onLoaded, address);
          // Use MakeGenericMethod so the delegate parameter type matches the event's value-type argument exactly
          var openMethod = typeof(CompletedHandler<T>).GetMethod("OnCompleted", BindingFlags.Public | BindingFlags.Instance);
          var closedMethod = openMethod.MakeGenericMethod(handle.GetType());
          var del = Delegate.CreateDelegate(completedEvent.EventHandlerType, wrapper, closedMethod);
          completedEvent.GetAddMethod().Invoke(handle, new object[] { del });
        } else {
          Debug.LogWarning($"[ModContentLoader] No Completed event for '{address}'.");
          onLoaded?.Invoke(null);
        }
      } catch (Exception ex) {
        Debug.LogWarning($"[ModContentLoader] Load failed '{address}': {ex.Message}");
        onLoaded?.Invoke(null);
      }
    }

    /// <summary>Instantiate a prefab by Addressable address.</summary>
    public static void InstantiateAsync(string address, Vector3 position, Quaternion rotation, Action<GameObject> onInstantiated) {
      EnsureInitialized();
      
      // Fallback to load + manual instantiate if no InstantiateAsync found
      if (s_InstantiateAsync == null) {
        LoadAssetAsync<GameObject>(address, prefab => {
          if (prefab != null) {
            onInstantiated?.Invoke(UnityEngine.Object.Instantiate(prefab, position, rotation));
          } else {
            onInstantiated?.Invoke(null);
          }
        });
        return;
      }

      try {
        var parms = s_InstantiateAsync.GetParameters();
        object handle;
        bool needsTransform = false;
        
        if (parms.Length == 4 && parms[1].ParameterType == typeof(Vector3)) {
          handle = s_InstantiateAsync.Invoke(null, new object[] { address, position, rotation, null });
        } else {
          handle = s_InstantiateAsync.Invoke(null, new object[] { address });
          needsTransform = true;
        }

        if (handle == null) {
          Debug.LogWarning($"[ModContentLoader] Null handle for instantiate '{address}'.");
          onInstantiated?.Invoke(null);
          return;
        }

        var completedEvent = handle.GetType().GetEvent("Completed");
        if (completedEvent != null) {
          var wrapper = new InstantiateHandler(onInstantiated, address, needsTransform, position, rotation);
          var openMethod = typeof(InstantiateHandler).GetMethod("OnCompleted", BindingFlags.Public | BindingFlags.Instance);
          var closedMethod = openMethod.MakeGenericMethod(handle.GetType());
          var del = Delegate.CreateDelegate(completedEvent.EventHandlerType, wrapper, closedMethod);
          completedEvent.GetAddMethod().Invoke(handle, new object[] { del });
        } else {
          Debug.LogWarning($"[ModContentLoader] No Completed event for instantiate '{address}'.");
          onInstantiated?.Invoke(null);
        }
      } catch (Exception ex) {
        Debug.LogWarning($"[ModContentLoader] Instantiate failed '{address}': {ex.Message}");
        onInstantiated?.Invoke(null);
      }
    }

    /// <summary>Instantiate a prefab at the origin.</summary>
    public static void InstantiateAsync(string address, Action<GameObject> onInstantiated) {
      InstantiateAsync(address, Vector3.zero, Quaternion.identity, onInstantiated);
    }

    /// <summary>Release a loaded asset back to Addressables to free memory.</summary>
    public static void Release<T>(T asset) where T : UnityEngine.Object {
      if (asset == null) return;
      EnsureInitialized();
      if (s_Release == null) {
        Debug.LogWarning("[ModContentLoader] Release not available. Asset may leak memory.");
        return;
      }
      try {
        var genericMethod = s_Release.MakeGenericMethod(typeof(T));
        genericMethod.Invoke(null, new object[] { asset });
      } catch (Exception ex) {
        Debug.LogWarning($"[ModContentLoader] Release failed: {ex.Message}");
      }
    }

    /// <summary>Release a GameObject (typically from InstantiateAsync).</summary>
    public static void ReleaseInstance(GameObject instance) {
      if (instance == null) return;
      EnsureInitialized();
      // For instantiated objects, Addressables expects ReleaseInstance, but we can use Release<GameObject>
      // If that fails, at minimum destroy the object
      try {
        if (s_Release != null) {
          var genericMethod = s_Release.MakeGenericMethod(typeof(GameObject));
          genericMethod.Invoke(null, new object[] { instance });
        } else {
          UnityEngine.Object.Destroy(instance);
        }
      } catch {
        // Fallback: just destroy the object
        UnityEngine.Object.Destroy(instance);
      }
    }

    private class CompletedHandler<T> where T : UnityEngine.Object {
      private readonly Action<T> _callback;
      private readonly string _address;

      public CompletedHandler(Action<T> callback, string address) {
        _callback = callback;
        _address = address;
      }

      public void OnCompleted<THandle>(THandle handle) {
        try {
          object boxed = handle; // box the struct so reflection works correctly
          var handleType = boxed.GetType();
          var status = handleType.GetProperty("Status")?.GetValue(boxed);
          if (status != null && Convert.ToInt32(status) != STATUS_SUCCEEDED) {
            var ex = handleType.GetProperty("OperationException")?.GetValue(boxed) as Exception;
            Debug.LogWarning($"[ModContentLoader] Load failed '{_address}': {ex?.Message ?? "Unknown error"}");
            _callback?.Invoke(null);
            return;
          }
          var result = handleType.GetProperty("Result")?.GetValue(boxed) as T;
          if (result == null) {
            Debug.LogWarning($"[ModContentLoader] Load succeeded but result is null for '{_address}'. Check if address exists in catalog.");
          }
          _callback?.Invoke(result);
        } catch (Exception ex) {
          Debug.LogWarning($"[ModContentLoader] Callback error '{_address}': {ex.Message}");
          _callback?.Invoke(null);
        }
      }
    }

    private class InstantiateHandler {
      private readonly Action<GameObject> _callback;
      private readonly string _address;
      private readonly bool _needsTransform;
      private readonly Vector3 _position;
      private readonly Quaternion _rotation;

      public InstantiateHandler(Action<GameObject> callback, string address, bool needsTransform, Vector3 pos, Quaternion rot) {
        _callback = callback;
        _address = address;
        _needsTransform = needsTransform;
        _position = pos;
        _rotation = rot;
      }

      public void OnCompleted<THandle>(THandle handle) {
        try {
          object boxed = handle; // box the struct so reflection works correctly
          var handleType = boxed.GetType();
          var status = handleType.GetProperty("Status")?.GetValue(boxed);
          if (status != null && Convert.ToInt32(status) != STATUS_SUCCEEDED) {
            var ex = handleType.GetProperty("OperationException")?.GetValue(boxed) as Exception;
            Debug.LogWarning($"[ModContentLoader] Instantiate failed '{_address}': {ex?.Message ?? "Unknown error"}");
            _callback?.Invoke(null);
            return;
          }
          var go = handleType.GetProperty("Result")?.GetValue(boxed) as GameObject;
          if (go == null) {
            Debug.LogWarning($"[ModContentLoader] Instantiate succeeded but result is null for '{_address}'. Check if prefab exists.");
          }
          if (go != null && _needsTransform) {
            go.transform.position = _position;
            go.transform.rotation = _rotation;
          }
          _callback?.Invoke(go);
        } catch (Exception ex) {
          Debug.LogWarning($"[ModContentLoader] Callback error '{_address}': {ex.Message}");
          _callback?.Invoke(null);
        }
      }
    }
  }

  /// <summary>
  /// Lean reflection helpers for Harmony patching.
  /// Generic, cached, and tolerant of assembly names.
  /// </summary>
  /// <remarks>
  /// This is part of the SDK. Do not modify - create your own helpers in your mod's Code/ folder.
  /// </remarks>
  public static class HarmonyTargets {

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