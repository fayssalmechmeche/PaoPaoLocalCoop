using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CoopLocal;

// Split screen: borderless window, window 1 on the left half, window 2 on the right half.
// Window 2 is muted so that everything is not heard twice.
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
        catch (Exception e) { Plugin.Instance.Log.LogError("Window: " + e.Message); }
    }

    private static void Apply()
    {
        // Launcher option: visible taskbar -> windows stop above it.
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

        // The game may restore its own resolution (saved settings): re-apply ours if needed.
        if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != w || Screen.height != h)
        {
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            _resolutionSet = true;
            return; // the window is placed on the next pass, once the resolution is applied
        }
        _resolutionSet = true;

        if (_hwnd == IntPtr.Zero) _hwnd = FindOwnWindow();
        if (_hwnd == IntPtr.Zero) return;

        // The window is already borderless thanks to the -popupwindow launch option: only position it.
        GetWindowRect(_hwnd, out var r);
        if (r.Left == x && r.Top == y && r.Right - r.Left == w && r.Bottom - r.Top == h) return;
        SetWindowPos(_hwnd, IntPtr.Zero, x, y, w, h, SWP_SHOWWINDOW | SWP_NOZORDER);
        if (_moves++ < 5)
            Plugin.Instance.Log.LogInfo($"Window placed: x={x} y={y} width={w} height={h} (before: {r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top})");
    }

    // True if this game's window is the foreground window in Windows.
    public static bool IsForeground()
    {
        if (_hwnd == IntPtr.Zero) _hwnd = FindOwnWindow();
        return _hwnd != IntPtr.Zero && GetForegroundWindow() == _hwnd;
    }

    // Finds this process's Unity window.
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
