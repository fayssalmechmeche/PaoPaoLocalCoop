using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoopLocal;

// Liste les types (et membres des types "interessants") des assemblages lies au demarrage / plateforme / reseau.
public static class Sigs2
{
    public static void Dump()
    {
        string interop = Path.Combine(BepInEx.Paths.BepInExRootPath, "interop");
        string[] dlls = { "PawPaw.Application.dll", "PawPaw.Platform.Base.dll", "PawPaw.Platform.Dummy.dll", "PawPaw.Platform.Steamworks.dll", "PawPaw.Debug.dll", "PawPaw.Architecture.dll", "Unity.Netcode.Runtime.dll" };
        string[] keys = { "platform", "login", "auth", "transport", "connect", "args", "command", "boot", "entry", "install", "eos", "identity", "user", "dummy" };
        const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        using var w = new StreamWriter(Path.Combine(BepInEx.Paths.BepInExRootPath, "dump_sigs2.txt"));
        foreach (var dll in dlls)
        {
            Type[] types;
            try { types = Assembly.LoadFrom(Path.Combine(interop, dll)).GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            catch (Exception e) { w.WriteLine($"##### {dll} ERREUR {e.Message}"); continue; }
            w.WriteLine($"##### {dll} ({types.Length})");
            foreach (var t in types.OrderBy(t => t.FullName))
            {
                var name = t.FullName ?? "";
                if (name.Contains("+_") || name.Contains("__c") || name.Contains("MethodInfoStore")) continue;
                if (dll.StartsWith("Unity.Netcode") && !name.Contains("Transport")) continue;
                w.WriteLine(name + (t.BaseType != null ? " : " + t.BaseType.Name : ""));
                if (!keys.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                try
                {
                    foreach (var m in t.GetMethods(f).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_")))
                        w.WriteLine($"    {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
                    foreach (var p in t.GetProperties(f).Where(p => !p.Name.StartsWith("NativeFieldInfoPtr")))
                        w.WriteLine($"    prop {p.PropertyType.Name} {p.Name}");
                }
                catch { }
            }
        }
    }
}
