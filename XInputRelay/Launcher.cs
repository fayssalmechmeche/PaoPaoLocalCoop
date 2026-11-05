using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

// Lanceur du mod de coop locale : retrouve le jeu via Steam, installe le mod si besoin
// (dossier "mod_files" a cote de ce programme), prepare BepInEx au premier lancement,
// puis demarre le relais des manettes et les deux fenetres du jeu.
// Les messages sont en francais si Windows est en francais, en anglais sinon.
internal static class Launcher
{
    private const string GameFolder = "PaoPao"; // dossier du jeu dans steamapps\common
    private const string GameProcess = "PaoPao";
    private const string ManifestName = "LocalCoop_install.txt";
    private const string OldManifestName = "CoopLocal_install.txt"; // nom avant la v0.3.0
    private const string Title = "PAO PAO Local Co-op";

    private static readonly string AppDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
    private static readonly string PayloadDir = Path.Combine(AppDir, "mod_files");
    private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaoPaoLocalCoop");
    private static string LogFile => Path.Combine(DataDir, "launcher.log");

    private static bool French => (GetUserDefaultUILanguage() & 0x3FF) == 0x0C;
    private static string T(string fr, string en) => French ? fr : en;

    // ---------------------------------------------------------------- lancement

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

        // Un relais coupe brutalement peut avoir laisse la barre des taches cachee.
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
        Log("Jeu : " + game.Dir + " (appid " + game.AppId + ")");

        if (Process.GetProcessesByName(GameProcess).Length > 0)
        {
            Error(T("PAO PAO est deja ouvert. Ferme-le puis relance ce programme.",
                    "PAO PAO is already running. Close it, then start this program again."));
            return;
        }

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

        Start(game, options);
    }

    // Premier lancement du jeu seul : BepInEx genere ses fichiers (1 a 3 minutes). Deux fenetres en meme
    // temps se gêneraient, donc on le fait avant de lancer le coop. Refait apres une mise a jour du jeu.
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
        if (done) Thread.Sleep(6000); // laisse le jeu finir de demarrer avant de le fermer
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

    private static void Start(Game game, Options options)
    {
        string logs = Path.Combine(DataDir, "logs");
        Directory.CreateDirectory(logs);

        // Relais des manettes (sans fenetre, s'arrete seul apres la fermeture du jeu).
        var relay = new ProcessStartInfo(Environment.ProcessPath, "") { UseShellExecute = false, CreateNoWindow = true };
        relay.ArgumentList.Add("--relay");
        relay.ArgumentList.Add("--game");
        relay.ArgumentList.Add(GameProcess);
        if (options.HideTaskbar) relay.ArgumentList.Add("--hide-taskbar");
        Process.Start(relay);

        // Par defaut chaque fenetre s'ouvre sur le menu (Coop locale > Heberger / Rejoindre).
        // Demarrage automatique : la fenetre 1 heberge le dernier monde joue, la 2 la rejoint.
        StartGame(game, options, options.AutoStart ? new[] { "--coop-host" } : new[] { "--coop-local", "--coop-fenetre", "1" },
                  Path.Combine(logs, "coop_joueur1.log"));
        Thread.Sleep(5000);
        StartGame(game, options, options.AutoStart ? new[] { "--coop-join" } : new[] { "--coop-local", "--coop-fenetre", "2" },
                  Path.Combine(logs, "coop_joueur2.log"));
    }

    private static void StartGame(Game game, Options options, string[] role, string logFile)
    {
        var psi = new ProcessStartInfo(game.Exe) { WorkingDirectory = game.Dir, UseShellExecute = false };
        foreach (var a in role) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("-popupwindow");
        psi.ArgumentList.Add("-logFile");
        psi.ArgumentList.Add(logFile);
        // Barre des taches visible : les fenetres s'arretent au-dessus d'elle.
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

        // Necessaire pour lancer le jeu hors de Steam (deux fenetres).
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
        Log("Installation terminee (version " + version + ", complete=" + old.Full + ")");
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

    // Fichier d'etat de l'installation dans le dossier du jeu (renomme s'il porte encore l'ancien nom).
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
            if (top && Path.GetFileName(f) == "version.txt") continue; // sert au lanceur, pas au jeu
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
        Thread.Sleep(15000); // laisse Steam finir de se connecter
    }

    private static bool RelayRunning()
    {
        return Mutex.TryOpenExisting("Local\\PaoPaoCoopXInputRelay", out var m) && Close(m);
        static bool Close(Mutex x) { x.Dispose(); return true; }
    }

    // ---------------------------------------------------------------- petits outils

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

    // --yes (ou --oui) : repond "Oui" a toutes les questions et n'affiche aucune boite (installation silencieuse, tests).
    private static readonly bool Auto = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--yes" || a == "--oui");

    private static void Info(string msg) { if (Auto) Log("INFO " + msg); else MessageBoxW(IntPtr.Zero, msg, Title, 0x40); }
    private static void Error(string msg) { if (Auto) Log("ERREUR " + msg); else MessageBoxW(IntPtr.Zero, msg, Title, 0x10); }
    private static bool Ask(string msg) { if (Auto) { Log("QUESTION (oui) " + msg); return true; } return MessageBoxW(IntPtr.Zero, msg, Title, 0x24) == 6; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();

    // Etat de l'installation, ecrit dans le dossier du jeu pour pouvoir tout desinstaller proprement.
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
                bool yes = v == "yes" || v == "oui";
                // Les cles francaises sont celles des versions precedentes.
                if (k == "version") m.Version = v;
                else if (k == "game" || k == "jeu") m.GameSig = v;
                else if (k == "full" || k == "complet") m.Full = yes;
                else if (k == "appid_created" || k == "appid_cree") m.AppIdCreated = yes;
            }
            return m;
        }

        public void Write(string path) => File.WriteAllLines(path, new[]
        {
            "# PAO PAO Local Co-op installation state (do not edit). To uninstall: PaoPaoLocalCoop.exe --uninstall",
            "version=" + Version, "game=" + GameSig, "full=" + (Full ? "yes" : "no"), "appid_created=" + (AppIdCreated ? "yes" : "no"),
        });
    }

    // Reglages de options.ini (a cote du programme).
    private sealed class Options
    {
        public bool HideTaskbar, AutoStart;
        public string GameDir = "";

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
                bool yes = v == "oui" || v == "yes" || v == "true" || v == "1";
                // Les cles francaises sont celles des versions precedentes.
                if (k == "hide_taskbar" || k == "cacher_barre_des_taches") o.HideTaskbar = yes;
                else if (k == "auto_start" || k == "demarrage_auto") o.AutoStart = yes;
                else if (k == "game_folder" || k == "dossier_du_jeu") o.GameDir = v;
            }
            return o;
        }
    }
}
