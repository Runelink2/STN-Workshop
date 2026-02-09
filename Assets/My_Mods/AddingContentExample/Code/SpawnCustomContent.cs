using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
static class SpawnCustomContent
{
    const bool DEBUG = true;
    const string PrefabAddress = "Mod/AddingContentExample/Assets/ExampleSphere";
    const string AudioAddress = "Mod/AddingContentExample/Assets/EWWUPPP";

    static bool s_kDown, s_lDown;
    static AudioClip s_clip;

    static MethodBase TargetMethod() => STN.ModSDK.HarmonyTargets.MethodCached("PlayerOwner:Update", Type.EmptyTypes);

    public static void Postfix(object __instance)
    {
        var comp = __instance as Component;
        if (comp == null) return;

        // K = spawn prefab
        bool k = Input.GetKey(KeyCode.K);
        if (k && !s_kDown)
        {
            var pos = comp.transform.position + comp.transform.forward * 3f + Vector3.up;
            if (DEBUG) Debug.Log($"[AddingContentExample] Spawning at {pos}");
            try
            {
                STN.ModSDK.ModContentLoader.InstantiateAsync(PrefabAddress, pos, Quaternion.identity, go =>
                {
                    Debug.Log($"[AddingContentExample] Spawn callback fired, result: {(go != null ? go.name : "NULL")}");
                    if (go != null) { if (DEBUG) Debug.Log($"[AddingContentExample] Spawned {go.name}"); }
                    else Debug.LogWarning("[AddingContentExample] Spawn failed - object is null");
                });
                if (DEBUG) Debug.Log("[AddingContentExample] InstantiateAsync called");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddingContentExample] InstantiateAsync exception: {ex}");
            }
        }
        s_kDown = k;

        // L = play sound
        bool l = Input.GetKey(KeyCode.L);
        if (l && !s_lDown)
        {
            if (s_clip != null)
            {
                AudioSource.PlayClipAtPoint(s_clip, comp.transform.position);
                if (DEBUG) Debug.Log("[AddingContentExample] Playing cached clip");
            }
            else
            {
                if (DEBUG) Debug.Log("[AddingContentExample] Loading audio...");
                try
                {
                    STN.ModSDK.ModContentLoader.LoadAssetAsync<AudioClip>(AudioAddress, clip =>
                    {
                        Debug.Log($"[AddingContentExample] Audio callback fired, result: {(clip != null ? clip.name : "NULL")}");
                        if (clip != null) { s_clip = clip; AudioSource.PlayClipAtPoint(clip, comp.transform.position); }
                        else Debug.LogWarning("[AddingContentExample] Audio load failed - clip is null");
                    });
                    if (DEBUG) Debug.Log("[AddingContentExample] LoadAssetAsync called");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AddingContentExample] LoadAssetAsync exception: {ex}");
                }
            }
        }
        s_lDown = l;
    }
}
