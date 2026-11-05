using System;
using PawPaw.Game.Services.Input.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace CoopLocal;

// Binds the virtual gamepad to this window's player (PlayerInput) and enables "Gamepad" mode.
// The game does not switch to the virtual gamepad on its own (it is filled directly, without
// going through the event queue it watches): without this, menus and dialogs ignore it
// and on-screen hints show keyboard keys.
// Window 1: switch back to the gamepad as soon as it is used; the game returns to keyboard/mouse by itself when those are used.
public static class SchemeSync
{
    private static InputService _svc;
    private static string _lastLog = "";

    public static void Tick()
    {
        try
        {
            var pad = XInputFeed.Pad;
            if (pad == null) return;
            if (_svc == null) _svc = UnityEngine.Object.FindObjectOfType<InputService>();
            var pi = _svc?._playerInput;
            if (pi == null) return;

            bool padRecentlyUsed = Time.unscaledTime - XInputFeed.LastActive < 1f;
            bool wantPad = !Role.IsFirstWindow || padRecentlyUsed;
            if (!wantPad) return;

            bool paired = false;
            var devices = pi.devices;
            for (int i = 0; i < devices.Count; i++)
                if (devices[i].deviceId == pad.deviceId) paired = true;
            if (paired && pi.currentControlScheme == "Gamepad") return;

            var user = InputUser.PerformPairingWithDevice(pad, pi.user, InputUserPairingOptions.UnpairCurrentDevicesFromUser);
            user.ActivateControlScheme("Gamepad");
            Log($"Mode manette active pour ce joueur (avant : {(paired ? "associee" : "non associee")}, maintenant : {pi.currentControlScheme})");
        }
        catch (Exception e) { Log("Mode manette : " + e.Message); }
    }

    private static void Log(string msg)
    {
        if (msg == _lastLog) return;
        _lastLog = msg;
        Plugin.Instance.Log.LogInfo(msg);
    }
}
