using System;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Haulnet;

/// <summary>
/// Haulnet inside the game's own menus besides the region page (game code hooks, all optional):
/// <list type="bullet">
/// <item><c>MenuPageMain.Start</c>: the main menu opened, reset the session;</item>
/// <item><c>SteamLobbyManager.SetLobbyData</c>: publish where the game lives in the Steam lobby;</item>
/// <item><c>MenuPageLobby.Start</c> / <c>UpdateChatPrompt</c>: a status line in the lobby, remember the server;</item>
/// <item><c>NetworkConnect.OnDisconnected</c>: a Haulnet explanation instead of "Disconnected: ExceptionOnConnect".</item>
/// </list>
/// If a hook is missing after a game update, that feature is skipped and logged.
/// </summary>
internal static class MenuIntegration
{
    // Zero-width marker so the status line is appended once per prompt text.
    private const string StatusMarker = "​";

    private static bool _incompatibilityShown;

    public static void Apply(Harmony harmony)
    {
        TryPatch(harmony, typeof(MenuPageMain), "Start", postfix: nameof(AfterMainMenuStart));
        TryPatch(harmony, typeof(SteamLobbyManager), nameof(SteamLobbyManager.SetLobbyData), postfix: nameof(AfterSetLobbyData));
        TryPatch(harmony, typeof(MenuPageLobby), "Start", postfix: nameof(AfterLobbyStart));
        TryPatch(harmony, typeof(MenuPageLobby), nameof(MenuPageLobby.UpdateChatPrompt), postfix: nameof(AfterUpdateChatPrompt));
        TryPatch(harmony, typeof(NetworkConnect), nameof(NetworkConnect.OnDisconnected), prefix: nameof(BeforeConnectDisconnected), postfix: nameof(AfterConnectDisconnected));
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

    private static void AfterMainMenuStart()
    {
        Session.Reset();
        if (!WherePage.Available && !_incompatibilityShown)
        {
            _incompatibilityShown = true;
            Popups.Schedule(Text.IncompatibleHeader, Text.IncompatibleBody);
        }
    }

    /// <summary>The game wrote its lobby data (room name, region, version); add or remove ours.</summary>
    private static void AfterSetLobbyData()
    {
        try
        {
            var lobby = LobbyData.CurrentLobby();
            var target = Session.Current;
            switch (target.Kind)
            {
                case TargetKind.Host:
                    LobbyData.WriteHost(lobby, NetworkAddresses.Collect(), Plugin.Server.BasePort);
                    break;
                case TargetKind.Server:
                    LobbyData.WriteServer(lobby, target.Address);
                    break;
                default:
                    LobbyData.Clear(lobby);
                    break;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not write Haulnet lobby data: {e}");
        }
    }

    /// <summary>The lobby page opened: the connection worked. Show where it goes and remember the server.</summary>
    private static void AfterLobbyStart(MenuPageLobby __instance)
    {
        var target = Session.Current;
        if (target.Kind == TargetKind.Cloud)
        {
            return;
        }

        try
        {
            if (target.Kind == TargetKind.Server)
            {
                KnownServers.AddRecent(target.Address, target.Label);
            }

            // The game shows "<Photon region> <room>"; a Haulnet server's region code means nothing.
            if (__instance.roomNameText != null)
            {
                __instance.roomNameText.text = "Haulnet " + PhotonNetwork.CurrentRoom?.Name;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not update the lobby header: {e.Message}");
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

            prompt.text += $"\n<size=80%><color=#FF8C00>{StatusMarker}{Escape(status)}</color></size>";
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
            TargetKind.Host => Text.StatusHosting(Plugin.Server.BasePort),
            TargetKind.Server => Text.StatusServer(target.Label.Length > 0 ? $"{target.Label} ({target.Address.Display})" : target.Address.Display),
            TargetKind.FriendPc => Text.StatusFriendPc(target.Address.ToString()),
            _ => null,
        };
    }

    // The prompt is rich text; server names come from the network.
    private static string Escape(string text) => text.Replace("<", "‹").Replace(">", "›");

    // ---------- Connection failures ----------

    private static bool _failureHandled;

    /// <summary>
    /// <c>NetworkConnect.OnDisconnected</c> (game code) shows "Disconnected / Cause: ..." and leaves to the main
    /// menu when connecting fails before the game has a NetworkManager. For Haulnet targets that cause
    /// (ExceptionOnConnect, timeout) says nothing to the player; replace it with what to check. Before the game
    /// leaves the Steam lobby, a friend who could not reach the host's PC tells the host.
    /// </summary>
    private static void BeforeConnectDisconnected(DisconnectCause _cause)
    {
        _failureHandled = false;
        if (NetworkManager.instance || _cause is DisconnectCause.DisconnectByClientLogic or DisconnectCause.DisconnectByServerLogic)
        {
            return;
        }

        var target = Session.Current;
        if (target.Kind == TargetKind.Cloud)
        {
            return;
        }

        _failureHandled = true;
        Plugin.Log.LogWarning($"Connection to {target.Kind} {target.Address} failed: {_cause}.");
        if (target.Kind == TargetKind.FriendPc)
        {
            JoinGuard.TellHostUnreachable();
        }
    }

    private static void AfterConnectDisconnected()
    {
        if (!_failureHandled)
        {
            return;
        }

        _failureHandled = false;
        var target = Session.Current;
        switch (target.Kind)
        {
            case TargetKind.Server:
                Popups.Schedule(Text.NoAnswerHeader, Text.NoAnswerBody(target.Address.HostForEndpoint, target.Address.EffectivePort));
                break;
            case TargetKind.FriendPc:
                Popups.Schedule(Text.NoAnswerHeader, Text.UnreachableBody(Session.LobbyOwnerName, Session.FriendAddresses));
                break;
            case TargetKind.Host when Plugin.Server.State == ServerState.Failed:
                Popups.Schedule(Text.ServerFailedHeader, Text.ServerFailedBody(Plugin.Server.Error));
                break;
        }
    }
}
