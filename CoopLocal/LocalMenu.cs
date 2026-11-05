using System;
using HarmonyLib;
using PawPaw.Game.Services.Network.Modules.Lobby;
using PawPaw.Game.Systems.GameModals;
using PawPaw.UI.Common.PawButton.Core;
using PawPaw.UI.Menu.Main;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace CoopLocal;

// Ligne "Coop locale" dans le menu principal, sous "Coop".
// Elle ouvre l'onglet Coop du jeu, dont les deux boutons deviennent :
//  - "Heberger"  : ecran de choix du monde du mode solo (monde existant ou nouveau), puis partie locale ;
//  - "Rejoindre" : attend qu'une partie locale soit ouverte dans l'autre fenetre, puis la rejoint.
// Le bouton "Coop" normal remet l'onglet Coop d'origine (en ligne).
public static class LocalMenu
{
    private static UIMenuView _menu;
    private static PawButton _localButton;
    private static bool _localTab;      // l'onglet Coop est affiche en mode "coop locale"
    private static bool _hostPending;   // le prochain monde choisi ouvre une partie locale
    private static float _findTimer;
    private static GameModal _joinModal;
    private static Session.JoinState _lastJoin = Session.JoinState.Idle;

    private static BepInEx.Logging.ManualLogSource Log => Plugin.Instance.Log;

    // ---------------------------------------------------------------- textes

    private static bool French
    {
        get
        {
            try { return LocalizationSettings.SelectedLocale?.Identifier.Code?.StartsWith("fr") ?? false; }
            catch { return false; }
        }
    }

    private static string T(string fr, string en) => French ? fr : en;

    // ---------------------------------------------------------------- installation

    public static void Apply()
    {
        try
        {
            var h = new Harmony("local.paopao.cooplocal.menu");
            void Pre(string method, string prefix) =>
                h.Patch(AccessTools.Method(typeof(UIMenuView), method), prefix: new HarmonyMethod(typeof(LocalMenu), prefix));
            Pre(nameof(UIMenuView.PrepareGame), nameof(PrepareGamePrefix));
            Pre(nameof(UIMenuView.CreateLobby), nameof(CreateLobbyPrefix));
            Pre(nameof(UIMenuView.ShowLobbies), nameof(ShowLobbiesPrefix));
            Pre(nameof(UIMenuView.OnCoopGameClicked), nameof(LeaveLocalPrefix));
            Pre(nameof(UIMenuView.OnSingleGameClicked), nameof(LeaveLocalPrefix));
            h.Patch(AccessTools.Method(typeof(UIMenuView), nameof(UIMenuView.SwitchTab)),
                postfix: new HarmonyMethod(typeof(LocalMenu), nameof(SwitchTabPostfix)));
            Log.LogInfo("Menu : ligne \"Coop locale\" prete");
        }
        catch (Exception e) { Log.LogError("Menu : " + e); }
    }

    public static void Tick(float dt)
    {
        try
        {
            TickMenu(dt);
            TickJoin();
            if (Session.Error != "")
            {
                ShowMessage(Session.Join == Session.JoinState.Failed
                    ? T("Aucune partie locale trouvée.\n\nDans l'autre fenêtre, choisis Coop locale > Héberger, puis réessaie.",
                        "No local game found.\n\nIn the other window, choose Local co-op > Host, then try again.")
                    : T("La partie locale n'a pas pu s'ouvrir.\n\n", "The local game could not be opened.\n\n") + Session.Error);
                Session.ClearError();
            }
        }
        catch (Exception e) { Log.LogError("Menu : " + e.Message); }
    }

    private static void TickMenu(float dt)
    {
        if (_localButton != null) return;
        _findTimer -= dt;
        if (_findTimer > 0f) return;
        _findTimer = 1f;
        _menu = UnityEngine.Object.FindObjectOfType<UIMenuView>();
        if (_menu == null || _menu._coopButton == null) return;
        CreateButton();
    }

    private static void CreateButton()
    {
        var coop = _menu._coopButton;
        var go = UnityEngine.Object.Instantiate(coop.gameObject, coop.transform.parent);
        go.name = "CoopLocaleButton";
        go.transform.SetSiblingIndex(coop.transform.GetSiblingIndex() + 1);
        _localButton = go.GetComponent<PawButton>();
        // La copie garde les actions enregistrees dans l'editeur du jeu : on les coupe et on met la notre.
        var clicked = _localButton.Clicked;
        for (int i = 0; i < clicked.GetPersistentEventCount(); i++) clicked.SetPersistentListenerState(i, UnityEventCallState.Off);
        clicked.RemoveAllListeners();
        clicked.AddListener((UnityAction)(Action)OnLocalClicked);
        SetLabel(go, T("Coop locale", "Local co-op"));

        var parts = "";
        foreach (var c in go.GetComponentsInChildren<Component>(true)) parts += c.GetIl2CppType().Name + " ";
        Log.LogInfo($"Menu : bouton \"Coop locale\" ajoute (persistants={clicked.GetPersistentEventCount()}) : {parts}");
    }

