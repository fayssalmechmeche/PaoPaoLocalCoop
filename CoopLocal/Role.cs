using System;

namespace CoopLocal;

public enum CoopRole { None, Host, Client }

// Lit les options de lancement ajoutees par le lanceur :
//   --coop-local --coop-fenetre 1|2 : fenetre de coop locale ; chaque joueur choisit dans le menu
//                                     (Coop locale > Heberger / Rejoindre).
//   --coop-host / --coop-join       : demarrage automatique : la fenetre 1 heberge le dernier monde joue,
//                                     la fenetre 2 la rejoint.
// Sans ces options (lancement normal depuis Steam), le mod ne fait rien.
public static class Role
{
    public static bool Active { get; private set; }
    public static bool AutoStart { get; private set; }

    // 1 = fenetre de gauche (1re manette, plus clavier/souris), 2 = fenetre de droite (2e manette).
    public static int Window { get; private set; }
    public static bool IsFirstWindow => Window == 1;

    // Role reseau : impose en demarrage automatique, sinon choisi dans le menu.
    public static CoopRole Current { get; set; } = CoopRole.None;

    public static void Detect()
    {
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--coop-host") >= 0) { Active = AutoStart = true; Window = 1; Current = CoopRole.Host; }
        else if (Array.IndexOf(args, "--coop-join") >= 0) { Active = AutoStart = true; Window = 2; Current = CoopRole.Client; }
        else if (Array.IndexOf(args, "--coop-local") >= 0)
        {
            Active = true;
            int i = Array.IndexOf(args, "--coop-fenetre");
            Window = i >= 0 && i + 1 < args.Length && args[i + 1] == "2" ? 2 : 1;
        }
    }
}
