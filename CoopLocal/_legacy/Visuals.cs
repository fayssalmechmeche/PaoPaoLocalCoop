using System;
using UnityEngine;
using PawPaw.Game.Units.Common;
using PawPaw.Game.Units.Controllers.Player;

namespace CoopLocal;

public static class PuppetVisuals
{
    private static float _timer;
    private static int _ticks;

    // Re-enables only the cat model (not hidden accessories such as sword, mask...).
    public static void DumpAndFix(Transform root)
    {
        var log = Plugin.Instance.Log;
        try
        {
            var av = root.Find("Avatar");
            if (av != null) { av.gameObject.SetActive(true); foreach (var r in av.GetComponentsInChildren<SkinnedMeshRenderer>(false)) r.enabled = true; }
            log.LogInfo("VISUEL: avatar reactive");
        }
        catch (Exception e) { log.LogError("VISUEL a echoue : " + e); }
    }

    // Checks every second (for ~15 s) player 2's visibility state relative to player 1.
    public static void Monitor(float dt, Character p2, Character host)
    {
        if (_ticks >= 15) return;
        _timer += dt;
        if (_timer < 1f) return;
        _timer = 0f; _ticks++;
        var log = Plugin.Instance.Log;
        try
        {
            string F(Character c) => $"Flags={c.Flags} VisualFlags={c.VisualFlags}";
            log.LogInfo($"MONITEUR[{_ticks}] P2: {F(p2)} | P1: {F(host)}");
            var av = p2.transform.Find("Avatar");
            if (av != null)
            {
                var smr = av.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                    log.LogInfo($"MONITEUR[{_ticks}] avatar.active={av.gameObject.activeSelf} smr.enabled={smr.enabled} visible={smr.isVisible} boundsCenter={smr.bounds.center} p2pos={p2.transform.position}");
            }
        }
        catch (Exception e) { log.LogError("MONITEUR : " + e.Message); }
    }
}
