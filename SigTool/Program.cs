using System; using System.Linq; using System.Reflection;
// Mode 1: SigTool <dir> <dll> <Type...> -> signatures. Mode 2: SigTool <dir> <dll> --find <MethodName> -> types declaring it.
var dir = args[0]; var dll = args[1];
var resolver = new PathAssemblyResolver(System.IO.Directory.GetFiles(dir, "*.dll").Concat(System.IO.Directory.GetFiles(System.IO.Path.Combine(dir, "..", "core"), "*.dll")).Concat(System.IO.Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")).GroupBy(System.IO.Path.GetFileName).Select(g => g.First()));
using var mlc = new MetadataLoadContext(resolver, "mscorlib");
var asm = mlc.LoadFromAssemblyPath(System.IO.Path.Combine(dir, dll));
const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
string N(Func<string> g) { try { return g(); } catch { return "?"; } }
if (args.Length > 3 && args[2] == "--find") {
  Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
  foreach (var t in types) { try { foreach (var m in t.GetMethods(f)) if (m.Name.Contains(args[3])) Console.WriteLine($"{t.FullName} :: {m.Name}({N(() => string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)))})"); } catch { } }
  return;
}
foreach (var tn in args.Skip(2)) {
  var t = asm.GetType(tn); Console.WriteLine("##### " + tn + (t == null ? " (not found)" : " : " + N(() => t.BaseType?.FullName ?? "")));
  if (t == null) continue;
  foreach (var c in t.GetConstructors(f)) Console.WriteLine($"  ctor({N(() => string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)))})");
  foreach (var m in t.GetMethods(f)) Console.WriteLine($"  {(m.IsStatic ? "static " : "")}{N(() => m.ReturnType.Name)} {m.Name}({N(() => string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)))})");
  foreach (var p in t.GetProperties(f)) Console.WriteLine($"  prop {N(() => p.PropertyType.FullName ?? p.PropertyType.Name)} {p.Name}");
  if (t.IsEnum) foreach (var fl in t.GetFields(BindingFlags.Public | BindingFlags.Static)) Console.WriteLine($"  enum {fl.Name} = {N(() => fl.GetRawConstantValue()?.ToString() ?? "")}");
}
