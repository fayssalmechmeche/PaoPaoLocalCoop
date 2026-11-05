using System;
using System.IO;
using System.Runtime.InteropServices;

// Taskbar hidden during the game, so that it no longer covers the bottom of the game windows
// (dialogs). No Windows setting is changed: the taskbar is simply hidden, then
// shown again when the game closes. If the relay was killed abruptly, the taskbar stays hidden
// until the next co-op launch (or PC restart): a marker file records this.
internal static class Taskbar
{
    // Folder of the .exe itself (not the temporary extraction folder of a self-contained exe).
    private static readonly string Marker = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "taskbar_hidden.txt");
    private static readonly string[] Classes = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
    private static bool _hidden;

    public static void HideWhilePlaying()
    {
        try
        {
            File.WriteAllText(Marker, "The taskbar is hidden by the PAO PAO Local Co-op relay. It comes back when the game closes.");
            _hidden = true;
            Keep();
        }
        catch { }
    }

    // Windows may show it again (Windows key, Start menu): hide it again regularly.
    public static void Keep()
    {
        if (_hidden) ShowAll(SW_HIDE);
    }

    public static void Restore()
    {
        if (!_hidden) return;
        _hidden = false;
        try
        {
            ShowAll(SW_SHOW);
            File.Delete(Marker);
        }
        catch { }
    }

    // To restore the taskbar if a relay was killed abruptly: the marker file says it stayed hidden.
    public static bool WasLeftHidden() => File.Exists(Marker);

    public static void ForceShow()
    {
        try
        {
            ShowAll(SW_SHOW);
            File.Delete(Marker);
        }
        catch { }
    }

    private static void ShowAll(int cmd)
    {
        foreach (var cls in Classes)
        {
            IntPtr w = IntPtr.Zero;
            while ((w = FindWindowEx(IntPtr.Zero, w, cls, null)) != IntPtr.Zero)
                if (IsWindowVisible(w) != (cmd == SW_SHOW)) ShowWindow(w, cmd);
        }
    }

    private const int SW_HIDE = 0, SW_SHOW = 5;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
}
