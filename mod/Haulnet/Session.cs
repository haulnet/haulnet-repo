using System;
using System.Linq;
using System.Net;
using Steamworks;
using UnityEngine;

namespace Haulnet;

internal enum TargetKind
{
    /// <summary>Photon Cloud, the unmodded behaviour.</summary>
    Cloud,

    /// <summary>The server inside this game ("On my PC").</summary>
    Host,

    /// <summary>A Haulnet server (community or typed by address) at <see cref="ConnectionTarget.Address"/>.</summary>
    Server,

    /// <summary>A friend's PC that hosts the game ("On my PC" on their side), reached through the Steam lobby.</summary>
    FriendPc,
}

internal sealed class ConnectionTarget
{
    public static readonly ConnectionTarget Cloud = new(TargetKind.Cloud, default, "");

    public ConnectionTarget(TargetKind kind, ServerAddress address, string label)
    {
        Kind = kind;
        Address = address;
        Label = label;
    }

    public TargetKind Kind { get; }
    public ServerAddress Address { get; }

    /// <summary>Server name (Server only); may be empty.</summary>
    public string Label { get; }
}

/// <summary>
/// Decides, per connection, where the game connects. There is no global mode:
/// <list type="bullet">
/// <item>a player who creates or looks for a game picks the place on the region page (see <see cref="WherePage"/>):
/// a Photon region, "On my PC" or a Haulnet server; the choice lasts until the region page or the main menu opens
/// again;</item>
/// <item>a player in someone else's Steam lobby (invite, "Join game", public room) follows the Haulnet data in it;</item>
/// <item>everything else is Photon Cloud.</item>
/// </list>
/// </summary>
internal static class Session
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1.5);

    private static ConnectionTarget _chosen = ConnectionTarget.Cloud;
    private static ulong _resolvedLobbyId;
    private static ConnectionTarget? _resolvedTarget;

    /// <summary>The player chose "On my PC" for the game being created.</summary>
    public static bool HostingOnMyPc => _chosen.Kind == TargetKind.Host;

    /// <summary>Target of the most recent connection, for the lobby status line and the host's checks.</summary>
    public static ConnectionTarget Current { get; private set; } = ConnectionTarget.Cloud;

    /// <summary>Steam name of the lobby owner when the current target came from their lobby.</summary>
    public static string LobbyOwnerName { get; private set; } = "";

    /// <summary>All addresses and the port the friend's PC published, for the failure message.</summary>
    public static string FriendAddresses { get; private set; } = "";

    public static void ChooseOnMyPc(int port)
    {
        var loopback = ServerAddress.FromIp(IPAddress.Loopback, port);
        _chosen = new ConnectionTarget(TargetKind.Host, loopback, "");
        Plugin.Server.Start(port);
        Plugin.Probe.StartResponding();
        Plugin.Log.LogInfo($"Where to play: on this PC, UDP {port}-{port + 2}.");
    }

    public static void ChooseServer(ServerAddress address, string name)
    {
        StopHosting();
        _chosen = new ConnectionTarget(TargetKind.Server, address, name);
        Plugin.Log.LogInfo($"Where to play: server {address}{(name.Length > 0 ? " (" + name + ")" : "")}.");
    }

    /// <summary>Photon Cloud, or nothing chosen yet. Called when the region page opens.</summary>
    public static void ChooseCloud()
    {
        if (_chosen.Kind != TargetKind.Cloud)
        {
            Plugin.Log.LogInfo("Where to play: choice cleared (Photon Cloud unless chosen again).");
        }

        _chosen = ConnectionTarget.Cloud;
        StopHosting();
    }

    /// <summary>Back to the unmodded state. Called when the main menu opens.</summary>
    public static void Reset()
    {
        if (_chosen.Kind != TargetKind.Cloud || Current.Kind != TargetKind.Cloud)
        {
            Plugin.Log.LogInfo("Main menu: session reset, back to Photon Cloud.");
        }

        _chosen = ConnectionTarget.Cloud;
        Current = ConnectionTarget.Cloud;
        _resolvedLobbyId = 0;
        _resolvedTarget = null;
        LobbyOwnerName = "";
        FriendAddresses = "";
        HostAuthority.Reset();
        StopHosting();
    }

    /// <summary>Called right before the game connects to Photon. Never throws.</summary>
    public static ConnectionTarget ResolveForConnect()
    {
        try
        {
            Current = Resolve();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not decide where to connect, using Photon Cloud: {e}");
            Current = ConnectionTarget.Cloud;
        }

        return Current;
    }

    private static ConnectionTarget Resolve()
    {
        var lobby = LobbyData.CurrentLobby();
        var inForeignLobby = SteamClient.IsValid && lobby.Id.IsValid && !lobby.IsOwnedBy(SteamClient.SteamId);
        return inForeignLobby ? ResolveFromLobby(lobby) : _chosen;
    }

    private static ConnectionTarget ResolveFromLobby(Steamworks.Data.Lobby lobby)
    {
        if (_resolvedTarget != null && _resolvedLobbyId == lobby.Id.Value)
        {
            return _resolvedTarget; // The game reconnects several times per join; probe only once.
        }

        LobbyOwnerName = OwnerName(lobby);
        FriendAddresses = "";
        var info = LobbyData.Read(lobby);
        ConnectionTarget target;
        if (info == null)
        {
            target = ConnectionTarget.Cloud; // The game lives in Photon Cloud.
        }
        else
        {
            if (info.Protocol > LobbyData.Protocol)
            {
                Plugin.Log.LogWarning($"The host uses Haulnet lobby protocol {info.Protocol}, this mod knows {LobbyData.Protocol}; trying anyway.");
            }

            target = info.Server.HasValue
                ? new ConnectionTarget(TargetKind.Server, info.Server.Value, "")
                : ResolveFriendPc(info);
        }

        _resolvedLobbyId = lobby.Id.Value;
        _resolvedTarget = target;
        Plugin.Log.LogInfo($"Following the Steam lobby of {LobbyOwnerName}: {target.Kind} {target.Address}.");
        return target;
    }

    /// <summary>
    /// The game runs on the friend's PC: use the published address that answers the probe first. When none
    /// answers (the probe uses UDP 47777, which the host may not have opened), connect to the first one anyway;
    /// if that fails too, the player sees exactly which addresses and port were tried.
    /// </summary>
    private static ConnectionTarget ResolveFriendPc(HostInfo info)
    {
        var port = info.Port > 0 ? info.Port : ServerAddress.DefaultPort;
        FriendAddresses = string.Join(", ", info.Addresses.Select(a => ServerAddress.FromIp(a, 0).Display)) + ", UDP " + port;
        if (info.Addresses.Count == 0)
        {
            Plugin.Log.LogError("The host uses Haulnet but published no addresses.");
            return ConnectionTarget.Cloud;
        }

        var probe = HostProbe.FindReachable(info.Addresses.ToList(), ProbeTimeout);
        IPAddress chosen;
        if (probe != null)
        {
            chosen = probe.Address;
            Plugin.Log.LogInfo($"Host answered at {probe.Address}, probe {probe.RoundTripMs} ms.");
        }
        else
        {
            chosen = info.Addresses[0];
            Plugin.Log.LogWarning($"No host address answered the probe on UDP {HostProbe.Port}; connecting to {chosen} anyway.");
        }

        return new ConnectionTarget(TargetKind.FriendPc, ServerAddress.FromIp(chosen, info.Port), "");
    }

    private static string OwnerName(Steamworks.Data.Lobby lobby)
    {
        try
        {
            return lobby.Owner.Name ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static void StopHosting()
    {
        Plugin.Server.Stop();
        Plugin.Probe.StopResponding();
    }
}

/// <summary>Shows messages through the game's own popup pages.</summary>
internal static class Popups
{
    public static readonly Color Accent = new(1f, 0.55f, 0f);

    /// <summary>Shows the popup the next time the game returns to a menu (after a failed connection).</summary>
    public static void Schedule(string header, string body)
    {
        try
        {
            MenuManager.instance?.PagePopUpScheduled(header, Accent, body, Text.Ok, richText: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not schedule popup: {e.Message}");
        }
    }

    public static void ShowNow(string header, string body)
    {
        try
        {
            MenuManager.instance?.PagePopUp(header, Accent, body, Text.Ok, richText: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not show popup: {e.Message}");
        }
    }
}
