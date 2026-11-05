using System;
using System.IO;
using System.Runtime.InteropServices;

// Barre des taches cachee pendant la partie, pour qu'elle ne passe plus devant le bas des fenetres
// du jeu (dialogues). Aucun reglage de Windows n'est modifie : la barre est simplement masquee, puis
// reaffichee a la fermeture du jeu. Si le relais a ete coupe brutalement, la barre reste cachee
// jusqu'au prochain lancement du coop (ou redemarrage du PC) : un fichier-temoin le signale.
internal static class Taskbar
{
    // Dossier du .exe lui-meme (et non du dossier d'extraction temporaire d'un exe autonome).
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

    // Windows peut la reafficher (touche Windows, menu Demarrer) : on la recache regulierement.
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

    // Pour remettre la barre si un relais a ete coupe brutalement : le fichier-temoin dit qu'elle est restee cachee.
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
