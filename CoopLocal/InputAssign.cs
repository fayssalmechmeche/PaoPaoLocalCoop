using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopLocal;

// Donne a chaque fenetre ses propres commandes :
//  - sa manette virtuelle (alimentee par XInputFeed avec la manette de ce joueur) ;
//  - pour l'hote, en plus, le clavier et la souris.
// Les autres appareils sont desactives dans la fenetre, pour qu'une manette ne pilote qu'un joueur.
// Les commandes restent actives meme quand la fenetre n'est pas au premier plan.
// Le clavier et la souris ne comptent pour l'hote que lorsque sa fenetre est au premier plan :
// sinon, les mouvements de souris faits dans l'autre fenetre font basculer le jeu en mode
// "clavier/souris", et il ignore alors la manette du joueur 1.
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
            // Reimpose a chaque passage : le jeu peut remettre ses propres reglages apres nous.
            if (!Application.runInBackground) { Application.runInBackground = true; Note("runInBackground etait desactive, reactive"); }
            var s = InputSystem.settings;
            if (s.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus)
            {
                s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Note(_settingsApplied ? "le jeu avait remis backgroundBehavior, reimpose" : "backgroundBehavior = IgnoreFocus");
            }
            _settingsApplied = true;
            Assign();
            SchemeSync.Tick();
            Diagnose();
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("Manettes : " + e.Message); }
    }

    private static void Note(string msg) => Plugin.Instance.Log.LogInfo("Manettes : " + msg);

    // Releve d'etat (toutes les 5 s, seulement s'il change) pour comprendre le comportement hors focus.
    private static int _diagCount;
    private static string _lastDiag = "";
    private static void Diagnose()
    {
        if (++_diagCount % 25 != 0) return;
        var d = $"premierPlan={WindowLayout.IsForeground()} focus={Application.isFocused} bg={InputSystem.settings.backgroundBehavior} injection={(XInputFeed.Fed > 0 ? "oui" : "non")}";
        XInputFeed.Fed = 0;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            var p = pads[i];
            d += $" | pad{i} enabled={p.enabled} canRunInBg={p.canRunInBackground}";
        }
        if (d == _lastDiag) return;
        _lastDiag = d;
        Plugin.Instance.Log.LogInfo("ETAT " + d);
    }

    private static void Assign()
    {
        // Seule la manette virtuelle de cette fenetre compte (voir XInputFeed) : les vraies sont coupees.
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
            else continue; // autres appareils (ecran tactile, etc.) : on n'y touche pas
            if (want && !d.enabled) InputSystem.EnableDevice(d);
            if (!want && d.enabled) InputSystem.DisableDevice(d);
            summary += $"{d.displayName}={(want ? "OUI" : "non")}; ";
        }
        if (summary != _lastSummary)
        {
            _lastSummary = summary;
            Plugin.Instance.Log.LogInfo($"Manettes ({n} detectee(s)) : {summary}");
        }
    }
}
