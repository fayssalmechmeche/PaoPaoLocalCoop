using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CoopLocal;

// Point d'entree du mod. Le lanceur ouvre le jeu deux fois (voir Role) :
//   fenetre 1 a gauche, fenetre 2 a droite ; chaque joueur choisit "Coop locale" dans le menu,
//   ou bien (demarrage automatique) la fenetre 1 heberge et la fenetre 2 rejoint d'elle-meme.
// Sans ces options (lancement normal depuis Steam), le mod ne fait rien.
[BepInPlugin("local.paopao.cooplocal", "CoopLocal", "0.3.0")]
public class Plugin : BasePlugin
{
    public static Plugin Instance;

    public override void Load()
    {
        Instance = this;
        Role.Detect();
        Log.LogInfo(Role.Active
            ? $"CoopLocal 0.3.0 charge. Fenetre {Role.Window}, {(Role.AutoStart ? "demarrage automatique (" + Role.Current + ")" : "choix dans le menu")}"
            : "CoopLocal 0.3.0 charge. Lancement normal : le mod ne fait rien.");
        if (!Role.Active) return;

        FocusPatch.Apply();
        if (!Role.AutoStart) LocalMenu.Apply();
        ClassInjector.RegisterTypeInIl2Cpp<Runner>();
        var go = new GameObject("CoopLocalRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }
}

// Composant Unity qui fait tourner le mod a chaque image.
// Il ne contient que Update() : toute la logique est dans des classes statiques.
public class Runner : MonoBehaviour
{
    public Runner(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Session.Tick(dt);
        if (!Role.AutoStart) LocalMenu.Tick(dt);
        InputAssign.Tick(dt);
        XInputFeed.Hook();
        WindowLayout.Tick(dt);
        ProcessPriority.Tick(dt);
    }
}
