using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CoopLocal;

// Mod entry point. The launcher opens the game twice (see Role):
//   window 1 on the left, window 2 on the right; each player picks "Local co-op" in the menu,
//   or (automatic start) window 1 hosts and window 2 joins on its own.
// Without these options (normal launch from Steam), the mod does nothing.
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

// Unity component that runs the mod every frame.
// It only contains Update(): all the logic is in static classes.
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
