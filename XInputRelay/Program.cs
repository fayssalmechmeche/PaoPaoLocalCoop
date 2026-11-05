using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

// Relais des manettes pour le mod CoopLocal.
// Le jeu bloque la lecture XInput dans sa propre fenetre quand elle n'est pas au premier plan.
// Ce petit programme (sans fenetre) lit les 4 emplacements XInput en continu et publie leur etat
// dans une memoire partagee ("Local\PaoPaoCoopXInput") que les deux fenetres du jeu lisent.
// Il s'arrete tout seul quand plus aucune fenetre du jeu n'est ouverte.
internal static class Program
{
    public const string MapName = "Local\\PaoPaoCoopXInput";
    public const int SlotSize = 20; // code(4) paquet(4) boutons(2) LT(1) RT(1) LX LY RX RY (4x2)

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint userIndex, out XInputState state);
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);

    // Sans option : lanceur (installe si besoin, puis demarre le jeu en double).
    // --relay : relais des manettes (lance par le lanceur, sans fenetre).
    // Les options francaises (--relais, --desinstaller...) sont celles des versions precedentes.
    private static void Main(string[] args)
    {
        if (args.Length == 0) { Launcher.Run(); return; }
        if (args[0] == "--uninstall" || args[0] == "--desinstaller") { Launcher.Uninstall(); return; }
        if (args[0] == "--show-taskbar" || args[0] == "--reafficher-barre") { Taskbar.ForceShow(); return; }
        bool relay = args[0] == "--relay" || args[0] == "--relais";
        if (!relay && args[0] != "--hold" && args[0] != "--simulate") { Launcher.Run(); return; }
        if (args.Length > 1 && args[0] == "--hold")
        {
            // Etapes "masque:lx:ly:rx:ry:ms[:emplacement]" jouees a la suite, puis tout est relache.
            // Sans emplacement, toutes les manettes appuient ; sinon seulement celle-la (0 a 3).
            for (int s = 1; s < args.Length; s++)
            {
                var p = args[s].Split(':');
                Hold(Convert.ToUInt16(p[0], 16), short.Parse(p[1]), short.Parse(p[2]), short.Parse(p[3]), short.Parse(p[4]), int.Parse(p[5]),
                     p.Length > 6 ? int.Parse(p[6]) : -1);
            }
            Hold(0, 0, 0, 0, 0, 300);
            return;
        }
        if (args.Length > 0 && args[0] == "--simulate") { Simulate(args.Length > 1 ? short.Parse(args[1]) : short.MaxValue); return; }
        using var single = new Mutex(true, "Local\\PaoPaoCoopXInputRelay", out bool first);
        if (!first) return; // deja lance
        timeBeginPeriod(1); // Sleep(2) dure vraiment ~2 ms (sinon ~15 ms)

        using var map = MemoryMappedFile.CreateOrOpen(MapName, 4 * SlotSize);
        using var view = map.CreateViewAccessor();

        int j = Math.Max(Array.IndexOf(args, "--game"), Array.IndexOf(args, "--jeu"));
        if (j >= 0 && j + 1 < args.Length) GameNames = new[] { args[j + 1] };
        bool hideTaskbar = Array.IndexOf(args, "--hide-taskbar") >= 0 || Array.IndexOf(args, "--cacher-barre") >= 0;
        if (hideTaskbar) Taskbar.HideWhilePlaying();
        try { Loop(view); }
        finally { Taskbar.Restore(); }
    }

    private static string[] GameNames = { "PaoPao" };
    private static bool GameRunning()
    {
        foreach (var n in GameNames)
            if (Process.GetProcessesByName(n).Length > 0) return true;
        return false;
    }

    private static void Loop(MemoryMappedViewAccessor view)
    {
        var started = Stopwatch.StartNew();
        var lastCheck = Stopwatch.StartNew();
        var lastSeenGame = Stopwatch.StartNew();
        bool sawGame = false;

        while (true)
        {
            for (uint i = 0; i < 4; i++)
            {
                uint code = XInputGetState(i, out var st);
                long o = i * SlotSize;
                view.Write(o + 0, code);
                view.Write(o + 4, st.PacketNumber);
                view.Write(o + 8, st.Buttons);
                view.Write(o + 10, st.LeftTrigger);
                view.Write(o + 11, st.RightTrigger);
                view.Write(o + 12, st.ThumbLX);
                view.Write(o + 14, st.ThumbLY);
                view.Write(o + 16, st.ThumbRX);
                view.Write(o + 18, st.ThumbRY);
            }

            if (lastCheck.ElapsedMilliseconds > 2000)
            {
                lastCheck.Restart();
                if (GameRunning()) { sawGame = true; lastSeenGame.Restart(); Taskbar.Keep(); }
                // Arret : 10 s apres la fermeture du jeu, ou 2 min si le jeu n'a jamais demarre.
                if (sawGame ? lastSeenGame.ElapsedMilliseconds > 10000 : started.ElapsedMilliseconds > 120000) return;
            }
            Thread.Sleep(2);
        }
    }

    // Test sans manette : maintient des boutons (masque XInput en hexa) et les sticks pendant ms,
    // sur tous les emplacements (slot = -1) ou sur un seul (les autres restent branches, au repos).
    private static void Hold(ushort buttons, short lx, short ly, short rx, short ry, int ms, int slot = -1)
    {
        using var map = MemoryMappedFile.CreateOrOpen(MapName, 4 * SlotSize);
        using var view = map.CreateViewAccessor();
        uint packet = (uint)Environment.TickCount;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            for (uint i = 0; i < 4; i++)
            {
                long o = i * SlotSize;
                bool on = slot < 0 || slot == i;
                view.Write(o + 0, 0u);
                view.Write(o + 4, packet);
                view.Write(o + 8, on ? buttons : (ushort)0);
                view.Write(o + 10, (byte)0);
                view.Write(o + 11, (byte)0);
                view.Write(o + 12, on ? lx : (short)0);
                view.Write(o + 14, on ? ly : (short)0);
                view.Write(o + 16, on ? rx : (short)0);
                view.Write(o + 18, on ? ry : (short)0);
            }
            packet++;
            Thread.Sleep(10);
        }
    }

    // Test sans manette : stick gauche pousse (lx) 4 s sur l'emplacement 0, puis 4 s sur le 1, puis repos.
    private static void Simulate(short lx)
    {
        using var map = MemoryMappedFile.CreateOrOpen(MapName, 4 * SlotSize);
        using var view = map.CreateViewAccessor();
        uint packet = 1;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 11000)
        {
            long ms = sw.ElapsedMilliseconds;
            for (uint i = 0; i < 4; i++)
            {
                bool push = (i == 0 && ms < 4000) || (i == 1 && ms >= 4000 && ms < 8000);
                long o = i * SlotSize;
                view.Write(o + 0, 0u);
                view.Write(o + 4, packet);
                // Puis A appuye 3 fois (200 ms) sur les deux emplacements.
                bool pressA = ms >= 8000 && (ms - 8000) % 1000 < 200;
                view.Write(o + 8, pressA ? (ushort)0x1000 : (ushort)0);
                view.Write(o + 12, push ? lx : (short)0);
                view.Write(o + 14, (short)0);
            }
            packet++;
            Thread.Sleep(10);
        }
    }
}
