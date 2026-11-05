using System;
using HarmonyLib;
using UnityEngine;

namespace CoopLocal;

// Le jeu fige le personnage de l'hote quand sa fenetre n'a pas le focus.
// En coop locale, les deux fenetres doivent toujours se comporter comme si elles l'avaient.
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
