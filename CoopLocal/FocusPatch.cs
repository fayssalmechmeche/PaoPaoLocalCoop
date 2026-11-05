using System;
using HarmonyLib;
using UnityEngine;

namespace CoopLocal;

// The game freezes the host's character when its window does not have focus.
// In local co-op, both windows must always behave as if they had it.
public static class FocusPatch
{
    public static void Apply()
    {
        try
        {
            new Harmony("local.paopao.cooplocal.focus").Patch(
                AccessTools.PropertyGetter(typeof(Application), nameof(Application.isFocused)),
                prefix: new HarmonyMethod(typeof(FocusPatch), nameof(Prefix)));
            Plugin.Instance.Log.LogInfo("Focus : la fenetre se comporte toujours comme active");
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("Focus : " + e.Message); }
    }

    private static bool Prefix(ref bool __result) { __result = true; return false; }
}
