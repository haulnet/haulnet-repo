using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HarmonyLib;
using Steamworks.Data;

namespace Haulnet;

/// <summary>A host address published in the Steam lobby, with the label the host's mod gave it.</summary>
internal sealed class PublishedAddress
{
    public PublishedAddress(IPAddress address, string label)
    {
        Address = address;
        Label = label;
    }

    public IPAddress Address { get; }
    public string Label { get; }
}

/// <summary>What a Haulnet host writes into the game's Steam lobby.</summary>
internal sealed class HostInfo
{
    public HostInfo(int protocol, IReadOnlyList<PublishedAddress> addresses)
    {
        Protocol = protocol;
        Addresses = addresses;
    }

    public int Protocol { get; }
    public IReadOnlyList<PublishedAddress> Addresses { get; }
}

/// <summary>
/// Haulnet keys in the game's Steam lobby data. The game already links the Steam lobby to the Photon room
/// (keys <c>RoomName</c>, <c>Region</c>, <c>BuildName</c>); Haulnet adds where the server is, so invited
/// friends follow the host automatically.
/// </summary>
/// <remarks>
/// Keys: <c>haulnet</c> = protocol version; <c>haulnet_addr</c> = <c>ip=label;ip=label</c>.
/// Lobby data is visible to everyone who can see the lobby (friends for private games).
/// </remarks>
internal static class LobbyData
{
    private const string KeyProtocol = "haulnet";
    private const string KeyAddresses = "haulnet_addr";

    // SteamLobbyManager.currentLobby is internal in the game.
    private static readonly AccessTools.FieldRef<SteamLobbyManager, Lobby> CurrentLobbyRef =
        AccessTools.FieldRefAccess<SteamLobbyManager, Lobby>("currentLobby");

    /// <summary>The game's current Steam lobby, or default when there is none.</summary>
    public static Lobby CurrentLobby()
    {
        var manager = SteamLobbyManager.instance;
        return manager != null ? CurrentLobbyRef(manager) : default;
    }

    public static void Write(Lobby lobby, IEnumerable<AddressHint> hints)
    {
        var encoded = string.Join(";", hints.Select(h => h.Address + "=" + Sanitize(h.Label)));
        lobby.SetData(KeyProtocol, HostProbe.ProtocolVersion.ToString());
        lobby.SetData(KeyAddresses, encoded);
        Plugin.Log.LogInfo($"Steam lobby: published {hints.Count()} host address(es).");
    }

    public static void Clear(Lobby lobby)
    {
        if (!string.IsNullOrEmpty(lobby.GetData(KeyProtocol)))
        {
            lobby.DeleteData(KeyProtocol);
            lobby.DeleteData(KeyAddresses);
        }
    }

    public static HostInfo? Read(Lobby lobby)
    {
        var protocolText = lobby.GetData(KeyProtocol);
        if (string.IsNullOrEmpty(protocolText) || !int.TryParse(protocolText, out var protocol))
        {
            return null;
        }

        var addresses = new List<PublishedAddress>();
        foreach (var entry in (lobby.GetData(KeyAddresses) ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = entry.IndexOf('=');
            var ipText = split >= 0 ? entry.Substring(0, split) : entry;
            var label = split >= 0 ? entry.Substring(split + 1) : "";
            if (IPAddress.TryParse(ipText, out var ip) && addresses.Count < 16)
            {
                addresses.Add(new PublishedAddress(ip, label));
            }
        }

        return new HostInfo(protocol, addresses);
    }

    private static string Sanitize(string label) => label.Replace(";", ",").Replace("=", "-");
}
