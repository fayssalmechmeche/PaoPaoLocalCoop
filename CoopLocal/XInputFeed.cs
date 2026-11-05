using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CoopLocal;

// This window's virtual gamepad.
// Unity no longer reads Xbox controllers in a window that is not in the foreground, and its own
// (frozen) reads overwrite what we send it. So each window gets a virtual gamepad
// that belongs only to this player, filled with the state published by the
// XInputRelay.exe relay (started by the launcher). Real controllers are disabled in both windows.
public static unsafe class XInputFeed
{
    // State of a standard Unity gamepad (GamepadState, 'GPAD' format, 28 bytes):
    // buttons (4) + left stick (8) + right stick (8) + left trigger (4) + right trigger (4).
    private static readonly int FormatGamepad = MakeFourCC('G', 'P', 'A', 'D');
    private static readonly int EventState = MakeFourCC('S', 'T', 'A', 'T');
    private const int StateSize = 28;
    private const int EventSize = 24 + StateSize; // InputEvent header (20) + format (4) + state

    private static IntPtr _buffer = IntPtr.Zero;
    private static bool _disabled;
    private static uint _lastPacket = uint.MaxValue;
    private static float _keepAlive;

    public static Gamepad Pad;   // this window's virtual gamepad
    public static int Slot = -1; // XInput slot (0 to 3) read for this player
    public static int Fed;       // number of events sent (for the state report)
    public static float LastActive = -100f; // last time a button or stick was used

    // Diagnostics: last raw XInput read.
    public static uint LastCode = uint.MaxValue, LastPacket;
    public static short LastLX;

