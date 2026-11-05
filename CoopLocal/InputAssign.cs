using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopLocal;

// Gives each window its own controls:
//  - its virtual gamepad (fed by XInputFeed with this player's controller);
//  - for the host, also the keyboard and mouse.
// Other devices are disabled in the window, so that a controller only drives one player.
// Controls stay active even when the window is not in the foreground.
// Keyboard and mouse only count for the host while its window is in the foreground:
// otherwise, mouse movements made in the other window switch the game to
// "keyboard/mouse" mode, and it then ignores player 1's controller.
public static class InputAssign
{
    private const float Period = 0.2f;
    private static float _timer = Period;
    private static bool _settingsApplied;
    private static string _lastSummary = "";

    public static void Tick(float dt)
    {
        _timer += dt;
        if (_timer < Period) return;
        _timer = 0f;
        try
        {
            // Re-applied on every pass: the game may restore its own settings after us.
            if (!Application.runInBackground) { Application.runInBackground = true; Note("runInBackground was disabled, re-enabled"); }
            var s = InputSystem.settings;
            if (s.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus)
            {
                s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Note(_settingsApplied ? "the game had restored backgroundBehavior, re-applied" : "backgroundBehavior = IgnoreFocus");
            }
            _settingsApplied = true;
            Assign();
            SchemeSync.Tick();
            Diagnose();
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("Gamepads: " + e.Message); }
    }

    private static void Note(string msg) => Plugin.Instance.Log.LogInfo("Gamepads: " + msg);

    // State report (every 5 s, only when it changes) to understand out-of-focus behavior.
    private static int _diagCount;
    private static string _lastDiag = "";
    private static void Diagnose()
    {
        if (++_diagCount % 25 != 0) return;
        var d = $"foreground={WindowLayout.IsForeground()} focus={Application.isFocused} bg={InputSystem.settings.backgroundBehavior} injection={(XInputFeed.Fed > 0 ? "yes" : "no")}";
        XInputFeed.Fed = 0;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            var p = pads[i];
            d += $" | pad{i} enabled={p.enabled} canRunInBg={p.canRunInBackground}";
        }
        if (d == _lastDiag) return;
        _lastDiag = d;
        Plugin.Instance.Log.LogInfo("STATE " + d);
    }

    private static void Assign()
    {
        // Only this window's virtual gamepad counts (see XInputFeed): the real ones are disabled.
        var mine = XInputFeed.Pad;
        int n = Gamepad.all.Count;
        bool keepKeyboardMouse = Role.IsFirstWindow && WindowLayout.IsForeground();

        var devices = InputSystem.devices;
        var summary = "";
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            bool want;
            if (d.TryCast<Gamepad>() != null) want = mine != null && d.deviceId == mine.deviceId;
            else if (d.TryCast<Keyboard>() != null || d.TryCast<Mouse>() != null) want = keepKeyboardMouse;
            else continue; // other devices (touchscreen, etc.): left alone
            if (want && !d.enabled) InputSystem.EnableDevice(d);
            if (!want && d.enabled) InputSystem.DisableDevice(d);
            summary += $"{d.displayName}={(want ? "YES" : "no")}; ";
        }
        if (summary != _lastSummary)
        {
            _lastSummary = summary;
            Plugin.Instance.Log.LogInfo($"Gamepads ({n} detected): {summary}");
        }
    }
}
