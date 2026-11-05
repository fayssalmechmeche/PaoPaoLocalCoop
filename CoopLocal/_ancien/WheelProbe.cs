using System;
using PawPaw.Game.Services.Input.Core;
using PawPaw.UI.Gameplay.Systems.RadialWheel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopLocal;

// Releve de diagnostic (temporaire) pour la roue des outils : quand elle est ouverte, ce qu'elle lit
// comme direction, et quelle manette Unity considere comme "la manette actuelle".
public static class WheelProbe
{
    private static float _timer;
    private static bool _bindingsLogged;
    private static InputService _svc;

    // Note dans le journal les ouvertures demandees, refusees ou faites par le jeu.
    public static void Apply()
    {
        try
        {
            var h = new HarmonyLib.Harmony("local.paopao.cooplocal.roue");
            void Spy(string method, string prefix) =>
                h.Patch(HarmonyLib.AccessTools.Method(typeof(UIRadialWheel), method), prefix: new HarmonyLib.HarmonyMethod(typeof(WheelProbe), prefix));
            Spy(nameof(UIRadialWheel.RefuseOpen), nameof(OnRefuse));
            Spy(nameof(UIRadialWheel.OnRadialMenuStarted), nameof(OnStarted));
            Spy(nameof(UIRadialWheel.ToggleWheel), nameof(OnToggle));
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("ROUE espion : " + e.Message); }
    }

    private static void OnRefuse(UIRadialWheel __instance, string reason) => Plugin.Instance.Log.LogInfo($"ROUE {__instance.gameObject.name} ouverture refusee : {reason}");
    private static void OnStarted(UIRadialWheel __instance) => Plugin.Instance.Log.LogInfo($"ROUE {__instance.gameObject.name} demande d'ouverture");
    private static void OnToggle(UIRadialWheel __instance, bool state, bool force) => Plugin.Instance.Log.LogInfo($"ROUE {__instance.gameObject.name} ToggleWheel({state}, force={force})");

    public static void Tick(float dt)
    {
        _timer += dt;
        if (_timer < 0.25f) return;
        _timer = 0f;
        try
        {
            if (_svc == null) _svc = UnityEngine.Object.FindObjectOfType<InputService>();
            if (_svc == null) return;
            if (!_bindingsLogged)
            {
                _bindingsLogged = true;
                var b = _svc.Common.RadialMenu.bindings;
                var s = "";
                for (int i = 0; i < b.Count; i++) s += b[i].effectivePath + " ";
                Plugin.Instance.Log.LogInfo("ROUE touches d'ouverture : " + s);
            }
            var wheels = UnityEngine.Object.FindObjectsOfType<UIRadialWheel>();
            for (int i = 0; i < wheels.Count; i++)
            {
                var w = wheels[i];
                if (!w.WheelActive) continue;
                var cur = Gamepad.current;
                Plugin.Instance.Log.LogInfo(
                    $"ROUE {w.gameObject.name} direction={w.GetRawInputDirection()} manetteActuelle={(cur != null ? cur.deviceId + "/" + cur.displayName + "/actif=" + cur.enabled : "aucune")} " +
                    $"maManette={XInputFeed.Pad?.deviceId} stickD={XInputFeed.Pad?.rightStick.ReadValue()} stickG={XInputFeed.Pad?.leftStick.ReadValue()} " +
                    $"Look={_svc.Gameplay.Look.ReadValue<Vector2>()} Nav={_svc.UI.Navigate.ReadValue<Vector2>()} carte={_svc.MapType} schema={_svc.ControlScheme} curseur={_svc.LastCursorMode}");
            }
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("ROUE : " + e.Message); _timer = -5f; }
    }
}
