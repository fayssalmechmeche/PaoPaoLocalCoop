using System;
using PawPaw.Game.Services.Input.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace CoopLocal;

// Relie la manette virtuelle au joueur de cette fenetre (PlayerInput) et active le mode "Gamepad".
// Le jeu ne bascule pas tout seul sur la manette virtuelle (elle est remplie directement, sans
// passer par la file d'evenements qu'il surveille) : sans ca, les menus et dialogues l'ignorent
// et les aides a l'ecran affichent les touches du clavier.
// Fenetre 1 : on rebascule sur la manette des qu'elle sert ; le jeu repasse seul au clavier/souris quand on s'en sert.
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
