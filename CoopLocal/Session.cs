using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PawPaw.Architecture.Dependency;
using PawPaw.Debug.Multiplayer;
using PawPaw.Game.Services.GameState;
using PawPaw.Game.Services.Network.Modules.Session;
using PawPaw.Game.Services.Platform;
using Unity.Netcode;

namespace CoopLocal;

// Starts the co-op game over a direct connection on this PC, using the developers' multiplayer
// test tool (FastStart):
//  - host  : loads the world selected in the menu (or the last played world on automatic start),
//             opens the game, then signals the other window;
//  - client: waits until the other window has opened a game, then joins it.
public static class Session
{
    private enum Step { Idle, WaitMenu, LoadingWorld, Started, LoadingMap, Done }
    public enum JoinState { Idle, WaitingHost, Connecting, Connected, Failed }

    private const float MenuDelay = 15f;          // automatic start: lets the menu finish loading
    private const float WorldLoadTimeout = 20f;
    private const float WaitHostAuto = 180f;      // automatic start: the host needs ~30 s
    private const float WaitHostMenu = 120f;
    private const float ConnectTimeout = 20f;
    private const int MaxTries = 3;

    // Signals visible to the other window (same Windows session).
    private const string HostReadySignal = "Local\\PaoPaoCoopLocalGameOpen";

    private static Step _step = Step.Idle;
    private static float _t;
    private static float _stepT;
    private static UniTask _worldTask;
    private static IGameStateLoader _loader;
    private static EventWaitHandle _readySignal;

    private static int _tries;
    private static float _joinT;

    public static JoinState Join { get; private set; } = JoinState.Idle;
    public static string Error { get; private set; } = ""; // last error to show the player (empty = none)
    public static bool HostBusy => _step != Step.Idle && _step != Step.Done;

    private static BepInEx.Logging.ManualLogSource Log => Plugin.Instance.Log;

    public static void Tick(float dt)
    {
        _t += dt;
        _stepT += dt;
        _joinT += dt;
        try
        {
            if (Role.AutoStart && _t < 1f) return;
            if (Role.AutoStart && _step == Step.Idle && Join == JoinState.Idle)
            {
                if (Role.Current == CoopRole.Host) Go(Step.WaitMenu);
                else StartJoin(WaitHostAuto);
            }
            TickHost();
            TickClient();
        }
        catch (Exception e)
        {
            Log.LogError("Session: " + e);
            Fail("Erreur inattendue : " + e.Message, "Unexpected error: " + e.Message);
            _step = Step.Done;
        }
    }

    public static void ClearError() => Error = "";

    private static void Fail(string fr, string en) { Error = LocalMenu.T(fr, en); Log.LogError(en); }

    private static void Go(Step s) { _step = s; _stepT = 0f; }

    private static bool TimedOut(float limit, string whatFr, string whatEn)
    {
        if (_stepT < limit) return false;
        Fail($"HOTE : abandon, {whatFr} (etape {_step}).", $"HOST: giving up, {whatEn} (step {_step}).");
        Go(Step.Done);
        return true;
    }

    // ---------------------------------------------------------------- host

    // Called by the menu (Local co-op > Host) once the world is selected or created.
    public static bool HostWorld(string worldId, string worldName)
    {
        if (HostBusy || Join == JoinState.Connecting || Join == JoinState.Connected) return false;
        Role.Current = CoopRole.Host;
        Join = JoinState.Idle;
        FastStart.ResolveDependencies();
        var wsm = FastStart._worldStateManager;
        if (wsm == null || FastStart._connectionModule == null) { Fail("HOTE : le jeu n'est pas pret, reessaie dans quelques secondes.", "HOST: the game is not ready, try again in a few seconds."); return false; }
        Log.LogInfo($"HOST: loading world '{worldName}' ({worldId})");
        _worldTask = wsm.SetCurrentWorld(worldId);
        Go(Step.LoadingWorld);
        return true;
    }

