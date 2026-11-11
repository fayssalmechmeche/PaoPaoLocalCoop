using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

// Local co-op mod launcher: finds the game through Steam, installs the mod if needed
// ("mod_files" folder next to this program), prepares BepInEx on first launch,
// then starts the controller relay and the two game windows.
// Messages are in French if Windows is in French, otherwise in English.
internal static class Launcher
{
    private const string GameFolder = "PaoPao"; // game folder in steamapps\common
    private const string GameProcess = "PaoPao";
    private const string ManifestName = "LocalCoop_install.txt";
    private const string OldManifestName = "CoopLocal_install.txt"; // name before v0.3.0
    private const string Title = "PAO PAO Local Co-op";

    private static readonly string AppDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
    private static readonly string PayloadDir = Path.Combine(AppDir, "mod_files");
    private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaoPaoLocalCoop");
    private static string LogFile => Path.Combine(DataDir, "launcher.log");

    private static bool French => (GetUserDefaultUILanguage() & 0x3FF) == 0x0C;
    private static string T(string fr, string en) => French ? fr : en;

    // ---------------------------------------------------------------- launch

    public static void Run()
    {
        try { RunInner(); }
        catch (Exception e)
        {
            Log(e.ToString());
            Error(T("Une erreur inattendue est survenue :\n\n", "An unexpected error occurred:\n\n") + e.Message +
                  T("\n\nDetails : ", "\n\nDetails: ") + LogFile);
        }
    }

    private static void RunInner()
    {
        var options = Options.Load(AppDir);

        // A relay killed abruptly may have left the taskbar hidden.
        if (Taskbar.WasLeftHidden() && !RelayRunning()) Taskbar.ForceShow();

        var game = FindGame(options.GameDir);
        if (game == null)
        {
            Error(T("Le jeu PAO PAO est introuvable.\n\nVerifie qu'il est installe dans Steam (jeu complet, pas la demo). " +
                    "Sinon, indique son dossier dans le fichier options.ini.",
                    "PAO PAO was not found.\n\nMake sure it is installed in Steam (full game, not the demo). " +
                    "Otherwise, set its folder in options.ini."));
            return;
        }
        Log("Game: " + game.Dir + " (appid " + game.AppId + ")");

        if (Process.GetProcessesByName(GameProcess).Length > 0)
        {
            Error(T("PAO PAO est deja ouvert. Ferme-le puis relance ce programme.",
                    "PAO PAO is already running. Close it, then start this program again."));
            return;
        }

        if (!ChooseLayout(options, false, out var rects)) return;

        var manifestPath = ManifestPath(game);
        var manifest = Manifest.Read(manifestPath);
        if (!Install(game, manifest, manifestPath)) return;

        EnsureSteam();

        string interop = Path.Combine(game.Dir, "BepInEx", "interop", "PawPaw.Game.dll");
        if (!File.Exists(interop) || manifest.GameSig != GameSignature(game))
        {
            if (!Prepare(game)) return;
            manifest.GameSig = GameSignature(game);
            manifest.Write(manifestPath);
        }

        Start(game, options, rects);
    }

