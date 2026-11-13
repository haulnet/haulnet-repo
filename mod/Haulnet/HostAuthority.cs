using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Haulnet;

/// <summary>
/// Room rules that Photon Cloud enforces on its servers, enforced by the host's mod in Haulnet games.
/// </summary>
/// <remarks>
/// R.E.P.O. relies on server-side logic of its Photon Cloud app for three things (game code):
/// <list type="bullet">
/// <item>lobby password: the room property <c>PASSWORD</c> is compared with the joining player's property
/// <c>PASSWORD</c>; nothing in the client compares them;</item>
/// <item>kick and ban: <c>NetworkManager.KickPlayer</c> / <c>BanPlayer</c> raise events 123 / 124 that no client
/// handles; the server removes the player and sends them event 199 ("kicked by the server").</item>
/// </list>
/// LuxonServer has no such logic, so in Haulnet games the host (always the MasterClient) does it: it sends the
/// player event 199, which the game already handles (popup, leave room), preceded by a Haulnet event with the
/// reason so the player's mod shows the right text, and closes the connection if the player stays.
/// Enforcement by a client is weaker than by a server: a refused player is inside the room for a moment, and a
/// tampered client could ignore the request. Photon Cloud games are left to Photon.
/// </remarks>
internal sealed class HostAuthority : MonoBehaviourPunCallbacks
{
    private const byte ReasonEvent = 177;    // Haulnet: reason for the removal that follows
    private const byte KickedEvent = 199;    // Game: "you were kicked by the server"

    private const int ReasonWrongPassword = 1;
    private const int ReasonBanned = 2;

    private static readonly HashSet<string> Banned = new();
    private static HostAuthority? _instance;
    private static int _pendingReason;

    public static void Apply(Harmony harmony, GameObject host)
    {
        _instance = host.AddComponent<HostAuthority>();

        var kick = AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.KickPlayer));
        var ban = AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.BanPlayer));
        var popup = AccessTools.Method(typeof(MenuManager), nameof(MenuManager.PagePopUpScheduled));
        if (kick == null || ban == null || popup == null)
        {
            Plugin.Log.LogWarning("Kick/ban hooks not found (game update?); kick, ban and password checks are off in Haulnet games.");
            return;
        }

        harmony.Patch(kick, postfix: new HarmonyMethod(typeof(HostAuthority), nameof(AfterKick)));
        harmony.Patch(ban, postfix: new HarmonyMethod(typeof(HostAuthority), nameof(AfterBan)));
        harmony.Patch(popup, prefix: new HarmonyMethod(typeof(HostAuthority), nameof(BeforeScheduledPopup)));
    }

    /// <summary>Forget bans of the previous game. Called on session reset.</summary>
    public static void Reset() => Banned.Clear();

    private static bool Active => Session.Current.Kind != TargetKind.Cloud && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }

    public override void OnDisable()
    {
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        base.OnDisable();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!Active || newPlayer.IsLocal)
        {
            return;
        }

        if (Banned.Contains(Identity(newPlayer)))
        {
            Plugin.Log.LogInfo($"Refusing banned player {newPlayer.NickName}.");
            Remove(newPlayer, ReasonBanned);
            return;
        }

        var required = PhotonNetwork.CurrentRoom.CustomProperties["PASSWORD"] as string ?? "";
        if (required.Length == 0)
        {
            return;
        }

        var given = newPlayer.CustomProperties["PASSWORD"] as string ?? "";
        if (given != required)
        {
            Plugin.Log.LogInfo($"Refusing {newPlayer.NickName}: wrong lobby password.");
            Remove(newPlayer, ReasonWrongPassword);
        }
    }

    private static void AfterKick(Player _playerAvatar)
    {
        if (Active && _playerAvatar != null && !_playerAvatar.IsLocal)
        {
            Plugin.Log.LogInfo($"Kicking {_playerAvatar.NickName}.");
            Remove(_playerAvatar, 0);
        }
    }

    private static void AfterBan(Player _playerAvatar)
    {
        if (Active && _playerAvatar != null && !_playerAvatar.IsLocal)
        {
            Banned.Add(Identity(_playerAvatar));
            Plugin.Log.LogInfo($"Banning {_playerAvatar.NickName} for this game.");
            Remove(_playerAvatar, ReasonBanned);
        }
    }

    private static string Identity(Player player) =>
        !string.IsNullOrEmpty(player.UserId) ? "id:" + player.UserId : "name:" + player.NickName;

    private static void Remove(Player player, int reason)
    {
        var target = new RaiseEventOptions { TargetActors = new[] { player.ActorNumber } };
        if (reason != 0)
        {
            PhotonNetwork.RaiseEvent(ReasonEvent, reason, target, SendOptions.SendReliable);
        }

        PhotonNetwork.RaiseEvent(KickedEvent, null, target, SendOptions.SendReliable);
        _instance?.StartCoroutine(CloseIfStillThere(player));
    }

    /// <summary>The game leaves the room on event 199; close the connection if it has not after a moment.</summary>
    private static IEnumerator CloseIfStillThere(Player player)
    {
        yield return new WaitForSecondsRealtime(3f);
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.Players.ContainsKey(player.ActorNumber))
        {
            Plugin.Log.LogWarning($"{player.NickName} did not leave after removal; closing the connection.");
            PhotonNetwork.CloseConnection(player);
        }
    }

    /// <summary>Player side: remember why the host removes us, to show it instead of the generic text.</summary>
    private static void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != ReasonEvent || PhotonNetwork.MasterClient == null || photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber)
        {
            return;
        }

        if (photonEvent.CustomData is int reason)
        {
            _pendingReason = reason;
        }
    }

    /// <summary>Replaces the game's "Kicked" popup text when the host told us the reason.</summary>
    private static void BeforeScheduledPopup(ref string headerText, ref string bodyText)
    {
        var reason = _pendingReason;
        if (reason == 0)
        {
            return;
        }

        _pendingReason = 0;
        if (reason == ReasonWrongPassword)
        {
            headerText = Text.WrongPasswordHeader;
            bodyText = Text.WrongPasswordBody;
        }
        else if (reason == ReasonBanned)
        {
            headerText = Text.BannedHeader;
            bodyText = Text.BannedBody;
        }
    }
}
