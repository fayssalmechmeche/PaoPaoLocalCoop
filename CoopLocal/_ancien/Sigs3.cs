using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoopLocal;

// Dump detaille de types precis, cherches dans plusieurs assemblages.
public static class Sigs3
{
    public static void Dump()
    {
        string interop = Path.Combine(BepInEx.Paths.BepInExRootPath, "interop");
        var asms = new[] { "PawPaw.Game.dll", "PawPaw.Debug.dll", "PawPaw.Platform.Base.dll", "PawPaw.Platform.Steamworks.dll" }
            .Select(d => { try { return Assembly.LoadFrom(Path.Combine(interop, d)); } catch { return null; } }).Where(a => a != null).ToArray();
        string[] names = {
            "PawPaw.Debug.Multiplayer.FastStart",
            "PawPaw.Game.Services.Save.Data.WorldCollectionSaveData",
            "PawPaw.Game.Systems.WorldState.WorldCollection.WorldCollectionItem",
            "PawPaw.Game.Systems.WorldState.WorldStateManager",
            "PawPaw.Game.Services.Input.Core.InputService",
            "PawPaw.Game.Services.Network.NetworkService",
            "PawPaw.Game.Services.Platform.PlatformDebug",
            "PawPaw.Game.Services.EOS.AuthManagers.SteamEosAuthManager",
            "PawPaw.Game.Services.EOS.EosAuthorization",
            "PawPaw.Game.Services.EOS.EosService",
            "PawPaw.Game.Network.Transports.EOS.EOSTransport",
            "PawPaw.Extensions.Network.NetworkManagerExtensions",
            "PawPaw.Game.Services.Network.Modules.Connection.NetworkConnectionModule",
            "PawPaw.Game.Services.Network.Modules.Connection.Data.ConnectionPayload",
            "PawPaw.Game.Services.Network.Modules.Connection.States.HostSetupState",
            "PawPaw.Game.Services.Network.Modules.Connection.States.ClientConnectingState",
            "PawPaw.Game.Services.Network.Modules.Connection.States.HostingState",
            "PawPaw.Game.Services.Network.Modules.Lobby.NetworkLobbyModule",
            "PawPaw.Game.Services.Network.Modules.Lobby.LobbyJoinData",
            "PawPaw.Game.Services.Network.Modules.Lobby.LobbyJoinType",
        };
        const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        using var w = new StreamWriter(Path.Combine(BepInEx.Paths.BepInExRootPath, "dump_sigs3.txt"));
        foreach (var n in names)
        {
            var t = asms.Select(a => a.GetType(n)).FirstOrDefault(x => x != null);
            w.WriteLine("##### " + n + (t == null ? " (introuvable)" : " : " + t.BaseType?.Name));
            if (t == null) continue;
            if (t.IsEnum) { foreach (var v in Enum.GetNames(t)) w.WriteLine("  = " + v); continue; }
            foreach (var m in t.GetMethods(f).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_") && !m.Name.StartsWith("__")))
                w.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            foreach (var p in t.GetProperties(f).Where(p => !p.Name.StartsWith("NativeFieldInfoPtr") && !p.Name.StartsWith("NativeMethodInfoPtr")))
                w.WriteLine($"  prop {p.PropertyType.Name} {p.Name}");
        }
    }
}
