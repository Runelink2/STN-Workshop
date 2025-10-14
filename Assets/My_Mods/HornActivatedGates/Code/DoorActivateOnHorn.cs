using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
static class DoorActivateOnHorn
{
    // Mod example: Honk to open nearby gates (ObjectMetaData-based), then auto-close.
    // - Uses Harmony to postfix VehicleClient.HonkHorn
    // - Finds nearby ObjectMetaData with specific gate typeIDs
    // - Activates gate via MachineInteraction.ActivateInstantiatingMachineWithPartCheck()
    // - Schedules a delayed close via ObjectMetaData.DeActivateInstantiatingMachine()
    // - Before closing, checks DoorBlocker.DoorIsBlocked() and retries a few times if obstructed

    // Tweakable settings for the example mod
    const bool DEBUG = true;                  // Set false to silence informational logs
    const float CloseDelaySeconds = 10f;      // First close attempt after opening
    const float RetryDelaySeconds = 5f;       // Delay between retries when obstructed
    const int MaxCloseAttempts = 3;           // Total attempts when obstructed

    // Cached reflection to avoid repeated lookups
    static Type s_ObjectMetaType;
    static Type s_MachineInteractionType;
    static MethodInfo s_MI_ActivateInstantiating;
    static PropertyInfo s_OM_TypeId;
    static FieldInfo s_OM_ObjectId;
    static MethodInfo s_OM_Deactivate;
    static Type s_DoorBlockerType;
    static MethodInfo s_DB_DoorIsBlocked;
    // Known gate typeIDs (ObjectMetaData.typeID). Update this list to include more gates.
    static readonly System.Collections.Generic.HashSet<int> s_GateTypeIds = new System.Collections.Generic.HashSet<int>(new int[] { 4055, 4056, 4059 });
    static readonly Collider[] s_Overlap = new Collider[128];

    static System.Reflection.MethodBase TargetMethod() => STN.ModSDK.HarmonyTargets.MethodCached("VehicleClient:HonkHorn", Type.EmptyTypes);

    public static void Postfix(object __instance)
    {
        var comp = __instance as Component;
        if (comp == null)
        {
            Debug.LogWarning("[HornGates] Postfix __instance is not a Component; aborting");
            return;
        }
        if (DEBUG) Debug.Log($"[HornGates] HonkHorn postfix fired at {comp.transform.position}");
        TryOpenNearbyDoors(comp.transform.position, 25f, maxDoors: 6);
    }

    static void TryOpenNearbyDoors(Vector3 origin, float radius, int maxDoors)
    {
        EnsureReflection();

        int hitCount;
        try
        {
            hitCount = Physics.OverlapSphereNonAlloc(origin, radius, s_Overlap, ~0, QueryTriggerInteraction.Collide);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HornGates] OverlapSphereNonAlloc failed: {ex.Message}");
            return;
        }
        if (hitCount <= 0)
        {
            if (DEBUG) Debug.Log("[HornGates] OverlapSphere found 0 colliders");
            return;
        }
        if (DEBUG) Debug.Log($"[HornGates] OverlapSphere hits: {hitCount}");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var found = 0;