    // ---------------------------------------------------------------- libelles

    private static void SetLabel(GameObject go, string text)
    {
        foreach (var loc in go.GetComponentsInChildren<LocalizeStringEvent>(true)) loc.enabled = false;
        foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true)) tmp.text = text;
    }

    private static void RestoreLabel(GameObject go)
    {
        foreach (var loc in go.GetComponentsInChildren<LocalizeStringEvent>(true))
        {
            loc.enabled = true;
            loc.RefreshString();
        }
    }

    private static void EnterLocalTab()
    {
        _localTab = true;
        SetLabel(_menu._createLobbyButton.gameObject, T("Héberger", "Host"));
        SetLabel(_menu._findLobbyButton.gameObject, T("Rejoindre", "Join"));
    }

    private static void LeaveLocalTab()
    {
        if (!_localTab) return;
        _localTab = false;
        if (_menu == null) return;
        RestoreLabel(_menu._createLobbyButton.gameObject);
        RestoreLabel(_menu._findLobbyButton.gameObject);
    }

    // ---------------------------------------------------------------- actions

    private static void OnLocalClicked()
    {
        try
        {
            if (_menu == null) return;
            _hostPending = false;
            _menu.SwitchTab(UIMenuView.UIMenuTabs.Coop);
            EnterLocalTab();
        }
        catch (Exception e) { Log.LogError("Menu : " + e); }
    }

    private static void OnHostClicked()
    {
        if (Session.HostAvailable())
        {
            ShowMessage(T("Une partie locale est déjà ouverte dans l'autre fenêtre : choisis Rejoindre.",
                          "A local game is already open in the other window: choose Join."));
            return;
        }
        // Meme ecran que Solo > Charger : choix d'un monde existant ou creation d'un nouveau.
        _hostPending = true;
        _menu.LoadGame();
    }

    private static void OnJoinClicked()
    {
        _hostPending = false;
        Session.StartJoin();
        _joinModal = new GameModal();
        _joinModal.SetHeader(T("Coop locale", "Local co-op"));
        _joinModal.SetContent(T("Recherche d'une partie locale...\n\nDans l'autre fenêtre : Coop locale > Héberger.",
                                "Looking for a local game...\n\nIn the other window: Local co-op > Host."));
        _joinModal.SetButtonResult(GameModalButtons.Cancel, (Il2CppSystem.Action)(Action)(() =>
        {
            Session.CancelJoin();
            HideJoinModal();
        }));
        _joinModal.Build();
        _lastJoin = Session.Join;
    }

    private static void TickJoin()
    {
        if (Session.Join == _lastJoin) return;
        _lastJoin = Session.Join;
        if (Session.Join == Session.JoinState.Connected || Session.Join == Session.JoinState.Failed) HideJoinModal();
    }

    private static void HideJoinModal()
    {
        try { _joinModal?.Hide(); } catch { }
        _joinModal = null;
    }

    private static void ShowMessage(string text)
    {
        var m = new GameModal();
        m.SetHeader(T("Coop locale", "Local co-op"));
        m.SetContent(text);
        m.SetButtonResult(GameModalButtons.Ok, (Il2CppSystem.Action)(Action)(() => m.Hide()));
        m.Build();
    }

    // ---------------------------------------------------------------- patchs du menu du jeu

    // Monde choisi (ou cree) apres "Heberger" : partie locale au lieu de la partie solo.
    private static bool PrepareGamePrefix(bool overNetwork, string worldId)
    {
        if (!_hostPending) return true;
        _hostPending = false;
        Log.LogInfo($"Menu : Heberger -> monde {worldId} (le jeu demandait reseau={overNetwork})");
        Session.HostWorld(worldId, "");
        return false;
    }

    private static bool CreateLobbyPrefix()
    {
        if (!_localTab) return true;
        try { OnHostClicked(); } catch (Exception e) { Log.LogError("Menu : " + e); }
        return false;
    }

    private static bool ShowLobbiesPrefix()
    {
        if (!_localTab) return true;
        try { OnJoinClicked(); } catch (Exception e) { Log.LogError("Menu : " + e); }
        return false;
    }

    // Solo ou Coop (en ligne) : on quitte le mode coop locale.
    private static void LeaveLocalPrefix()
    {
        _hostPending = false;
        LeaveLocalTab();
    }

    private static void SwitchTabPostfix(UIMenuView.UIMenuTabs newTab)
    {
        if (newTab != UIMenuView.UIMenuTabs.Coop) LeaveLocalTab();
    }
}