    // Split screen or one screen per player. Asks when 2 screens or more are connected (unless the players
    // chose "don't ask again"), or always with force (--screens). rects: null = split screen, otherwise
    // the screen rectangle of each window. Returns false if the players cancelled.
    private static bool ChooseLayout(Options o, bool force, out string[] rects)
    {
        rects = null;
        ScreenSetup.InitUi();
        var screens = ScreenSetup.Ordered();
        if (force || (!Auto && o.AskScreens && screens.Length >= 2))
        {
            var current = new ScreenSetup.Choice { Dual = o.ScreenLayout == "dual", DontAsk = !o.AskScreens, Screen1 = o.Screen1, Screen2 = o.Screen2 };
            var c = ScreenSetup.Ask(screens, current, T, Title);
            if (c == null) { Log("Screen choice cancelled"); return false; }
            o.ScreenLayout = c.Dual ? "dual" : "split";
            o.Screen1 = c.Screen1;
            o.Screen2 = c.Screen2;
            o.AskScreens = !c.DontAsk;
            try
            {
                Options.Save(AppDir, ("screen_layout", o.ScreenLayout), ("player1_screen", o.Screen1),
                             ("player2_screen", o.Screen2), ("ask_screens", o.AskScreens ? "yes" : "no"));
            }
            catch (Exception e) { Log("options.ini not saved: " + e.Message); }
        }

        if (o.ScreenLayout != "dual") { Log("Screens: split screen"); return true; }
        var s1 = ScreenSetup.Find(screens, o.Screen1);
        var s2 = ScreenSetup.Find(screens, o.Screen2);
        if (s1 == null || s2 == null || s1.DeviceName == s2.DeviceName)
        {
            Log("Screens: one screen per player requested but screens not found (" + o.Screen1 + ", " + o.Screen2 + "): split screen");
            return true;
        }
        // Whole screen, not the work area: above the taskbar the window is wider than the game's maximum
        // aspect ratio and the game adds black bars on the sides. The taskbars are hidden while playing (see Start).
        rects = new[] { ScreenSetup.Rect(s1, false), ScreenSetup.Rect(s2, false) };
        Log("Screens: player 1 on " + o.Screen1 + " (" + rects[0] + "), player 2 on " + o.Screen2 + " (" + rects[1] + ")");
        return true;
    }

    public static void ChooseScreens()
    {
        try
        {
            var options = Options.Load(AppDir);
            if (ChooseLayout(options, true, out _))
                Info(T("Choix enregistre. Il sera utilise au prochain lancement.", "Choice saved. It will be used at the next start."));
        }
        catch (Exception e) { Log(e.ToString()); Error(e.Message); }
    }

    // First launch of the game alone: BepInEx generates its files (1 to 3 minutes). Two windows at the same
    // time would interfere, so this is done before starting co-op. Redone after a game update.
    private static bool Prepare(Game game)
    {
        Info(T("Premiere preparation (1 a 3 minutes).\n\nUne fenetre du jeu va s'ouvrir toute seule puis se refermer. " +
               "Ne touche a rien : le coop demarrera ensuite automatiquement.",
               "First-time setup (1 to 3 minutes).\n\nA game window will open by itself and close again. " +
               "Don't touch anything: co-op will start automatically afterwards."));
        string log = Path.Combine(game.Dir, "BepInEx", "LogOutput.log");
        var started = DateTime.Now.AddSeconds(-2);
        var p = Process.Start(new ProcessStartInfo(game.Exe, "-popupwindow") { WorkingDirectory = game.Dir, UseShellExecute = false });
        var limit = DateTime.Now.AddMinutes(10);
        bool done = false;
        while (DateTime.Now < limit && !p.HasExited)
        {
            Thread.Sleep(1000);
            if (!File.Exists(log) || File.GetLastWriteTime(log) < started) continue;
            if (ReadShared(log).Contains("Chainloader startup complete")) { done = true; break; }
        }
        if (done) Thread.Sleep(6000); // lets the game finish starting before closing it
        try { p.Kill(true); } catch { }
        foreach (var o in Process.GetProcessesByName(GameProcess)) { try { o.Kill(true); } catch { } }
        Thread.Sleep(2000);
        if (!done)
        {
            Error(T("La preparation n'a pas abouti. Regarde le fichier BepInEx\\LogOutput.log dans le dossier du jeu.",
                    "Setup did not complete. See BepInEx\\LogOutput.log in the game folder."));
            return false;
        }
        return true;
    }

    private static void Start(Game game, Options options, string[] rects)
    {
        string logs = Path.Combine(DataDir, "logs");
        Directory.CreateDirectory(logs);

        // Controller relay (windowless, stops by itself after the game closes).
        var relay = new ProcessStartInfo(Environment.ProcessPath, "") { UseShellExecute = false, CreateNoWindow = true };
        relay.ArgumentList.Add("--relay");
        relay.ArgumentList.Add("--game");
        relay.ArgumentList.Add(GameProcess);
        // One screen per player: the windows cover the whole screens, so the taskbars are hidden too.
        if (options.HideTaskbar || rects != null) relay.ArgumentList.Add("--hide-taskbar");
        Process.Start(relay);

        // By default each window opens on the menu (Local co-op > Host / Join).
        // Automatic start: window 1 hosts the last played world, window 2 joins it.
        StartGame(game, options, options.AutoStart ? new[] { "--coop-host" } : new[] { "--coop-local", "--coop-window", "1" },
                  rects?[0], Path.Combine(logs, "coop_player1.log"));
        Thread.Sleep(5000);
        StartGame(game, options, options.AutoStart ? new[] { "--coop-join" } : new[] { "--coop-local", "--coop-window", "2" },
                  rects?[1], Path.Combine(logs, "coop_player2.log"));
    }

