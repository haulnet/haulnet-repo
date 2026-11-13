using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using Color = UnityEngine.Color;

namespace Haulnet;

/// <summary>
/// Clear outcomes when a friend cannot reach a host who plays "On my PC".
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Friend side: if no host address answered the reachability probe, the join is cancelled before the
/// game starts connecting, with a message naming the host, instead of a connection timeout and a generic
/// error. Uses the game's own exit path for a failed Steam lobby check
/// (<c>NetworkConnect.SteamLobbyVersionCheck</c>, game code). Before leaving, the friend's mod tells the host
/// through Steam lobby chat.</item>
/// <item>Host side: a friend who joined the Steam lobby but did not reach the room (left early, reported that
/// the host is unreachable, or is still not in after a while) produces a message for the host.</item>
/// </list>
/// </remarks>
internal sealed class JoinGuard : MonoBehaviourPunCallbacks
{
    private const string UnreachableChat = "HAULNET|unreachable";
    private const float SlowJoinSeconds = 25f;

    private readonly Dictionary<ulong, Pending> _pending = new();

    public static void Apply(Harmony harmony, GameObject host)
    {
        host.AddComponent<JoinGuard>();

        var check = AccessTools.Method(typeof(NetworkConnect), "SteamLobbyVersionCheck");
        if (check == null)
        {
            Plugin.Log.LogWarning("NetworkConnect.SteamLobbyVersionCheck not found (game update?); unreachable hosts end in a timeout.");
            return;
        }

        harmony.Patch(check, postfix: new HarmonyMethod(typeof(JoinGuard), nameof(AfterLobbyCheck)));
    }

    // ---------- Friend side ----------

    private static void AfterLobbyCheck(ref bool __result)
    {
        if (!__result || !Session.TakeUnreachable(out var hostName))
        {
            return;
        }

        __result = false;
        try
        {
            var lobby = LobbyData.CurrentLobby();
            if (lobby.Id.IsValid)
            {
                lobby.SendChatString(UnreachableChat);
            }

            // Same steps the game takes when its own lobby check fails.
            MenuManager.instance.PagePopUpScheduled(Text.UnreachableHeader, Color.red, Text.UnreachableBody(hostName), Text.Ok, richText: false);
            PhotonNetwork.Disconnect();
            SteamLobbyManager.instance.LeaveLobby();
            GameManager.instance.SetGameMode(0);
            RunManager.instance.levelCurrent = RunManager.instance.levelMainMenu;
            SceneManager.LoadSceneAsync("Reload");
            Plugin.Log.LogInfo("Join cancelled: the host is not reachable directly.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not cancel the join cleanly: {e}");
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
        SteamMatchmaking.OnLobbyMemberJoined += OnMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnMemberLeft;
        SteamMatchmaking.OnChatMessage += OnChat;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyMemberJoined -= OnMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= OnMemberLeft;
        SteamMatchmaking.OnChatMessage -= OnChat;
    }

    private void OnMemberJoined(Lobby lobby, Friend friend)
    {
        if (Hosting && friend.Id != SteamClient.SteamId)
        {
            _pending[friend.Id.Value] = new Pending(friend.Name, Time.realtimeSinceStartup);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Photon nicknames are Steam names in R.E.P.O.; user IDs are not published in private rooms.
        foreach (var key in _pending.Where(p => p.Value.Name == newPlayer.NickName).Select(p => p.Key).ToList())
        {
            _pending.Remove(key);
        }
    }

    private void OnChat(Lobby lobby, Friend friend, string message)
    {
        if (message == UnreachableChat && Hosting && friend.Id != SteamClient.SteamId)
        {
            _pending.Remove(friend.Id.Value);
            Notify(Text.FriendUnreachableBody(friend.Name));
        }
    }

    private void OnMemberLeft(Lobby lobby, Friend friend)
    {
        if (_pending.TryGetValue(friend.Id.Value, out var pending))
        {
            _pending.Remove(friend.Id.Value);
            if (Hosting && !pending.Notified)
            {
                Notify(Text.FriendLeftBody(pending.Name));
            }
        }
    }

    private void Update()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        if (!Hosting)
        {
            _pending.Clear();
            return;
        }

        var now = Time.realtimeSinceStartup;
        foreach (var entry in _pending.Values.Where(p => !p.Notified && now - p.Since > SlowJoinSeconds).ToList())
        {
            entry.Notified = true;
            Notify(Text.FriendSlowBody(entry.Name, (int)SlowJoinSeconds));
        }
    }

    private static void Notify(string body)
    {
        Plugin.Log.LogWarning(body);
        try
        {
            if (SemiFunc.MenuLevel() && MenuManager.instance != null)
            {
                MenuManager.instance.PagePopUp(Text.FriendFailedHeader, new Color(1f, 0.55f, 0f), body, Text.Ok, richText: false);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not show popup: {e.Message}");
        }
    }

    private sealed class Pending
    {
        public Pending(string name, float since)
        {
            Name = name;
            Since = since;
        }

        public string Name { get; }
        public float Since { get; }
        public bool Notified { get; set; }
    }
}
