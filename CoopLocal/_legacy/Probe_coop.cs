using System;
using PawPaw.Game.Services.Input.Core;
using PawPaw.Game.Units.Controllers.Player;
using UnityEngine;

namespace CoopLocal;

// Diagnostic report (temporary): one line per second with the controller, the game's "Move" action,
// the local character's position and the frame rate.
public static class Probe
{
    private static float _timer;
    private static int _frames;
    private static InputService _svc;
    private static Vector3 _lastPos;

    public static void Tick(float dt)
    {
        _timer += dt;
        _frames++;
        if (_timer < 1f) return;
        try
        {
            var me = NetworkPlayer.LocalInstance;
            if (me == null || me.Character == null) return;
            if (_svc == null) _svc = UnityEngine.Object.FindObjectOfType<InputService>();
            var pos = me.Character.transform.position;
            var pad = XInputFeed.Pad;
            var move = _svc != null ? _svc.Gameplay.Move.ReadValue<Vector2>().ToString() : "?";
            var ctl = _svc?.Gameplay.Move.activeControl;
            Plugin.Instance.Log.LogInfo(
                $"SONDE premierPlan={WindowLayout.IsForeground()} focus={Application.isFocused} ips={_frames / _timer:F0} " +
                $"timeScale={Time.timeScale} stick={(pad != null ? pad.leftStick.ReadValue().ToString() : "-")} " +
                $"Move={move} ctl={(ctl != null ? ctl.path : "-")} schema={_svc?.ControlScheme} carte={_svc?.MapType} " +
                $"bouge={Vector3.Distance(_lastPos, pos):F2}m | relais={XInputFeed.RelayActive} xinput n°{XInputFeed.Slot} code={XInputFeed.LastCode} " +
                $"paquet={XInputFeed.LastPacket} LX={XInputFeed.LastLX} envois={XInputFeed.Fed} padId={pad?.deviceId} appuisA={XInputFeed.SouthPresses} | heures : input={UnityEngine.InputSystem.LowLevel.InputState.currentTime:F3} " +
                $"realtime={Time.realtimeSinceStartupAsDouble:F3} modeMaj={UnityEngine.InputSystem.InputSystem.settings.updateMode}");
            _lastPos = pos;
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("SONDE : " + e.Message); }
        finally { _timer = 0f; _frames = 0; }
    }
}
