using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace Haulnet;

/// <summary>
/// Haulnet inside the game's own menus (game code hooks, all optional):
/// <list type="bullet">
/// <item><c>MenuPageMain.ButtonEventHostGame</c>: "Where to host?" popup before the region page;</item>
/// <item><c>MenuPageMain.Start</c>: the main menu opened, reset the session;</item>
/// <item><c>SteamLobbyManager.SetLobbyData</c>: publish the host's addresses in the Steam lobby;</item>
/// <item><c>MenuPageLobby.Start</c>: a status line in the lobby.</item>
/// </list>
/// If a hook is missing after a game update, that feature is skipped and logged; the F8 panel still works.
/// </summary>
internal static class MenuIntegration
{
    private static readonly Color Accent = new(1f, 0.55f, 0f);

    // Zero-width marker so the status line is appended once per prompt text.
    private const string StatusMarker = "​";

    private static bool _bypassHostChoice;
    private static MenuButtonPopUp? _popupTemplate;

    public static void Apply(Harmony harmony)
    {
        TryPatch(harmony, typeof(MenuPageMain), nameof(MenuPageMain.ButtonEventHostGame), prefix: nameof(BeforeHostGame));
        TryPatch(harmony, typeof(MenuPageMain), "Start", postfix: nameof(AfterMainMenuStart));
        TryPatch(harmony, typeof(SteamLobbyManager), nameof(SteamLobbyManager.SetLobbyData), postfix: nameof(AfterSetLobbyData));
        TryPatch(harmony, typeof(MenuPageLobby), "Start", postfix: nameof(AfterLobbyStart));
        TryPatch(harmony, typeof(MenuPageLobby), nameof(MenuPageLobby.UpdateChatPrompt), postfix: nameof(AfterUpdateChatPrompt));
    }

    private static void TryPatch(Harmony harmony, Type type, string method, string? prefix = null, string? postfix = null)
    {
        var target = AccessTools.Method(type, method);
        if (target == null)
        {
            Plugin.Log.LogWarning($"{type.Name}.{method} not found (game update?); that part of the menu integration is off.");
            return;
        }

        harmony.Patch(
            target,
            prefix: prefix != null ? new HarmonyMethod(typeof(MenuIntegration), prefix) : null,
            postfix: postfix != null ? new HarmonyMethod(typeof(MenuIntegration), postfix) : null);
    }

    /// <summary>"Private game" was confirmed: ask where to host before the region page opens.</summary>
    private static bool BeforeHostGame(MenuPageMain __instance)
    {
        if (_bypassHostChoice)
        {
            return true;
        }

        try
        {
            var popup = PopupTemplate();
            var myPcFirst = Plugin.Settings.LastChoiceOnMyPc;
            popup.option1Event = new UnityEvent();
            popup.option2Event = new UnityEvent();
            popup.option1Event.AddListener(() => Continue(__instance, myPcFirst));
            popup.option2Event.AddListener(() => Continue(__instance, !myPcFirst));

            MenuManager.instance.PagePopUpTwoOptions(
                popup,
                Text.WhereHeader, null, Accent,
                Text.WhereBody, null,
                myPcFirst ? Text.OptionMyPc : Text.OptionCloud, null,
                myPcFirst ? Text.OptionCloud : Text.OptionMyPc, null,
                richText: false);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not show the hosting choice, continuing with Photon Cloud: {e}");
            return true;
        }
    }

    private static void Continue(MenuPageMain page, bool onMyPc)
    {
        if (onMyPc)
        {
            Session.ChooseOnMyPc();
            if (Plugin.Server.State == ServerState.Failed)
            {
                Popups.ShowNow(Text.ServerFailedHeader, Text.ServerFailedBody(Plugin.Server.Error));
                Session.ChooseCloud();
                return;
            }

            if (OpenSavesDirectly())
            {
                return;
            }
        }
        else
        {
            Session.ChooseCloud();
        }

        _bypassHostChoice = true;
        try
        {
            page.ButtonEventHostGame();
        }
        finally
        {
            _bypassHostChoice = false;
        }
    }

    /// <summary>
    /// Hosting on this PC: skip the region page. There is one server, the host's own PC, so choosing a region
    /// is an empty step. Does what <c>MenuPageRegions.PickRegion</c> does for a hosted game: set the region
    /// and open the save page. <c>NetworkConnect</c> applies region, version and server before connecting.
    /// Returns false (and the region page opens as usual) if the game code changed.
    /// </summary>
    private static bool OpenSavesDirectly()
    {
        try
        {
            NetworkRegion(DataDirector.instance) = HostServer.RegionCode;
            SemiFunc.MainMenuSetMultiplayer();
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not skip the region page, showing it instead: {e.Message}");
            return false;
        }
    }

    // DataDirector.networkRegion is internal in the game.
    private static readonly AccessTools.FieldRef<DataDirector, string> NetworkRegion =
        AccessTools.FieldRefAccess<DataDirector, string>("networkRegion");

    private static MenuButtonPopUp PopupTemplate()
    {
        if (_popupTemplate == null)
        {
            var go = new GameObject("Haulnet.PopupTemplate") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _popupTemplate = go.AddComponent<MenuButtonPopUp>();
        }

        return _popupTemplate;
    }

    private static void AfterMainMenuStart()
    {
        Session.Reset();
    }

    /// <summary>The game wrote its lobby data (room name, region, version); add or remove ours.</summary>
    private static void AfterSetLobbyData()
    {
        try
        {
            if (Session.HostingOnMyPc)
            {
                LobbyData.Write(LobbyData.CurrentLobby(), NetworkAddresses.Collect());
            }
            else
            {
                LobbyData.Clear(LobbyData.CurrentLobby());
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not write Haulnet lobby data: {e}");
        }
    }

    /// <summary>The lobby page opened: the connection worked. Show where it goes.</summary>
    private static void AfterLobbyStart(MenuPageLobby __instance)
    {
        // The connection succeeded, so a "host not reachable" warning from the probe is moot.
        try
        {
            MenuManager.instance?.PagePopUpScheduledReset();
        }
        catch (Exception)
        {
        }

        AppendStatus(__instance);
    }

    private static void AfterUpdateChatPrompt(MenuPageLobby __instance) => AppendStatus(__instance);

    /// <summary>
    /// Adds a second line to the lobby's "Press T to chat" prompt under the LOBBY title. The game rewrites
    /// the prompt when the language or input glyphs change, so this also runs after <c>UpdateChatPrompt</c>.
    /// Using the game's own text keeps the line where the game lays out its header on every resolution.
    /// </summary>
    private static void AppendStatus(MenuPageLobby page)
    {
        try
        {
            var prompt = page.chatPromptText;
            var status = StatusLine();
            if (prompt == null || status == null || prompt.text.Contains(StatusMarker))
            {
                return;
            }

            prompt.text += $"\n<size=80%><color=#FF8C00>{StatusMarker}{status}</color></size>";
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not add the lobby status line: {e.Message}");
        }
    }

    private static string? StatusLine()
    {
        var target = Session.Current;
        return target.Kind switch
        {
            TargetKind.Host => Text.StatusHosting,
            TargetKind.Remote when target.Label.Length > 0 => Text.StatusJoined(target.Address.ToString(), target.Label),
            TargetKind.Remote => Text.StatusManual(target.Address.ToString()),
            _ => null,
        };
    }
}
