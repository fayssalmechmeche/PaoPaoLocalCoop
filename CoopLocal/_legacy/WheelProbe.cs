using System;
using PawPaw.Game.Services.Input.Core;
using PawPaw.UI.Gameplay.Systems.RadialWheel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopLocal;

// Diagnostic report (temporary) for the tool wheel: while it is open, what it reads
// as direction, and which controller Unity considers "the current gamepad".
public static class WheelProbe
{
    private static float _timer;
    private static bool _bindingsLogged;
    private static InputService _svc;

    // Logs the openings requested, refused or made by the game.
    public static void Apply()
    {
        try
        {
            var h = new HarmonyLib.Harmony("local.paopao.cooplocal.wheel");
            void Spy(string method, string prefix) =>
                h.Patch(HarmonyLib.AccessTools.Method(typeof(UIRadialWheel), method), prefix: new HarmonyLib.HarmonyMethod(typeof(WheelProbe), prefix));
            Spy(nameof(UIRadialWheel.RefuseOpen), nameof(OnRefuse));
            Spy(nameof(UIRadialWheel.OnRadialMenuStarted), nameof(OnStarted));
            Spy(nameof(UIRadialWheel.ToggleWheel), nameof(OnToggle));
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("WHEEL spy: " + e.Message); }
    }

    private static void OnRefuse(UIRadialWheel __instance, string reason) => Plugin.Instance.Log.LogInfo($"WHEEL {__instance.gameObject.name} open refused: {reason}");
    private static void OnStarted(UIRadialWheel __instance) => Plugin.Instance.Log.LogInfo($"WHEEL {__instance.gameObject.name} open requested");
    private static void OnToggle(UIRadialWheel __instance, bool state, bool force) => Plugin.Instance.Log.LogInfo($"WHEEL {__instance.gameObject.name} ToggleWheel({state}, force={force})");

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
                Plugin.Instance.Log.LogInfo("WHEEL open keys: " + s);
            }
            var wheels = UnityEngine.Object.FindObjectsOfType<UIRadialWheel>();
            for (int i = 0; i < wheels.Count; i++)
            {
                var w = wheels[i];
                if (!w.WheelActive) continue;
                var cur = Gamepad.current;
                Plugin.Instance.Log.LogInfo(
                    $"WHEEL {w.gameObject.name} direction={w.GetRawInputDirection()} currentGamepad={(cur != null ? cur.deviceId + "/" + cur.displayName + "/active=" + cur.enabled : "aucune")} " +
                    $"myGamepad={XInputFeed.Pad?.deviceId} stickR={XInputFeed.Pad?.rightStick.ReadValue()} stickL={XInputFeed.Pad?.leftStick.ReadValue()} " +
                    $"Look={_svc.Gameplay.Look.ReadValue<Vector2>()} Nav={_svc.UI.Navigate.ReadValue<Vector2>()} map={_svc.MapType} scheme={_svc.ControlScheme} cursor={_svc.LastCursorMode}");
            }
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("WHEEL: " + e.Message); _timer = -5f; }
    }
}