    private static void StartGame(Game game, Options options, string[] role, string rect, string logFile)
    {
        var psi = new ProcessStartInfo(game.Exe) { WorkingDirectory = game.Dir, UseShellExecute = false };
        foreach (var a in role) psi.ArgumentList.Add(a);
        // One screen per player: the window fills this screen rectangle.
        if (rect != null) { psi.ArgumentList.Add("--coop-rect"); psi.ArgumentList.Add(rect); }
        psi.ArgumentList.Add("-popupwindow");
        psi.ArgumentList.Add("-logFile");
        psi.ArgumentList.Add(logFile);
        // Visible taskbar: windows stop above it.
        if (!options.HideTaskbar) psi.ArgumentList.Add("--coop-workarea");
        Process.Start(psi);
    }

    // ---------------------------------------------------------------- installation

    private static bool Install(Game game, Manifest old, string manifestPath)
    {
        if (!Directory.Exists(PayloadDir))
        {
            Error(T("Le dossier \"mod_files\" est introuvable a cote de ce programme.\n\nDecompresse tout le contenu du fichier zip avant de lancer.",
                    "The \"mod_files\" folder is missing next to this program.\n\nExtract the whole zip file before starting."));
            return false;
        }
        string version = File.ReadAllText(Path.Combine(PayloadDir, "version.txt")).Trim();
        string plugin = Path.Combine(game.Dir, "BepInEx", "plugins", "CoopLocal.dll");
        string payloadPlugin = Path.Combine(PayloadDir, "BepInEx", "plugins", "CoopLocal.dll");
        bool upToDate = old.Version == version && File.Exists(plugin) &&
                        new FileInfo(plugin).Length == new FileInfo(payloadPlugin).Length;
        if (upToDate) return true;

        bool otherBepInEx = !old.Full && (Directory.Exists(Path.Combine(game.Dir, "BepInEx", "core")) ||
                                          File.Exists(Path.Combine(game.Dir, "winhttp.dll")));
        string question = old.Version == ""
            ? T("Le mod va etre installe dans le dossier du jeu :\n\n" + game.Dir +
                "\n\nDes fichiers sont ajoutes, aucun fichier du jeu n'est modifie. Tes sauvegardes ne sont pas touchees." +
                "\nPour tout enlever plus tard : lance ce programme avec l'option --uninstall.\n\nContinuer ?",
                "The mod will be installed in the game folder:\n\n" + game.Dir +
                "\n\nFiles are added; no game file is modified. Your saves are not touched." +
                "\nTo remove everything later, run this program with the --uninstall option.\n\nContinue?")
            : T("Une nouvelle version du mod va etre installee (" + version + "). Continuer ?",
                "A new version of the mod will be installed (" + version + "). Continue?");
        if (otherBepInEx)
            question += T("\n\nBepInEx est deja present dans ce jeu : seul le plugin sera ajoute (il faut BepInEx 6 pour IL2CPP).",
                          "\n\nBepInEx is already present in this game: only the plugin will be added (BepInEx 6 for IL2CPP is required).");
        if (!Ask(question)) return false;

        bool full = !otherBepInEx;
        if (full) CopyDir(PayloadDir, game.Dir);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plugin));
            File.Copy(payloadPlugin, plugin, true);
        }

        // Required to launch the game outside Steam (two windows).
        string appIdFile = Path.Combine(game.Dir, "steam_appid.txt");
        bool existed = File.Exists(appIdFile);
        if (!existed || File.ReadAllText(appIdFile).Trim() != game.AppId)
        {
            File.WriteAllText(appIdFile, game.AppId);
            if (!existed) old.AppIdCreated = true;
        }

        old.Version = version;
        old.Full = old.Full || full;
        old.Write(manifestPath);
        Log("Installation complete (version " + version + ", full=" + old.Full + ")");
        return true;
    }

    public static void Uninstall()
    {
        try
        {
            var options = Options.Load(AppDir);
            var game = FindGame(options.GameDir);
            var manifestPath = game == null ? null : ManifestPath(game);
            if (game == null || !File.Exists(manifestPath))
            {
                Info(T("Le mod n'est pas installe : rien a desinstaller.", "The mod is not installed: nothing to uninstall."));
                return;
            }
            if (Process.GetProcessesByName(GameProcess).Length > 0)
            {
                Error(T("Ferme PAO PAO avant de desinstaller.", "Close PAO PAO before uninstalling."));
                return;
            }
            if (!Ask(T("Desinstaller le mod du dossier du jeu ?\n\nTes sauvegardes ne sont pas touchees.",
                       "Uninstall the mod from the game folder?\n\nYour saves are not touched."))) return;

            var m = Manifest.Read(manifestPath);
            TryDelete(Path.Combine(game.Dir, "BepInEx", "plugins", "CoopLocal.dll"));
            if (m.Full)
            {
                foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" }) TryDelete(Path.Combine(game.Dir, f));
                TryDeleteDir(Path.Combine(game.Dir, "dotnet"));
                TryDeleteDir(Path.Combine(game.Dir, "BepInEx"));
            }
            if (m.AppIdCreated) TryDelete(Path.Combine(game.Dir, "steam_appid.txt"));
            TryDelete(manifestPath);
            Taskbar.ForceShow();
            Info(T("Mod desinstalle.", "Mod uninstalled."));
        }
        catch (Exception e) { Log(e.ToString()); Error(e.Message); }
    }

    // Installation state file in the game folder (renamed if it still has the old name).
    private static string ManifestPath(Game game)
    {
        string path = Path.Combine(game.Dir, ManifestName), old = Path.Combine(game.Dir, OldManifestName);
        if (!File.Exists(path) && File.Exists(old)) File.Move(old, path);
        return path;
    }

    private static void CopyDir(string from, string to, bool top = true)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
        {
            if (top && Path.GetFileName(f) == "version.txt") continue; // used by the launcher, not the game
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        }
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)), false);
    }

    private static void TryDelete(string path) { try { File.Delete(path); } catch { } }
    private static void TryDeleteDir(string path) { try { Directory.Delete(path, true); } catch { } }

    // ---------------------------------------------------------------- Steam

    private sealed class Game
    {
        public string Dir, Exe, AppId;
    }

    private static string GameSignature(Game g)
    {
        var f = new FileInfo(Path.Combine(g.Dir, "GameAssembly.dll"));
        return f.Exists ? f.Length + "_" + f.LastWriteTimeUtc.Ticks : "";
    }

    private static Game FindGame(string overrideDir)
    {
        foreach (var lib in SteamLibraries())
        {
            string apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
            {
                string text = File.ReadAllText(acf);
                string dir = Field(text, "installdir"), id = Field(text, "appid");
                string wanted = string.IsNullOrEmpty(overrideDir) ? GameFolder : Path.GetFileName(overrideDir.TrimEnd('\\', '/'));
                if (!string.Equals(dir, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                string gdir = string.IsNullOrEmpty(overrideDir) ? Path.Combine(apps, "common", dir) : overrideDir;
                string exe = Path.Combine(gdir, "PaoPao.exe");
                if (File.Exists(exe)) return new Game { Dir = gdir, Exe = exe, AppId = id };
            }
        }
        return null;
    }

    private static List<string> SteamLibraries()
    {
        var libs = new List<string>();
        string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrEmpty(steam)) return libs;
        steam = Path.GetFullPath(steam);
        libs.Add(steam);
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                string p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (!libs.Exists(l => string.Equals(l, p, StringComparison.OrdinalIgnoreCase))) libs.Add(p);
            }
        return libs;
    }

    private static string Field(string text, string key)
    {
        var m = Regex.Match(text, "\"" + key + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "";
    }

    private static void EnsureSteam()
    {
        if (Process.GetProcessesByName("steam").Length > 0) return;
        Process.Start(new ProcessStartInfo("steam://open/main") { UseShellExecute = true });
        for (int i = 0; i < 60 && Process.GetProcessesByName("steam").Length == 0; i++) Thread.Sleep(1000);
        Thread.Sleep(15000); // lets Steam finish connecting
    }

    private static bool RelayRunning()
    {
        return Mutex.TryOpenExisting("Local\\PaoPaoCoopXInputRelay", out var m) && Close(m);
        static bool Close(Mutex x) { x.Dispose(); return true; }
    }

    // ---------------------------------------------------------------- helpers

    private static string ReadShared(string path)
    {
        try
        {
            using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch { return ""; }
    }

    private static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(LogFile, DateTime.Now.ToString("s") + " " + msg + Environment.NewLine);
        }
        catch { }
    }

    // --yes: answers "Yes" to all questions and shows no dialog (silent install, tests).
    private static readonly bool Auto = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--yes");

    private static void Info(string msg) { if (Auto) Log("INFO " + msg); else MessageBoxW(IntPtr.Zero, msg, Title, 0x40); }
    private static void Error(string msg) { if (Auto) Log("ERROR " + msg); else MessageBoxW(IntPtr.Zero, msg, Title, 0x10); }
    private static bool Ask(string msg) { if (Auto) { Log("QUESTION (yes) " + msg); return true; } return MessageBoxW(IntPtr.Zero, msg, Title, 0x24) == 6; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();

    // Installation state, written to the game folder so that everything can be uninstalled cleanly.
    private sealed class Manifest
    {
        public string Version = "", GameSig = "";
        public bool Full, AppIdCreated;

        public static Manifest Read(string path)
        {
            var m = new Manifest();
            if (!File.Exists(path)) return m;
            foreach (var line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('=');
                if (i < 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                bool yes = v == "yes";
                if (k == "version") m.Version = v;
                else if (k == "game") m.GameSig = v;
                else if (k == "full") m.Full = yes;
                else if (k == "appid_created") m.AppIdCreated = yes;
            }
            return m;
        }

        public void Write(string path) => File.WriteAllLines(path, new[]
        {
            "# PAO PAO Local Co-op installation state (do not edit). To uninstall: PaoPaoLocalCoop.exe --uninstall",
            "version=" + Version, "game=" + GameSig, "full=" + (Full ? "yes" : "no"), "appid_created=" + (AppIdCreated ? "yes" : "no"),
        });
    }

    // Settings from options.ini (next to the program).
    private sealed class Options
    {
        public bool HideTaskbar, AutoStart, AskScreens = true;
        public string GameDir = "", ScreenLayout = "split", Screen1 = "", Screen2 = "";

        public static Options Load(string dir)
        {
            var o = new Options();
            string path = Path.Combine(dir, "options.ini");
            if (!File.Exists(path)) return o;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || line.IndexOf('=') < 0) continue;
                string k = line.Substring(0, line.IndexOf('=')).Trim(), v = line.Substring(line.IndexOf('=') + 1).Trim();
                bool yes = v == "yes" || v == "true" || v == "1";
                if (k == "hide_taskbar") o.HideTaskbar = yes;
                else if (k == "auto_start") o.AutoStart = yes;
                else if (k == "game_folder") o.GameDir = v;
                else if (k == "screen_layout") o.ScreenLayout = v.ToLowerInvariant() == "dual" ? "dual" : "split";
                else if (k == "player1_screen") o.Screen1 = v;
                else if (k == "player2_screen") o.Screen2 = v;
                else if (k == "ask_screens") o.AskScreens = !(v == "no" || v == "false" || v == "0");
            }
            return o;
        }

        // Replaces the given keys in options.ini (comments and other lines are kept), or adds them at the end.
        public static void Save(string dir, params (string Key, string Value)[] values)
        {
            string path = Path.Combine(dir, "options.ini");
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            foreach (var (key, value) in values)
            {
                int i = lines.FindIndex(l =>
                {
                    string t = l.Trim();
                    int eq = t.IndexOf('=');
                    return !t.StartsWith("#") && eq > 0 && t.Substring(0, eq).Trim() == key;
                });
                if (i >= 0) lines[i] = key + "=" + value;
                else lines.Add(key + "=" + value);
            }
            File.WriteAllLines(path, lines);
        }
    }
}
