using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoopLocal;

public static class Sigs
{
    public static void Dump()
    {
        string interop = Path.Combine(BepInEx.Paths.BepInExRootPath, "interop");
        var asm = Assembly.LoadFrom(Path.Combine(interop, "PawPaw.Game.dll"));
        string[] names = {
            "PawPaw.Game.Services.Network.Modules.Session.NetworkSessionUtility",
            "PawPaw.Game.Services.Network.Modules.Session.NetworkSessionInstance",
            "PawPaw.Game.Services.Network.Modules.Session.SessionMemberData",
            "PawPaw.Game.Units.Common.CharacterMovement",
            "PawPaw.Game.Units.Controllers.Player.NetworkPlayer",
            "PawPaw.Game.Units.Common.Character",
            "PawPaw.Game.Units.Controllers.Player.PlayerSpawnerComponent",
            "PawPaw.Game.Units.Controllers.Player.PlayerInputHandler",
            "PawPaw.Game.Units.Common.CharacterInputState",
        };
        using var w = new StreamWriter(Path.Combine(BepInEx.Paths.BepInExRootPath, "dump_sigs.txt"));
        const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var n in names)
        {
            var t = asm.GetType(n);
            w.WriteLine("##### " + n + (t == null ? " (introuvable)" : ""));
            if (t == null) continue;
            foreach (var c in t.GetConstructors(f)) w.WriteLine("  ctor(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            foreach (var m in t.GetMethods(f).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_") && !m.Name.StartsWith("__") && !m.Name.StartsWith("_")))
                w.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            foreach (var p in t.GetProperties(f).Where(p => !p.Name.StartsWith("_") ))
                w.WriteLine($"  prop {p.PropertyType.Name} {p.Name}");
        }
    }
}