    // Called by Unity at the start of each input update, before the game's scripts:
    // a press applied here is seen by the game as "pressed this frame" (otherwise A was missed).
    private static bool _hooked;
    private static float _lastTick;
    public static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        InputSystem.add_onBeforeUpdate((Il2CppSystem.Action)(Action)OnBeforeUpdate);
        Plugin.Instance.Log.LogInfo("Virtual gamepad: hooked at the start of each frame");
    }

    private static void OnBeforeUpdate()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        float now = Time.unscaledTime;
        Tick(now - _lastTick);
        _lastTick = now;
    }


    private static void Tick(float dt)
    {
        if (_disabled) return;
        try
        {
            if (Pad == null)
            {
                Pad = InputSystem.AddDevice("Gamepad", "CoopLocal Gamepad").TryCast<Gamepad>();
                Plugin.Instance.Log.LogInfo($"Virtual gamepad created (id={Pad?.deviceId})");
                if (Pad == null) { _disabled = true; return; }
            }
            ChooseSlot();
            if (Slot < 0) return;

            LastCode = ReadState(Slot, out var st);
            LastPacket = st.PacketNumber;
            LastLX = st.ThumbLX;
            if (LastCode != 0) return; // controller unplugged

            _keepAlive += dt;
            if (st.PacketNumber == _lastPacket && _keepAlive < 0.5f) return; // nothing changed
            _lastPacket = st.PacketNumber;
            _keepAlive = 0f;
            Send(st);
        }
        catch (Exception e)
        {
            _disabled = true;
            Plugin.Instance.Log.LogError("Virtual gamepad: disabled after an error: " + e);
        }
    }

    // Window 1 (left) = 1st connected controller, window 2 = 2nd (or the only one if there is just one: window 1 then keeps keyboard/mouse).
    private static float _slotCheck;
    private static void ChooseSlot()
    {
        _slotCheck -= Time.unscaledDeltaTime;
        if (_slotCheck > 0f && Slot >= 0) return;
        _slotCheck = 1f;
        int first = -1, second = -1;
        for (int i = 0; i < 4; i++)
        {
            if (ReadState(i, out _) != 0) continue;
            if (first < 0) first = i; else if (second < 0) second = i;
        }
        int slot = Role.IsFirstWindow ? (second >= 0 ? first : -1)
                                                 : (second >= 0 ? second : first);
        if (slot != Slot)
        {
            Slot = slot;
            _lastPacket = uint.MaxValue;
            Plugin.Instance.Log.LogInfo(slot >= 0 ? $"Virtual gamepad: reading XInput controller #{slot}" : "Virtual gamepad: no controller for this player");
            if (slot < 0) Send(default); // release everything
        }
    }

    // Reads a controller. In the game process, XInput stays frozen once the window has lost
    // focus once: so we read the state published by the relay. Without a relay, read directly.
    private static MemoryMappedFile _relayMap;
    private static MemoryMappedViewAccessor _relay;
    private static float _nextRelayTry;
    private static bool _relayLogged;
    public static bool RelayActive => _relay != null;

    private static uint ReadState(int slot, out XInputState st)
    {
        if (_relay == null && Time.unscaledTime >= _nextRelayTry)
        {
            _nextRelayTry = Time.unscaledTime + 2f;
            try
            {
                // Also keep the file open: otherwise its name disappears and a restarted relay would not find it.
                _relayMap = MemoryMappedFile.OpenExisting("Local\\PaoPaoCoopXInput");
                _relay = _relayMap.CreateViewAccessor();
                Plugin.Instance.Log.LogInfo("Virtual gamepad: XInputRelay relay connected");
            }
            catch
            {
                if (!_relayLogged) { _relayLogged = true; Plugin.Instance.Log.LogWarning("Virtual gamepad: XInputRelay relay not found, reading directly"); }
            }
        }
        if (_relay == null) return XInputGetState((uint)slot, out st);
        long o = slot * 20;
        st = new XInputState
        {
            PacketNumber = _relay.ReadUInt32(o + 4),
            Buttons = _relay.ReadUInt16(o + 8),
            LeftTrigger = _relay.ReadByte(o + 10),
            RightTrigger = _relay.ReadByte(o + 11),
            ThumbLX = _relay.ReadInt16(o + 12),
            ThumbLY = _relay.ReadInt16(o + 14),
            ThumbRX = _relay.ReadInt16(o + 16),
            ThumbRY = _relay.ReadInt16(o + 18),
        };
        return _relay.ReadUInt32(o);
    }

    // XInput buttons -> Unity GamepadButton bits.
    private static readonly (ushort xinput, int bit)[] ButtonMap =
    {
        (0x0001, 0),  // d-pad up
        (0x0002, 1),  // d-pad down
        (0x0004, 2),  // d-pad left
        (0x0008, 3),  // d-pad right
        (0x8000, 4),  // Y  (north)
        (0x2000, 5),  // B  (east)
        (0x1000, 6),  // A  (south)
        (0x4000, 7),  // X  (west)
        (0x0040, 8),  // left stick click
        (0x0080, 9),  // right stick click
        (0x0100, 10), // LB
        (0x0200, 11), // RB
        (0x0010, 12), // Start
        (0x0020, 13), // Back / Select
    };

    // Sends Unity the full state of the virtual gamepad.
    private static void Send(XInputState st)
    {
        if (Pad == null) return;
        uint buttons = 0;
        foreach (var (x, bit) in ButtonMap)
            if ((st.Buttons & x) != 0) buttons |= 1u << bit;
        const int StickUsed = 12000; // ~0.37: above stick noise
        if (buttons != 0 || st.LeftTrigger > 60 || st.RightTrigger > 60 ||
            Math.Abs((int)st.ThumbLX) > StickUsed || Math.Abs((int)st.ThumbLY) > StickUsed ||
            Math.Abs((int)st.ThumbRX) > StickUsed || Math.Abs((int)st.ThumbRY) > StickUsed)
            LastActive = Time.unscaledTime;

        if (_buffer == IntPtr.Zero) _buffer = Marshal.AllocHGlobal(EventSize);
        var b = (byte*)_buffer;
        *(int*)(b + 0) = EventState;
        *(ushort*)(b + 4) = EventSize;
        *(ushort*)(b + 6) = (ushort)Pad.deviceId;
        *(uint*)(b + 8) = 0;
        *(double*)(b + 12) = InputState.currentTime;
        *(int*)(b + 20) = FormatGamepad;
        *(uint*)(b + 24) = buttons;
        *(float*)(b + 28) = Axis(st.ThumbLX);
        *(float*)(b + 32) = Axis(st.ThumbLY);
        *(float*)(b + 36) = Axis(st.ThumbRX);
        *(float*)(b + 40) = Axis(st.ThumbRY);
        *(float*)(b + 44) = st.LeftTrigger / 255f;
        *(float*)(b + 48) = st.RightTrigger / 255f;
        // Applied immediately (not queued: the queue was losing some of the states).
        InputState.Change(Pad, InputEventPtr.From((InputEvent*)b), default);
        Fed++;
    }

    private static float Axis(short v) => Math.Max(-1f, v / 32767f);

    private static int MakeFourCC(char a, char b, char c, char d) => (a << 24) | (b << 16) | (c << 8) | d;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint userIndex, out XInputState state);
}
