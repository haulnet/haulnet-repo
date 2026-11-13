using System;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace Haulnet;

internal enum TargetKind
{
    /// <summary>Photon Cloud, the unmodded behaviour.</summary>
    Cloud,

    /// <summary>The server inside this game.</summary>
    Host,

    /// <summary>A Haulnet host or dedicated server at <see cref="ConnectionTarget.Address"/>.</summary>
    Remote,
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
    public string Label { get; }
}

/// <summary>
/// Decides, per connection, where the game connects. There is no global mode:
/// <list type="bullet">
/// <item>the host chooses "On my PC" or "Photon Cloud" when creating a private game;</item>
/// <item>a player who joins through a Steam invite follows the Haulnet data in that lobby;</item>
/// <item>a server address from the F8 panel applies to the next connection without a lobby (Public game);</item>
/// <item>everything else is Photon Cloud.</item>
/// </list>
/// The state resets whenever the main menu opens.
/// </summary>
internal static class Session
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1.5);

    private static ulong _resolvedLobbyId;
    private static ConnectionTarget? _resolvedTarget;

    /// <summary>The player chose "On my PC" for the game being created.</summary>
    public static bool HostingOnMyPc { get; private set; }

    /// <summary>Server address set in the F8 panel for the next lobby-less connection.</summary>
    public static ServerAddress? ManualAddress { get; private set; }

    /// <summary>Target of the most recent connection, for the lobby status line and the panel.</summary>
    public static ConnectionTarget Current { get; private set; } = ConnectionTarget.Cloud;

    public static void ChooseOnMyPc()
    {
        HostingOnMyPc = true;
        Plugin.Settings.LastChoiceOnMyPc = true;
        Plugin.Server.Start();
        Plugin.Probe.StartResponding();
    }

    public static void ChooseCloud()
    {
        HostingOnMyPc = false;
        Plugin.Settings.LastChoiceOnMyPc = false;
        StopHosting();
    }

    public static void SetManualAddress(ServerAddress? address)
    {
        ManualAddress = address;
        if (address.HasValue)
        {
            Plugin.Settings.LastManualAddress = address.Value.ToString();
        }
    }

    /// <summary>Back to the unmodded state. Called when the main menu opens.</summary>
    public static void Reset()
    {
        if (HostingOnMyPc || ManualAddress.HasValue || Current.Kind != TargetKind.Cloud)
        {
            Plugin.Log.LogInfo("Main menu: session reset, back to Photon Cloud.");
        }

        HostingOnMyPc = false;
        ManualAddress = null;
        Current = ConnectionTarget.Cloud;
        _resolvedLobbyId = 0;
        _unreachableHost = null;
        _resolvedTarget = null;
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

        if (inForeignLobby)
        {
            return ResolveFromLobby(lobby);
        }

        if (HostingOnMyPc)
        {
            ServerAddress.TryParse("127.0.0.1", out var loopback, out _);
            return new ConnectionTarget(TargetKind.Host, loopback, "");
        }

        if (ManualAddress.HasValue)
        {
            return new ConnectionTarget(TargetKind.Remote, ManualAddress.Value, "");
        }

        return ConnectionTarget.Cloud;
    }

    private static ConnectionTarget ResolveFromLobby(Steamworks.Data.Lobby lobby)
    {
        if (_resolvedTarget != null && _resolvedLobbyId == lobby.Id.Value)
        {
            return _resolvedTarget; // The game reconnects several times per join; probe only once.
        }

        var info = LobbyData.Read(lobby);
        ConnectionTarget target;
        if (info == null)
        {
            target = ConnectionTarget.Cloud; // The host plays through Photon Cloud.
        }
        else if (info.Addresses.Count == 0)
        {
            Plugin.Log.LogError("The host uses Haulnet but published no addresses.");
            _unreachableHost = OwnerName(lobby);
            target = ConnectionTarget.Cloud;
        }
        else
        {
            if (info.Protocol != HostProbe.ProtocolVersion)
            {
                Plugin.Log.LogWarning($"Host uses Haulnet protocol {info.Protocol}, this game uses {HostProbe.ProtocolVersion}.");
            }

            var probe = HostProbe.FindReachable(info.Addresses.Select(a => a.Address).ToList(), ProbeTimeout);
            PublishedAddress chosen;
            string label;
            if (probe != null)
            {
                chosen = info.Addresses.FirstOrDefault(a => a.Address.Equals(probe.Address)) ?? new PublishedAddress(probe.Address, "");
                label = chosen.Label;
                Plugin.Log.LogInfo($"Host reachable at {probe.Address} ({chosen.Label}), probe {probe.RoundTripMs} ms.");
            }
            else
            {
                // No answer from any address: stop here with a clear message (see JoinGuard) instead of
                // letting the game wait for a connection timeout and show a generic error.
                chosen = info.Addresses[0];
                label = Text.LabelUnverified;
                Plugin.Log.LogWarning("No host address answered the reachability probe; the join will be cancelled.");
                _unreachableHost = OwnerName(lobby);
            }

            var text = chosen.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{chosen.Address}]"
                : chosen.Address.ToString();
            ServerAddress.TryParse(text, out var address, out _);
            target = new ConnectionTarget(TargetKind.Remote, address, label);
        }

        _resolvedLobbyId = lobby.Id.Value;
        _resolvedTarget = target;
        return target;
    }

    private static string? _unreachableHost;

    /// <summary>
    /// True once after a join was resolved to a host that did not answer; returns the host's Steam name.
    /// </summary>
    public static bool TakeUnreachable(out string hostName)
    {
        hostName = _unreachableHost ?? "";
        if (_unreachableHost == null)
        {
            return false;
        }

        _unreachableHost = null;
        _resolvedTarget = null; // Try again on the next attempt.
        return true;
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
    private static readonly Color Header = new(1f, 0.55f, 0f);

    /// <summary>Shows the popup the next time the game returns to a menu (after a failed connection).</summary>
    public static void Schedule(string header, string body)
    {
        try
        {
            MenuManager.instance?.PagePopUpScheduled(header, Header, body, Text.Ok, richText: false);
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
            MenuManager.instance?.PagePopUp(header, Header, body, Text.Ok, richText: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not show popup: {e.Message}");
        }
    }
}
