using System;
using System.Text;
using PawPaw.Game.Services.Network.Modules.Session;
using PawPaw.Game.Units.Controllers.Player;
using Unity.Netcode;

namespace CoopLocal;

// Etape A (diagnostic) : F10 = ajoute le membre fictif "Joueur 2" (le jeu cree alors son joueur).
// Puis 3 rapports d'etat a +2s, +6s, +12s.
public static class Coop
{
    private static bool _added;
    private static float _since = -1f;
    private static int _reports;
    private static readonly float[] Delays = { 2f, 6f, 12f };

    public static void SpawnPlayer2()
    {
        var log = Plugin.Instance.Log;
        var session = UnityEngine.Object.FindObjectOfType<NetworkSessionInstance>();
        if (session == null) { log.LogWarning("F10: pas de NetworkSessionInstance (es-tu en partie ?)"); return; }
        if (_added) { log.LogInfo("F10: membre deja ajoute, rapport immediat"); Report(); return; }
        _added = true;
        var member = new SessionMemberData(1UL, "Joueur 2", (sbyte)1, default, "local-player-2");
        session.AddMember(member);
        log.LogInfo($"F10: membre ajoute, PlayerCount={session.PlayerCount}");
        _since = 0f; _reports = 0;
    }

    public static void Tick(float dt)
    {
        if (_since < 0f || _reports >= Delays.Length) return;
        _since += dt;
        if (_since >= Delays[_reports]) { _reports++; if (_reports == 1) CoopWarp.AttachPuppet(); if (_reports == 2) ProbeHelper.DumpCharacters(); Report(); }
    }

    private static void Report()
    {
        var log = Plugin.Instance.Log;
        try
        {
            var nm = NetworkManager.Singleton;
            foreach (var np in UnityEngine.Object.FindObjectsOfType<NetworkPlayer>())
                log.LogInfo($"RAPPORT NetworkPlayer '{np.name}': spawned={np.IsSpawned} owner={np.OwnerClientId} ready={np.IsReady} hasMember={np.HasMemberData} character={(np.Character != null ? "OUI" : "non")} camera={(np.CharacterCamera != null ? "OUI" : "non")}");
            foreach (var o in UnityEngine.Object.FindObjectsOfType<NetworkObject>())
                if (o.name.StartsWith("PlayerCharacter"))
                    log.LogInfo($"RAPPORT PlayerCharacter owner={o.OwnerClientId} spawned={o.IsSpawned} active={o.gameObject.activeInHierarchy} pos={o.transform.position}");
        }
        catch (Exception e) { log.LogError("RAPPORT a echoue : " + e); }
    }
}

public static class CoopWarp
{
    // Place le personnage du Joueur 2 a cote de celui du joueur local.
    public static void AttachPuppet()
    {
        var log = Plugin.Instance.Log;
        try
        {
            var host = NetworkPlayer.LocalInstance;
            NetworkPlayer p2 = null;
            foreach (var np in UnityEngine.Object.FindObjectsOfType<NetworkPlayer>())
                if (np.OwnerClientId == 1UL) p2 = np;
            if (host == null || p2 == null || host.Character == null || p2.Character == null) { log.LogWarning("PUPPET: introuvable"); return; }
            Puppet.Attach(p2.Character.transform, host.Character.transform);
        }
        catch (System.Exception e) { log.LogError("PUPPET attache a echoue : " + e); }
    }

    public static void WarpPlayer2NearHost()
    {
        var log = Plugin.Instance.Log;
        try
        {
            var host = NetworkPlayer.LocalInstance;
            NetworkPlayer p2 = null;
            foreach (var np in UnityEngine.Object.FindObjectsOfType<NetworkPlayer>())
                if (np.OwnerClientId == 1UL) p2 = np;
            if (host == null || p2 == null || host.Character == null || p2.Character == null)
            { log.LogWarning("WARP: host/joueur2/personnage introuvable"); return; }
            var pos = host.Character.transform.position + host.Character.transform.right * 1.5f;
            log.LogInfo($"WARP: joueur 2 -> {pos}");
            var rot = host.Character.transform.rotation;
            var c2 = p2.Character;
            try { c2.ToggleSimulation(true); log.LogInfo("WARP: Character.ToggleSimulation(true) ok"); } catch (System.Exception e0) { log.LogWarning("WARP: ToggleSimulation(Character) : " + e0.Message); }
            try { c2.Movement.ToggleSimulation(true); log.LogInfo("WARP: Movement.ToggleSimulation(true) ok"); } catch (System.Exception e0) { log.LogWarning("WARP: ToggleSimulation(Movement) : " + e0.Message); }
            try { c2.Movement.Motor.SetPositionAndRotation(pos, rot, true); log.LogInfo("WARP: Motor.SetPositionAndRotation ok"); } catch (System.Exception e1) { log.LogWarning("WARP: Motor : " + e1.Message); }
            try { c2.Warp(pos, new Il2CppSystem.Nullable<UnityEngine.Quaternion>(rot)); log.LogInfo("WARP: Warp() ok"); } catch (System.Exception e2) { log.LogWarning("WARP: Warp : " + e2.Message); }
            log.LogInfo($"WARP: position apres = {c2.transform.position}");
        }
        catch (System.Exception e) { log.LogError("WARP a echoue : " + e); }
    }
}

public static class CoopPuppetAttach { }
