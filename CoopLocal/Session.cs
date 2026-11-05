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

// Demarre la partie coop en connexion directe sur ce PC, avec l'outil de test multijoueur
// des developpeurs (FastStart) :
//  - hote   : charge le monde choisi dans le menu (ou le dernier monde joue en demarrage automatique),
//             ouvre la partie, puis le signale a l'autre fenetre ;
//  - client : attend que l'autre fenetre ait ouvert une partie, puis la rejoint.
public static class Session
{
    private enum Step { Idle, WaitMenu, LoadingWorld, Started, LoadingMap, Done }
    public enum JoinState { Idle, WaitingHost, Connecting, Connected, Failed }

    private const float MenuDelay = 15f;          // demarrage automatique : laisse le menu finir de charger
    private const float WorldLoadTimeout = 20f;
    private const float WaitHostAuto = 180f;      // demarrage automatique : l'hote a besoin de ~30 s
    private const float WaitHostMenu = 120f;
    private const float ConnectTimeout = 20f;
    private const int MaxTries = 3;

    // Signaux visibles par l'autre fenetre (meme session Windows).
    private const string HostReadySignal = "Local\\PaoPaoCoopLocalPartieOuverte";

    private static Step _step = Step.Idle;
    private static float _t;
    private static float _stepT;
    private static UniTask _worldTask;
    private static IGameStateLoader _loader;
    private static EventWaitHandle _readySignal;

    private static int _tries;
    private static float _joinT;

    public static JoinState Join { get; private set; } = JoinState.Idle;
    public static string Error { get; private set; } = ""; // derniere erreur a afficher au joueur (vide = aucune)
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
            Log.LogError("Session : " + e);
            Fail("Erreur inattendue : " + e.Message);
            _step = Step.Done;
        }
    }

    public static void ClearError() => Error = "";

    private static void Fail(string msg) { Error = msg; Log.LogError(msg); }

    private static void Go(Step s) { _step = s; _stepT = 0f; }

    private static bool TimedOut(float limit, string what)
    {
        if (_stepT < limit) return false;
        Fail($"HOTE : abandon, {what} (etape {_step}).");
        Go(Step.Done);
        return true;
    }

    // ---------------------------------------------------------------- hote

    // Appele par le menu (Coop locale > Heberger) une fois le monde choisi ou cree.
    public static bool HostWorld(string worldId, string worldName)
    {
        if (HostBusy || Join == JoinState.Connecting || Join == JoinState.Connected) return false;
        Role.Current = CoopRole.Host;
        Join = JoinState.Idle;
        FastStart.ResolveDependencies();
        var wsm = FastStart._worldStateManager;
        if (wsm == null || FastStart._connectionModule == null) { Fail("HOTE : le jeu n'est pas pret, reessaie dans quelques secondes."); return false; }
        Log.LogInfo($"HOTE : chargement du monde '{worldName}' ({worldId})");
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
                // On lit le dernier monde joue directement dans les donnees de sauvegarde :
                // la version "Nullable" de WorldStateManager.LastPlayedWorld est mal traduite par
                // la couche IL2CPP (les champs sont decales : on recevait le nom a la place de l'id).
                var world = wsm._worldCollectionData?.LastPlayedWorld;
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                {
                    Log.LogWarning("HOTE : aucun monde joue trouve, utilisation du monde de test des developpeurs.");
                    FastStart.StartServer();
                    _readySignal ??= new EventWaitHandle(false, EventResetMode.ManualReset, HostReadySignal);
                    Go(Step.Done);
                    return;
                }
                Log.LogInfo($"HOTE : chargement du dernier monde joue '{world.WorldName}' ({world.WorldId})");
                _worldTask = wsm.SetCurrentWorld(world.WorldId);
                Go(Step.LoadingWorld);
                return;

            case Step.LoadingWorld:
                bool loaded = _worldTask.Status != UniTaskStatus.Pending;
                if (!loaded && _stepT < WorldLoadTimeout) return;
                Log.LogInfo($"HOTE : monde pret (etat={_worldTask.Status}), ouverture de la partie coop locale");
                FastStart.SetupConnectionData(0);
                FastStart._connectionModule.StartHost();
                Go(Step.Started);
                return;

            case Step.Started:
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsHost) { TimedOut(15f, "la partie ne s'est pas ouverte"); return; }
                // La partie accepte les connexions : on previent l'autre fenetre.
                _readySignal ??= new EventWaitHandle(false, EventResetMode.ManualReset, HostReadySignal);
                // Comme l'outil des developpeurs : une fois la partie ouverte, on charge la carte de jeu.
                _loader = Container.Resolve<IGameStateLoader>();
                Log.LogInfo($"HOTE : partie ouverte, chargement de la carte (etat actuel={_loader.CurrentState})");
                if (_loader.CurrentState != GameStateType.Gameplay) _loader.TransitionTo(GameStateType.Gameplay);
                Go(Step.LoadingMap);
                return;

            case Step.LoadingMap:
                // Une fois la carte chargee, on demarre la session : c'est ce qui cree les personnages.
                if (_loader.CurrentState != GameStateType.Gameplay || !_loader.Ready) { TimedOut(60f, "la carte ne se charge pas"); return; }
                var session = UnityEngine.Object.FindObjectOfType<NetworkSessionInstance>();
                if (session == null) { TimedOut(60f, "pas de session"); return; }
                if (!session.IsGameStarted)
                {
                    Log.LogInfo("HOTE : carte chargee, demarrage de la session (creation des personnages)");
                    session.StartGame();
                }
                else Log.LogInfo("HOTE : carte chargee, session deja demarree");
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

    // Appele par le menu (Coop locale > Rejoindre), ou au demarrage automatique.
    public static void StartJoin(float waitHost = WaitHostMenu)
    {
        if (HostBusy) return;
        Role.Current = CoopRole.Client;
        _tries = 0;
        _joinT = 0f;
        _waitHost = waitHost;
        Join = JoinState.WaitingHost;
        Log.LogInfo(HostAvailable() ? "CLIENT : partie locale trouvee" : "CLIENT : en attente d'une partie locale...");
    }

    public static void CancelJoin()
    {
        if (Join != JoinState.WaitingHost && Join != JoinState.Connecting) return;
        Join = JoinState.Idle;
        Role.Current = CoopRole.None;
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && !nm.IsConnectedClient) nm.Shutdown();
        Log.LogInfo("CLIENT : recherche annulee");
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
                    Fail("CLIENT : aucune partie locale trouvee.");
                }
                return;

            case JoinState.Connecting:
                if (nm != null && nm.IsConnectedClient)
                {
                    Log.LogInfo("CLIENT : connecte a l'hote !");
                    Join = JoinState.Connected;
                    return;
                }
                if (_joinT < ConnectTimeout) return;
                if (nm != null && (nm.IsClient || nm.ShutdownInProgress))
                {
                    if (_joinT < ConnectTimeout * 2) return; // tentative encore en cours
                    nm.Shutdown();
                    return;
                }
                if (_tries >= MaxTries)
                {
                    Join = JoinState.Failed;
                    Fail("CLIENT : impossible de rejoindre la partie locale.");
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
        Log.LogInfo($"CLIENT : tentative de connexion {_tries}/{MaxTries}");
        FastStart.StartClient();
    }
}
