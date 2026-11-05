using System;
using System.Diagnostics;

namespace CoopLocal;

// Windows favors the program whose window is in the foreground: the other player was stuttering.
// To compensate, the window that is not in the foreground gets "above normal" priority.
public static class ProcessPriority
{
    private const float Period = 0.5f;
    private static float _timer = Period;
    private static bool _disabled;

    public static void Tick(float dt)
    {
        _timer += dt;
        if (_timer < Period || _disabled) return;
        _timer = 0f;
        try
        {
            var want = WindowLayout.IsForeground() ? ProcessPriorityClass.Normal : ProcessPriorityClass.AboveNormal;
            using var me = Process.GetCurrentProcess();
            if (me.PriorityClass == want) return;
            me.PriorityClass = want;
            Plugin.Instance.Log.LogInfo($"Priorite de cette fenetre : {want}");
        }
        catch (Exception e)
        {
            _disabled = true;
            Plugin.Instance.Log.LogError("Priorite : desactivee suite a une erreur : " + e.Message);
        }
    }
}