        // Pass 0: Prioritize ObjectMetaData-based gates with known typeIDs
        if (s_ObjectMetaType != null && s_OM_TypeId != null)
        {
            for (int i = 0; i < hitCount && found < maxDoors; i++)
            {
                var om = s_Overlap[i].GetComponentInParent(s_ObjectMetaType) as Component;
                if (om == null) continue;
                int typeId = 0;
                try { typeId = (int)s_OM_TypeId.GetValue(om); } catch { typeId = 0; }
                if (!s_GateTypeIds.Contains(typeId)) continue;

                string omId = null;
                try { if (s_OM_ObjectId != null) omId = s_OM_ObjectId.GetValue(om) as string; } catch { omId = null; }
                if (!string.IsNullOrEmpty(omId) && seenIds.Contains(omId)) continue;

                // Try MachineInteraction first (instantiating gates)
                var mi = om.GetComponentInChildren(s_MachineInteractionType) ?? om.GetComponentInParent(s_MachineInteractionType);
                if (mi != null && s_MI_ActivateInstantiating != null)
                {
                    try
                    {
                        s_MI_ActivateInstantiating.Invoke(mi, null);
                        if (DEBUG) Debug.Log($"[HornGates] Gate(OM type {typeId}) -> ActivateInstantiatingMachineWithPartCheck()");
                        if (!string.IsNullOrEmpty(omId)) seenIds.Add(omId);
                        found++;
                        // Schedule close after first delay
                        if (s_OM_Deactivate != null)
                        {
                            HornGateScheduler.ScheduleClose(om, CloseDelaySeconds, s_OM_Deactivate, s_DoorBlockerType, s_DB_DoorIsBlocked, MaxCloseAttempts, RetryDelaySeconds, DEBUG);
                        }
                        continue;
                    }
                    catch (System.Exception ex)
                    {
                        if (DEBUG) Debug.LogWarning($"[HornGates] Gate(OM type {typeId}) instantiating activation failed: {ex.Message}");
                    }
                }

            }
        }
    }

    static void EnsureReflection()
    {
        // Lean helper usage: generic, cached lookups
        if (s_ObjectMetaType == null) s_ObjectMetaType = STN.ModSDK.HarmonyTargets.ResolveType("ObjectMetaData");
        if (s_MachineInteractionType == null) s_MachineInteractionType = STN.ModSDK.HarmonyTargets.ResolveType("MachineInteraction");
        if (s_MI_ActivateInstantiating == null && s_MachineInteractionType != null) s_MI_ActivateInstantiating = STN.ModSDK.HarmonyTargets.Method(s_MachineInteractionType, "ActivateInstantiatingMachineWithPartCheck");
        if (s_OM_TypeId == null && s_ObjectMetaType != null) s_OM_TypeId = STN.ModSDK.HarmonyTargets.Property(s_ObjectMetaType, "typeID");
        if (s_OM_ObjectId == null && s_ObjectMetaType != null) s_OM_ObjectId = STN.ModSDK.HarmonyTargets.Field(s_ObjectMetaType, "objectID");
        if (s_OM_Deactivate == null && s_ObjectMetaType != null) s_OM_Deactivate = STN.ModSDK.HarmonyTargets.Method(s_ObjectMetaType, "DeActivateInstantiatingMachine");
        if (s_DoorBlockerType == null) s_DoorBlockerType = STN.ModSDK.HarmonyTargets.ResolveType("DoorBlocker");
        if (s_DB_DoorIsBlocked == null && s_DoorBlockerType != null) s_DB_DoorIsBlocked = STN.ModSDK.HarmonyTargets.Method(s_DoorBlockerType, "DoorIsBlocked", new[] { typeof(bool) });
    }
}

internal class HornGateScheduler : MonoBehaviour
{
    static HornGateScheduler _inst;
    public static void Ensure()
    {
        if (_inst != null) return;
        var go = new GameObject("HornGateScheduler");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<HornGateScheduler>();
    }
    public static void ScheduleClose(Component objectMeta, float delaySeconds, MethodInfo deactivateMethod, Type doorBlockerType, MethodInfo doorIsBlocked, int maxAttempts, float retryDelay, bool debug)
    {
        Ensure();
        if (_inst != null) _inst.StartCoroutine(_inst.CloseAfterDelay(objectMeta, delaySeconds, deactivateMethod, doorBlockerType, doorIsBlocked, maxAttempts, retryDelay, debug));
    }
    IEnumerator CloseAfterDelay(Component objectMeta, float delaySeconds, MethodInfo deactivateMethod, Type doorBlockerType, MethodInfo doorIsBlocked, int maxAttempts, float retryDelay, bool debug)
    {
        int attempts = 0;
        yield return new WaitForSeconds(delaySeconds);
        while (attempts < maxAttempts)
        {
            if (objectMeta == null || deactivateMethod == null) yield break;
            bool blocked = false;
            try
            {
                if (doorBlockerType != null && doorIsBlocked != null)
                {
                    var blocker = (objectMeta as Component)?.GetComponentInChildren(doorBlockerType, true);
                    if (blocker != null)
                    {
                        var res = doorIsBlocked.Invoke(blocker, new object[] { false });
                        blocked = res is bool b && b;
                    }
                }
            }
            catch { blocked = false; }

            if (!blocked)
            {
                try
                {
                    deactivateMethod.Invoke(objectMeta, null);
                    if (debug) Debug.Log("[HornGates] Gate -> DeActivateInstantiatingMachine() after delay");
                }
                catch (Exception ex)
                {
                    if (debug) Debug.LogWarning($"[HornGates] Delayed close failed: {ex.Message}");
                }
                yield break;
            }
            else
            {
                attempts++;
                if (attempts >= maxAttempts)
                {
                    if (debug) Debug.Log("[HornGates] Skipping close after max blocked attempts");
                    yield break;
                }
                if (debug) Debug.Log($"[HornGates] Close blocked; retrying in {retryDelay:0.#}s...");
                yield return new WaitForSeconds(retryDelay); 
            }
        }
    }
}