    private static void TickHost()
    {
        switch (_step)
        {
            case Step.WaitMenu:
                if (_t < MenuDelay) return;
                FastStart.ResolveDependencies();
                var wsm = FastStart._worldStateManager;
                if (wsm == null || FastStart._connectionModule == null) { _t = MenuDelay - 2f; return; }
                // Read the last played world directly from the save data:
                // the "Nullable" version of WorldStateManager.LastPlayedWorld is mistranslated by
                // the IL2CPP layer (fields are shifted: we received the name instead of the id).
                var world = wsm._worldCollectionData?.LastPlayedWorld;
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                {
                    Log.LogWarning("HOST: no played world found, using the developers' test world.");
                    FastStart.StartServer();
                    _readySignal ??= new EventWaitHandle(false, EventResetMode.ManualReset, HostReadySignal);
                    Go(Step.Done);
                    return;
                }
                Log.LogInfo($"HOST: loading last played world '{world.WorldName}' ({world.WorldId})");
                _worldTask = wsm.SetCurrentWorld(world.WorldId);
                Go(Step.LoadingWorld);
                return;

            case Step.LoadingWorld:
                bool loaded = _worldTask.Status != UniTaskStatus.Pending;
                if (!loaded && _stepT < WorldLoadTimeout) return;
                Log.LogInfo($"HOST: world ready (state={_worldTask.Status}), opening the local co-op game");
                FastStart.SetupConnectionData(0);
                FastStart._connectionModule.StartHost();
                Go(Step.Started);
                return;

            case Step.Started:
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsHost) { TimedOut(15f, "la partie ne s'est pas ouverte", "the game did not open"); return; }
                // The game accepts connections: notify the other window.
                _readySignal ??= new EventWaitHandle(false, EventResetMode.ManualReset, HostReadySignal);
                // Like the developers' tool: once the game is open, load the game map.
                _loader = Container.Resolve<IGameStateLoader>();
                Log.LogInfo($"HOST: game open, loading the map (current state={_loader.CurrentState})");
                if (_loader.CurrentState != GameStateType.Gameplay) _loader.TransitionTo(GameStateType.Gameplay);
                Go(Step.LoadingMap);
                return;

            case Step.LoadingMap:
                // Once the map is loaded, start the session: this is what creates the characters.
                if (_loader.CurrentState != GameStateType.Gameplay || !_loader.Ready) { TimedOut(60f, "la carte ne se charge pas", "the map does not load"); return; }
                var session = UnityEngine.Object.FindObjectOfType<NetworkSessionInstance>();
                if (session == null) { TimedOut(60f, "pas de session", "no session"); return; }
                if (!session.IsGameStarted)
                {
                    Log.LogInfo("HOST: map loaded, starting the session (creating the characters)");
                    session.StartGame();
                }
                else Log.LogInfo("HOST: map loaded, session already started");
                Go(Step.Done);
                return;
        }
    }

    // ---------------------------------------------------------------- client

    public static bool HostAvailable()
    {
        if (!EventWaitHandle.TryOpenExisting(HostReadySignal, out var h)) return false;
        h.Dispose();
        return true;
    }

    // Called by the menu (Local co-op > Join), or on automatic start.
    public static void StartJoin(float waitHost = WaitHostMenu)
    {
        if (HostBusy) return;
        Role.Current = CoopRole.Client;
        _tries = 0;
        _joinT = 0f;
        _waitHost = waitHost;
        Join = JoinState.WaitingHost;
        Log.LogInfo(HostAvailable() ? "CLIENT: local game found" : "CLIENT: waiting for a local game...");
    }

    public static void CancelJoin()
    {
        if (Join != JoinState.WaitingHost && Join != JoinState.Connecting) return;
        Join = JoinState.Idle;
        Role.Current = CoopRole.None;
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && !nm.IsConnectedClient) nm.Shutdown();
        Log.LogInfo("CLIENT: search cancelled");
    }

    private static float _waitHost = WaitHostMenu;

    private static void TickClient()
    {
        var nm = NetworkManager.Singleton;
        switch (Join)
        {
            case JoinState.WaitingHost:
                if (HostAvailable()) { Connect(); return; }
                if (_joinT > _waitHost)
                {
                    Join = JoinState.Failed;
                    Fail("CLIENT : aucune partie locale trouvee.", "CLIENT: no local game found.");
                }
                return;

            case JoinState.Connecting:
                if (nm != null && nm.IsConnectedClient)
                {
                    Log.LogInfo("CLIENT: connected to the host!");
                    Join = JoinState.Connected;
                    return;
                }
                if (_joinT < ConnectTimeout) return;
                if (nm != null && (nm.IsClient || nm.ShutdownInProgress))
                {
                    if (_joinT < ConnectTimeout * 2) return; // attempt still in progress
                    nm.Shutdown();
                    return;
                }
                if (_tries >= MaxTries)
                {
                    Join = JoinState.Failed;
                    Fail("CLIENT : impossible de rejoindre la partie locale.", "CLIENT: could not join the local game.");
                    return;
                }
                Connect();
                return;
        }
    }

    private static void Connect()
    {
        _tries++;
        _joinT = 0f;
        Join = JoinState.Connecting;
        FastStart.ResolveDependencies();
        PlatformDebug.UserIdOverride = "local-coop-player-2";
        Log.LogInfo($"CLIENT: connection attempt {_tries}/{MaxTries}");
        FastStart.StartClient();
    }
}
