#if UNITY_EDITOR
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
static class ServerDebug
{
    static bool s_logged;
    const bool DEBUG = true;

    static Type s_ServerType;
    static PropertyInfo s_IsServerProp;
    static FieldInfo s_IsServerField;

    static MethodBase TargetMethod() => STN.ModSDK.HarmonyTargets.MethodCached("Server:Update", Type.EmptyTypes);

    public static void Postfix()
    {
        if (s_logged) return;
		EnsureReflection();
		bool isServer = false;
		try
		{
			if (s_IsServerProp != null)
			{
				isServer = (bool)s_IsServerProp.GetValue(null, null);
			}
			else if (s_IsServerField != null)
			{
				isServer = (bool)s_IsServerField.GetValue(null);
			}
		}
		catch { isServer = false; }
        if (!isServer) return;
        s_logged = true;
        if (DEBUG) Debug.Log("[ServerExample] Server mod initialized (Update hook)");
    }

	static void EnsureReflection()
	{
		if (s_ServerType == null) s_ServerType = STN.ModSDK.HarmonyTargets.ResolveType("Server");
		if (s_IsServerProp == null && s_ServerType != null) s_IsServerProp = STN.ModSDK.HarmonyTargets.Property(s_ServerType, "IsServer");
		if (s_IsServerField == null && s_ServerType != null) s_IsServerField = STN.ModSDK.HarmonyTargets.Field(s_ServerType, "IsServer");
	}
}
#endif
