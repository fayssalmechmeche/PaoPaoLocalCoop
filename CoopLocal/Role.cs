using System;

namespace CoopLocal;

public enum CoopRole { None, Host, Client }

// Reads the launch options added by the launcher:
//   --coop-local --coop-window 1|2 : local co-op window; each player chooses in the menu
//                                     (Local co-op > Host / Join).
//   --coop-host / --coop-join       : automatic start: window 1 hosts the last played world,
//                                     window 2 joins it.
// Without these options (normal launch from Steam), the mod does nothing.
public static class Role
{
    public static bool Active { get; private set; }
    public static bool AutoStart { get; private set; }

    // 1 = left window (1st controller, plus keyboard/mouse), 2 = right window (2nd controller).
    public static int Window { get; private set; }
    public static bool IsFirstWindow => Window == 1;

    // Network role: forced on automatic start, otherwise chosen in the menu.
    public static CoopRole Current { get; set; } = CoopRole.None;

    public static void Detect()
    {
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--coop-host") >= 0) { Active = AutoStart = true; Window = 1; Current = CoopRole.Host; }
        else if (Array.IndexOf(args, "--coop-join") >= 0) { Active = AutoStart = true; Window = 2; Current = CoopRole.Client; }
        else if (Array.IndexOf(args, "--coop-local") >= 0)
        {
            Active = true;
            int i = Array.IndexOf(args, "--coop-window");
            Window = i >= 0 && i + 1 < args.Length && args[i + 1] == "2" ? 2 : 1;
        }
    }
}
