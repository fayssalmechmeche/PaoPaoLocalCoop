using System;
using System.IO;
using System.Text;
using Il2CppInterop.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace CoopLocal;

// Composant Unity injecte dans le jeu. Il ne contient que Update() : toute la logique est dans ProbeHelper.
public class Probe : MonoBehaviour
{
    public Probe(IntPtr ptr) : base(ptr) { }

    private void LateUpdate() { Puppet.Apply(Time.deltaTime); }

    private float _timer;
    private int _dumps;

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        Coop.Tick(Time.unscaledDeltaTime);
        AutoStart.Tick(Time.unscaledDeltaTime);
        bool key = ProbeHelper.F9Pressed();
        if (ProbeHelper.KeyPressed(7)) TwoInstances.Host();
        if (ProbeHelper.KeyPressed(8)) TwoInstances.Join();
        if (ProbeHelper.F10Pressed()) { Plugin.Instance.Log.LogInfo("F10 detecte"); Coop.SpawnPlayer2(); }
        if (!key && _timer < 5f) return;
        _timer = 0f;
        if (_dumps >= 1 && !key) return;
        if (ProbeHelper.TryDump(key)) _dumps++;
    }
}

public static class ProbeHelper
{
    public static bool F9Pressed()
    {
        try { var k = UnityEngine.InputSystem.Keyboard.current; return k != null && k.f9Key.wasPressedThisFrame; } catch { return false; }
    }

    public static bool KeyPressed(int f)
    {
        try
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return false;
            return f == 7 ? k.f7Key.wasPressedThisFrame : f == 8 ? k.f8Key.wasPressedThisFrame : false;
        }
        catch { return false; }
    }

    public static bool F10Pressed()
    {
        try { var k = UnityEngine.InputSystem.Keyboard.current; return k != null && k.f10Key.wasPressedThisFrame; } catch { return false; }
    }

    public static bool TryDump(bool force)
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;
            var objs = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
            bool havePlayer = false;
            foreach (var o in objs) if (o.IsPlayerObject) havePlayer = true;
            if (!havePlayer && !force) return false;
            Dump(nm, objs);
            return true;
        }
        catch (Exception e) { Plugin.Instance.Log.LogError("Probe: " + e); return false; }
    }

    private static void Dump(NetworkManager nm, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<NetworkObject> objs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient} LocalClientId={nm.LocalClientId}");
        sb.AppendLine($"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        foreach (var o in objs)
        {
            if (!o.IsPlayerObject) continue;
            sb.AppendLine($"\n=== PLAYER OBJECT '{o.name}' Owner={o.OwnerClientId} IsLocalPlayer={o.IsLocalPlayer} NetworkObjectId={o.NetworkObjectId}");
            sb.AppendLine($"pos={o.transform.position}");
            DumpTree(sb, o.transform, 0);
        }
        sb.AppendLine("\n=== CAMERAS");
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>())
            sb.AppendLine($"{c.name} enabled={c.enabled} depth={c.depth} parent={(c.transform.parent ? c.transform.parent.name : "-")}");
        sb.AppendLine("\n=== GAMEPADS (legacy)");
        try { foreach (var n in Input.GetJoystickNames()) sb.AppendLine("'" + n + "'"); } catch (Exception e) { sb.AppendLine("n/a " + e.Message); }
        var path = Path.Combine(BepInEx.Paths.BepInExRootPath, "dump_player.txt");
        File.WriteAllText(path, sb.ToString());
        Plugin.Instance.Log.LogInfo("Rapport joueur ecrit : " + path);
    }

    public static void DumpCharacters()
    {
        var sb = new StringBuilder();
        foreach (var o in UnityEngine.Object.FindObjectsOfType<NetworkObject>())
        {
            if (!o.name.StartsWith("PlayerCharacter")) continue;
            sb.AppendLine($"=== {o.name} owner={o.OwnerClientId} pos={o.transform.position} parent={(o.transform.parent ? o.transform.parent.name : "-")}");
            DumpTree(sb, o.transform, 0);
        }
        File.WriteAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "dump_characters.txt"), sb.ToString());
    }

    private static void DumpTree(StringBuilder sb, Transform t, int depth)
    {
        if (depth > 3) return;
        var pad = new string(' ', depth * 2);
        var names = new StringBuilder();
        foreach (var c in t.gameObject.GetComponents<Component>())
            if (c != null) names.Append(c.GetIl2CppType().FullName).Append(", ");
        sb.AppendLine($"{pad}- {t.name}: {names}");
        for (int i = 0; i < t.childCount; i++) DumpTree(sb, t.GetChild(i), depth + 1);
    }
}
