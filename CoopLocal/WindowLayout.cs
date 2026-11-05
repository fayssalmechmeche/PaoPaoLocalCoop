using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CoopLocal;

// Ecran partage : fenetre sans bordure, fenetre 1 sur la moitie gauche, fenetre 2 sur la moitie droite.
// Le son de la fenetre 2 est coupe pour ne pas entendre tout en double.
public static class WindowLayout
{
    private const float FirstApply = 3f;
    private const float CheckEvery = 3f;

    private static float _t;
    private static float _check;
    private static bool _resolutionSet;
    private static IntPtr _hwnd = IntPtr.Zero;
    private static int _moves;

    public static void Tick(float dt)
    {
        _t += dt;
        if (!Role.IsFirstWindow) AudioListener.volume = 0f;
        if (_t < FirstApply) return;
        _check += dt;
        if (_resolutionSet && _check < CheckEvery) return;
        _check = 0f;
        try { Apply(); }
        catch (Exception e) { Plugin.Instance.Log.LogError("Fenetre : " + e.Message); }
    }

    private static void Apply()
    {
        // Option du lanceur : barre des taches visible -> les fenetres s'arretent au-dessus d'elle.
        int originX = 0, originY = 0;
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);
        if (UseWorkArea && SystemParametersInfo(SPI_GETWORKAREA, 0, out var wa, 0))
        {
            originX = wa.Left; originY = wa.Top;
            screenW = wa.Right - wa.Left; screenH = wa.Bottom - wa.Top;
        }
        int w = screenW / 2, h = screenH;
        int x = originX + (Role.IsFirstWindow ? 0 : w);
        int y = originY;

        // Le jeu peut remettre sa propre resolution (reglages sauvegardes) : on la reimpose si besoin.
        if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != w || Screen.height != h)
        {
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            _resolutionSet = true;
            return; // on place la fenetre au prochain passage, une fois la resolution appliquee
        }
        _resolutionSet = true;

        if (_hwnd == IntPtr.Zero) _hwnd = FindOwnWindow();
        if (_hwnd == IntPtr.Zero) return;

        // La fenetre est deja sans bordure grace a l'option de lancement -popupwindow : on la place seulement.
        GetWindowRect(_hwnd, out var r);
        if (r.Left == x && r.Top == y && r.Right - r.Left == w && r.Bottom - r.Top == h) return;
        SetWindowPos(_hwnd, IntPtr.Zero, x, y, w, h, SWP_SHOWWINDOW | SWP_NOZORDER);
        if (_moves++ < 5)
            Plugin.Instance.Log.LogInfo($"Fenetre placee : x={x} y={y} largeur={w} hauteur={h} (avant : {r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top})");
    }

    // Vrai si la fenetre de ce jeu est celle au premier plan dans Windows.
    public static bool IsForeground()
    {
        if (_hwnd == IntPtr.Zero) _hwnd = FindOwnWindow();
        return _hwnd != IntPtr.Zero && GetForegroundWindow() == _hwnd;
    }

    // Trouve la fenetre Unity de ce processus.
    private static IntPtr FindOwnWindow()
    {
        uint pid = (uint)Process.GetCurrentProcess().Id;
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint p);
            if (p != pid || !IsWindowVisible(hwnd)) return true;
            var cls = new System.Text.StringBuilder(64);
            GetClassName(hwnd, cls, cls.Capacity);
            if (cls.ToString() != "UnityWndClass") return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static readonly bool UseWorkArea = Array.IndexOf(Environment.GetCommandLineArgs(), "--coop-workarea") >= 0;

    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1, SPI_GETWORKAREA = 0x0030;
    private const uint SWP_NOZORDER = 0x0004, SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(int action, int param, out RECT rect, int flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder s, int max);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
}
