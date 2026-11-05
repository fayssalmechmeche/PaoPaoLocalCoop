using System;
using HarmonyLib;
using PawPaw.Game.Units.Controllers.Player;
using UnityEngine;

namespace CoopLocal;

// Auto-test (option de lancement --coop-selftest, fenetre hote seulement) :
// simule le stick gauche pousse a droite pendant 2 s et mesure si le personnage local a bouge.
//   essai 1 : tel quel ;  essai 2 : en faisant croire a Unity que la fenetre a le focus.
public static class SelfTest
{
    private static readonly bool Enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "--coop-selftest") >= 0;
    private static float _t;
    private static int _phase;
    private static float _phaseT;
    private static Vector3 _start;
    private static Harmony _harmony;

    private static BepInEx.Logging.ManualLogSource Log => Plugin.Instance.Log;

    public static void Tick(float dt)
    {
        if (!Enabled || _phase >= 6) return;
        _t += dt;
        _phaseT += dt;
        try
        {
            var me = NetworkPlayer.LocalInstance;
            if (me == null || me.Character == null || _t < 90f) return;
            var pos = me.Character.transform.position;
            switch (_phase)
            {
                case 0: // essai 1
                case 3: // essai 2
                    XInputFeed.Override = true;
                    _start = pos;
                    Log.LogInfo($"AUTOTEST essai {(_phase == 0 ? 1 : 2)} : debut, premierPlan={WindowLayout.IsForeground()} focus={Application.isFocused} pos={pos}");
                    Next();
                    break;
                case 1:
                case 4:
                    XInputFeed.Send(0, 0, 0, short.MaxValue, 0, 0, 0);
                    if (!_measured && _phaseT >= 1f) { _measured = true; Measure(); }
                    if (_phaseT >= 2f)
                    {
                        XInputFeed.Send(0, 0, 0, 0, 0, 0, 0);
                        Log.LogInfo($"AUTOTEST essai {(_phase == 1 ? 1 : 2)} : deplacement = {Vector3.Distance(_start, pos):F2} m (focus={Application.isFocused})");
                        Next();
                    }
                    break;
                case 2: // pause, puis on force le focus pour l'essai 2
                    if (_phaseT < 2f) return;
                    _harmony ??= new Harmony("local.paopao.cooplocal.selftest");
                    _harmony.Patch(AccessTools.PropertyGetter(typeof(Application), nameof(Application.isFocused)),
                        prefix: new HarmonyMethod(typeof(SelfTest), nameof(FocusPrefix)));
                    Log.LogInfo($"AUTOTEST : focus force (Application.isFocused={Application.isFocused})");
                    Next();
                    break;
                case 5:
                    XInputFeed.Override = false;
                    Log.LogInfo("AUTOTEST : termine");
                    Next();
                    break;
            }
        }
        catch (Exception e) { Log.LogError("AUTOTEST : " + e); _phase = 6; XInputFeed.Override = false; }
    }

    private static void Next() { _phase++; _phaseT = 0f; _measured = false; }
    private static bool _measured;

    // Ou se perd l'information ? manette (Unity) -> action "Move" du jeu -> personnage.
    private static void Measure()
    {
        var pad = XInputFeed.Target;
        Log.LogInfo($"AUTOTEST   manette '{pad?.displayName}' id={pad?.deviceId} enabled={pad?.enabled} stick={pad?.leftStick.ReadValue()}");
        var svc = UnityEngine.Object.FindObjectOfType<PawPaw.Game.Services.Input.Core.InputService>();
        if (svc == null) { Log.LogInfo("AUTOTEST   InputService introuvable"); return; }
        var move = svc.Gameplay.Move;
        var ctl = move.activeControl;
        Log.LogInfo($"AUTOTEST   carte={svc.MapType} schema={svc.ControlScheme} Gameplay.enabled={svc.Gameplay.enabled} Move.enabled={move.enabled} Move={move.ReadValue<Vector2>()} controle actif={(ctl != null ? ctl.path : "aucun")}");
        var pi = svc._playerInput;
        if (pi != null)
        {
            var devs = "";
            var paired = pi.devices;
            for (int i = 0; i < paired.Count; i++) devs += $"{paired[i].displayName}#{paired[i].deviceId} ";
            Log.LogInfo($"AUTOTEST   PlayerInput schema={pi.currentControlScheme} appareils associes={devs}");
        }
    }

    private static bool FocusPrefix(ref bool __result) { __result = true; return false; }
}
