using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CoopLocal;

// Manette virtuelle de cette fenetre.
// Unity ne lit plus les manettes Xbox d'une fenetre qui n'est pas au premier plan, et ses propres
// lectures (figees) ecrasent ce qu'on lui transmet. On cree donc dans chaque fenetre une manette
// virtuelle qui n'appartient qu'a ce joueur, et on la remplit avec l'etat publie par le relais
// XInputRelay.exe (lance par le lanceur). Les vraies manettes sont coupees dans les deux fenetres.
public static unsafe class XInputFeed
{
    // Etat d'une manette standard de Unity (GamepadState, format 'GPAD', 28 octets) :
    // boutons (4) + stick gauche (8) + stick droit (8) + gachette gauche (4) + gachette droite (4).
    private static readonly int FormatGamepad = MakeFourCC('G', 'P', 'A', 'D');
    private static readonly int EventState = MakeFourCC('S', 'T', 'A', 'T');
    private const int StateSize = 28;
    private const int EventSize = 24 + StateSize; // en-tete InputEvent (20) + format (4) + etat

    private static IntPtr _buffer = IntPtr.Zero;
    private static bool _disabled;
    private static uint _lastPacket = uint.MaxValue;
    private static float _keepAlive;

    public static Gamepad Pad;   // manette virtuelle de cette fenetre
    public static int Slot = -1; // emplacement XInput (0 a 3) lu pour ce joueur
    public static int Fed;       // nombre d'evenements transmis (pour le releve d'etat)
    public static float LastActive = -100f; // dernier moment ou un bouton ou un stick a ete utilise

    // Diagnostic : derniere lecture XInput brute.
    public static uint LastCode = uint.MaxValue, LastPacket;
    public static short LastLX;

    // Appele par Unity au debut de chaque mise a jour des commandes, avant les scripts du jeu :
    // un appui applique ici est vu par le jeu comme "appuye a cette image" (sinon A etait rate).
    private static bool _hooked;
    private static float _lastTick;
    public static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        InputSystem.add_onBeforeUpdate((Il2CppSystem.Action)(Action)OnBeforeUpdate);
        Plugin.Instance.Log.LogInfo("Manette virtuelle : branchee au debut de chaque image");
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
                Pad = InputSystem.AddDevice("Gamepad", "CoopLocal Manette").TryCast<Gamepad>();
                Plugin.Instance.Log.LogInfo($"Manette virtuelle creee (id={Pad?.deviceId})");
                if (Pad == null) { _disabled = true; return; }
            }
            ChooseSlot();
            if (Slot < 0) return;

            LastCode = ReadState(Slot, out var st);
            LastPacket = st.PacketNumber;
            LastLX = st.ThumbLX;
            if (LastCode != 0) return; // manette debranchee

            _keepAlive += dt;
            if (st.PacketNumber == _lastPacket && _keepAlive < 0.5f) return; // rien n'a change
            _lastPacket = st.PacketNumber;
            _keepAlive = 0f;
            Send(st);
        }
        catch (Exception e)
        {
            _disabled = true;
            Plugin.Instance.Log.LogError("Manette virtuelle : desactivee suite a une erreur : " + e);
        }
    }

    // Fenetre 1 (gauche) = 1re manette branchee, fenetre 2 = 2e (ou la seule s'il n'y en a qu'une : la fenetre 1 garde alors clavier/souris).
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
            Plugin.Instance.Log.LogInfo(slot >= 0 ? $"Manette virtuelle : lit la manette XInput n°{slot}" : "Manette virtuelle : aucune manette pour ce joueur");
            if (slot < 0) Send(default); // relache tout
        }
    }

    // Lecture d'une manette. Dans le processus du jeu, XInput reste fige des que la fenetre a perdu
    // le focus une fois : on lit donc l'etat publie par le relais. Sans relais, lecture directe.
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
                // On garde aussi le fichier ouvert : sinon son nom disparait et un relais relance ne le retrouverait pas.
                _relayMap = MemoryMappedFile.OpenExisting("Local\\PaoPaoCoopXInput");
                _relay = _relayMap.CreateViewAccessor();
                Plugin.Instance.Log.LogInfo("Manette virtuelle : relais XInputRelay connecte");
            }
            catch
            {
                if (!_relayLogged) { _relayLogged = true; Plugin.Instance.Log.LogWarning("Manette virtuelle : relais XInputRelay introuvable, lecture directe"); }
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

    // Boutons XInput -> bits de GamepadButton de Unity.
    private static readonly (ushort xinput, int bit)[] ButtonMap =
    {
        (0x0001, 0),  // croix haut
        (0x0002, 1),  // croix bas
        (0x0004, 2),  // croix gauche
        (0x0008, 3),  // croix droite
        (0x8000, 4),  // Y  (nord)
        (0x2000, 5),  // B  (est)
        (0x1000, 6),  // A  (sud)
        (0x4000, 7),  // X  (ouest)
        (0x0040, 8),  // clic stick gauche
        (0x0080, 9),  // clic stick droit
        (0x0100, 10), // LB
        (0x0200, 11), // RB
        (0x0010, 12), // Start
        (0x0020, 13), // Back / Select
    };

    // Transmet a Unity l'etat complet de la manette virtuelle.
    private static void Send(XInputState st)
    {
        if (Pad == null) return;
        uint buttons = 0;
        foreach (var (x, bit) in ButtonMap)
            if ((st.Buttons & x) != 0) buttons |= 1u << bit;
        const int StickUsed = 12000; // ~0.37 : au-dela du bruit des sticks
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
        // Application immediate (et non mise en file d'attente : la file perdait une partie des etats).
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
