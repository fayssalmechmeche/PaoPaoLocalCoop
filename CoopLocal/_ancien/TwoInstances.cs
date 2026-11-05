using System;
using Il2CppInterop.Runtime;
using PawPaw.Debug.Multiplayer;
using PawPaw.Game.Services.Platform;
using Unity.Netcode;

namespace CoopLocal;

// Essai "deux fenetres" : F7 = cette fenetre devient l'hote, F8 = cette fenetre rejoint l'hote.
// Utilise l'outil de test multijoueur des developpeurs (FastStart, connexion directe).
public static class TwoInstances
{
    public static void Host()
    {
        var log = Plugin.Instance.Log;
        try
        {
            log.LogInfo($"F7: FastStart WORLD_NAME={FastStart.WORLD_NAME} ClientIndex={FastStart.ClientIndex}");
            FastStart.StartServer();
            log.LogInfo("F7: FastStart.StartServer() appele");
            LogTransport();
        }
        catch (Exception e) { log.LogError("F7 a echoue : " + e); }
    }

    public static void Join()
    {
        var log = Plugin.Instance.Log;
        try
        {
            PlatformDebug.UserIdOverride = "local-coop-player-2";
            log.LogInfo($"F8: UserIdOverride={PlatformDebug.UserIdOverride} ClientIndex={FastStart.ClientIndex}");
            FastStart.StartClient();
            log.LogInfo("F8: FastStart.StartClient() appele");
            LogTransport();
        }
        catch (Exception e) { log.LogError("F8 a echoue : " + e); }
    }

    private static void LogTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { Plugin.Instance.Log.LogInfo("  NetworkManager absent"); return; }
        var tr = nm.NetworkConfig?.NetworkTransport;
        Plugin.Instance.Log.LogInfo($"  transport={(tr != null ? tr.GetIl2CppType().FullName : "null")} IsHost={nm.IsHost} IsClient={nm.IsClient}");
    }
}

// Declenchement automatique via les options de lancement (pour les tests) : --coop-host / --coop-join
public static class AutoStart
{
    private static float _t;
    private static bool _done;

    public static void Tick(float dt)
    {
        if (_done) return;
        _t += dt;
        var args = Environment.GetCommandLineArgs();
        bool host = Array.IndexOf(args, "--coop-host") >= 0;
        bool join = Array.IndexOf(args, "--coop-join") >= 0;
        if (!host && !join) { _done = true; return; }
        if (host && _t > 25f) { _done = true; TwoInstances.Host(); }
        if (join && _t > 45f) { _done = true; TwoInstances.Join(); }
    }
}
