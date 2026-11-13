using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Haulnet;

/// <summary>
/// When a friend's connection to a host who plays "On my PC" fails, the friend's mod tells the host through Steam
/// lobby chat before the game leaves the lobby (<see cref="TellHostUnreachable"/>, called from
/// <see cref="MenuIntegration"/>), and the host sees who could not connect. Only reported facts, no guesses about
/// friends who are merely slow or left.
/// </summary>
internal sealed class JoinGuard : MonoBehaviourPunCallbacks
{
    private const string UnreachableChat = "HAULNET|unreachable";

    public static void Apply(Harmony harmony, GameObject host)
    {
        host.AddComponent<JoinGuard>();
    }

    // ---------- Friend side ----------

    public static void TellHostUnreachable()
    {
        try
        {
            var lobby = LobbyData.CurrentLobby();
            if (lobby.Id.IsValid)
            {
                lobby.SendChatString(UnreachableChat);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not tell the host: {e.Message}");
        }
    }

    // ---------- Host side ----------

    private static bool Hosting
    {
        get
        {
            if (!Session.HostingOnMyPc || !SteamClient.IsValid)
            {
                return false;
            }

            var lobby = LobbyData.CurrentLobby();
            return lobby.Id.IsValid && lobby.IsOwnedBy(SteamClient.SteamId);
        }
    }

    private void Start()
    {
        SteamMatchmaking.OnChatMessage += OnChat;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnChatMessage -= OnChat;
    }

    private void OnChat(Lobby lobby, Friend friend, string message)
    {
        if (message == UnreachableChat && Hosting && friend.Id != SteamClient.SteamId)
        {
            Notify(Text.FriendUnreachableBody(friend.Name, Plugin.Server.BasePort));
        }
    }

    private static void Notify(string body)
    {
        Plugin.Log.LogWarning(body);
        try
        {
            if (SemiFunc.MenuLevel() && MenuManager.instance != null)
            {
                MenuManager.instance.PagePopUp(Text.FriendFailedHeader, Popups.Accent, body, Text.Ok, richText: false);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not show popup: {e.Message}");
        }
    }

}
