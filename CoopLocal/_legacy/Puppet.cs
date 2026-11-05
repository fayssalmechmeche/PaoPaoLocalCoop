using System;
using Unity.Netcode;
using UnityEngine;
using PawPaw.Game.Units.Controllers.Player;

namespace CoopLocal;

// Drives Player 2's "puppet" character: we move its Transform ourselves.
public static class Puppet
{
    private static Transform _t;
    private static Vector3 _pos;
    private static Quaternion _rot = Quaternion.identity;
    private static Transform _avatar;
    private const float Speed = 5f;

    public static bool Active => _t != null;
    private static PawPaw.Game.Units.Common.Character _hostChar;

    // Called once, when player 2's character exists: place it near player 1.
    public static void Attach(Transform p2Char, Transform hostChar)
    {
        _t = p2Char;
        _hostChar = hostChar.GetComponent<PawPaw.Game.Units.Common.Character>();
        _pos = hostChar.position + hostChar.right * 1.5f;
        _rot = hostChar.rotation;
        ReadHostCollisionSetup(hostChar);
        // Disable whatever could overwrite our position.
        try { int n = 0; foreach (var nt in p2Char.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true)) { nt.enabled = false; n++; } Plugin.Instance.Log.LogInfo($"PUPPET: {n} NetworkTransform(s) disabled"); } catch (Exception e) { Plugin.Instance.Log.LogWarning("PUPPET NT: " + e.Message); }
        _avatar = p2Char.Find("Avatar");
        try { var rb = p2Char.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true; } catch { }
        try { var col = p2Char.GetComponent<CapsuleCollider>(); if (col != null) col.enabled = false; } catch { }
        Plugin.Instance.Log.LogInfo($"PUPPET: attached, start position {_pos}");
        PuppetVisuals.DumpAndFix(p2Char);
    }

    private static Vector2 ReadInput()
    {
        var v = Vector2.zero;
        try
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (k.upArrowKey.isPressed) v.y += 1f;
                if (k.downArrowKey.isPressed) v.y -= 1f;
                if (k.rightArrowKey.isPressed) v.x += 1f;
                if (k.leftArrowKey.isPressed) v.x -= 1f;
            }
        }
        catch { }
        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    private const float StepHeight = 0.4f;
    private const float Skin = 0.05f;
    private static float _radius = 0.4f;
    private static float _height = 1.2f;
    private static int _mask = ~0;

    // Reads player 1's capsule size and collision layers, to imitate its behavior.
    private static void ReadHostCollisionSetup(Transform hostChar)
    {
        var log = Plugin.Instance.Log;
        try
        {
            var cap = hostChar.GetComponent<CapsuleCollider>();
            if (cap != null) { _radius = cap.radius; _height = cap.height; }
            var mv = hostChar.GetComponent<PawPaw.Game.Units.Common.CharacterMovement>();
            if (mv != null) _mask = mv.DefaultCollisionLayers.value;
        }
        catch (Exception e) { log.LogWarning("PUPPET collisions: " + e.Message); }
        log.LogInfo($"PUPPET: capsule radius={_radius} height={_height} mask=0x{_mask:X}");
    }

    // Moves by 'delta', sliding along obstacles (2 attempts: direct, then slide).
    private static Vector3 MoveWithCollisions(Vector3 pos, Vector3 delta)
    {
        for (int i = 0; i < 2 && delta.sqrMagnitude > 1e-8f; i++)
        {
            float dist = delta.magnitude;
            var dir = delta / dist;
            var bottom = pos + Vector3.up * (_radius + StepHeight);
            var top = pos + Vector3.up * Mathf.Max(_radius + StepHeight, _height - _radius);
            RaycastHit hit;
            if (!Physics.CapsuleCast(bottom, top, _radius, dir, out hit, dist + Skin, _mask, QueryTriggerInteraction.Ignore))
                return pos + delta;
            // Move until contact, then keep only the part parallel to the wall.
            float allowed = Mathf.Max(0f, hit.distance - Skin);
            pos += dir * allowed;
            var rest = delta - dir * allowed;
            var n = hit.normal; n.y = 0f;
            if (n.sqrMagnitude < 1e-6f) return pos;
            n.Normalize();
            delta = rest - n * Vector3.Dot(rest, n);
        }
        return pos;
    }

    // Called every frame (LateUpdate).
    public static void Apply(float dt)
    {
        if (_t == null) return;
        try
        {
            var input = ReadInput();
            var cam = Camera.main;
            if (cam != null && input.sqrMagnitude > 0.0001f)
            {
                var f = cam.transform.forward; f.y = 0f; f.Normalize();
                var r = cam.transform.right; r.y = 0f; r.Normalize();
                var dir = r * input.x + f * input.y;
                _pos = MoveWithCollisions(_pos, dir * Speed * dt);
                _rot = Quaternion.Slerp(_rot, Quaternion.LookRotation(dir), 12f * dt);
            }
            // Height: downward ray from step height (not from above the roofs).
            RaycastHit hit;
            if (Physics.Raycast(_pos + Vector3.up * StepHeight, Vector3.down, out hit, 5f, _mask, QueryTriggerInteraction.Ignore))
                _pos.y = Mathf.Lerp(_pos.y, hit.point.y, 20f * dt);
            _t.SetPositionAndRotation(_pos, _rot);
            if (_avatar != null) { _avatar.localPosition = Vector3.zero; _avatar.localRotation = Quaternion.identity; }
            var c2 = _t.GetComponent<PawPaw.Game.Units.Common.Character>();
            if (c2 != null && _hostChar != null) PuppetVisuals.Monitor(dt, c2, _hostChar);
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("PUPPET: " + e.Message); _t = null; }
    }
}

