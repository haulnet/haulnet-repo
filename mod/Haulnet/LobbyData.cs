using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HarmonyLib;
using Steamworks.Data;

namespace Haulnet;

/// <summary>Where a Haulnet game lives, as written into the game's Steam lobby.</summary>
internal sealed class HostInfo
{
    public HostInfo(int protocol, IReadOnlyList<IPAddress> addresses, int port, ServerAddress? server)
    {
        Protocol = protocol;
        Addresses = addresses;
        Port = port;
        Server = server;
    }

    public int Protocol { get; }

    /// <summary>The host's own addresses, when the game runs on the host's PC.</summary>
    public IReadOnlyList<IPAddress> Addresses { get; }

    /// <summary>NameServer port of the host's PC (0 = default 27000).</summary>
    public int Port { get; }

    /// <summary>The server the game runs on (community or typed by address), when not on the host's PC.</summary>
    public ServerAddress? Server { get; }
}

/// <summary>
/// Haulnet keys in the game's Steam lobby data. The game already links the Steam lobby to the Photon room
/// (keys <c>RoomName</c>, <c>Region</c>, <c>BuildName</c>); Haulnet adds where the server is, so friends who accept
/// an invite or use "Join game" in Steam follow automatically. The game writes the lobby data for private games
/// (<c>NetworkConnect</c>) and public games (<c>RunManagerPUN.HostSteamLobby</c>) alike.
/// </summary>
/// <remarks>
/// Protocol 2 keys: <c>haulnet</c> = protocol version; either <c>haulnet_server</c> = <c>host:port</c> (game on a
/// server), or <c>haulnet_addr</c> = <c>ip;ip</c> plus <c>haulnet_port</c> when not 27000 (game on the host's PC).
/// Protocol 1 lobbies (<c>haulnet_addr</c> as <c>ip=label;...</c>) are still read; labels are ignored.
/// Lobby data is visible to everyone who can see the lobby (friends for private games, anyone for public ones).
/// </remarks>
internal static class LobbyData
{
    public const int Protocol = 2;

    private const string KeyProtocol = "haulnet";
    private const string KeyAddresses = "haulnet_addr";
    private const string KeyPort = "haulnet_port";
    private const string KeyServer = "haulnet_server";

    // SteamLobbyManager.currentLobby is internal in the game.
    private static readonly AccessTools.FieldRef<SteamLobbyManager, Lobby> CurrentLobbyRef =
        AccessTools.FieldRefAccess<SteamLobbyManager, Lobby>("currentLobby");

    /// <summary>The game's current Steam lobby, or default when there is none.</summary>
    public static Lobby CurrentLobby()
    {
        var manager = SteamLobbyManager.instance;
        return manager != null ? CurrentLobbyRef(manager) : default;
    }

    public static void WriteHost(Lobby lobby, IEnumerable<IPAddress> addresses, int port)
    {
        var list = addresses.ToList();
        lobby.SetData(KeyProtocol, Protocol.ToString());
        lobby.SetData(KeyAddresses, string.Join(";", list.Select(a => a.ToString())));
        SetOrDelete(lobby, KeyPort, port > 0 && port != ServerAddress.DefaultPort ? port.ToString() : "");
        SetOrDelete(lobby, KeyServer, "");
        Plugin.Log.LogInfo($"Steam lobby: published {list.Count} host address(es), port {(port > 0 ? port : ServerAddress.DefaultPort)}.");
    }

    public static void WriteServer(Lobby lobby, ServerAddress server)
    {
        lobby.SetData(KeyProtocol, Protocol.ToString());
        lobby.SetData(KeyServer, server.ToString());
        SetOrDelete(lobby, KeyAddresses, "");
        SetOrDelete(lobby, KeyPort, "");
        Plugin.Log.LogInfo($"Steam lobby: published server {server}.");
    }

    public static void Clear(Lobby lobby)
    {
        if (!string.IsNullOrEmpty(lobby.GetData(KeyProtocol)))
        {
            lobby.DeleteData(KeyProtocol);
            SetOrDelete(lobby, KeyAddresses, "");
            SetOrDelete(lobby, KeyPort, "");
            SetOrDelete(lobby, KeyServer, "");
        }
    }

    public static HostInfo? Read(Lobby lobby)
    {
        var protocolText = lobby.GetData(KeyProtocol);
        if (string.IsNullOrEmpty(protocolText) || !int.TryParse(protocolText, out var protocol))
        {
            return null;
        }

        ServerAddress? server = null;
        var serverText = lobby.GetData(KeyServer);
        if (!string.IsNullOrEmpty(serverText))
        {
            if (ServerAddress.TryParse(serverText, out var parsed, out var error))
            {
                server = parsed;
            }
            else
            {
                Plugin.Log.LogWarning($"Steam lobby: invalid server '{serverText}' ({error}).");
            }
        }

        var addresses = new List<IPAddress>();
        foreach (var entry in (lobby.GetData(KeyAddresses) ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = entry.IndexOf('=');
            var ipText = split >= 0 ? entry.Substring(0, split) : entry;
            if (IPAddress.TryParse(ipText, out var ip) && addresses.Count < 16)
            {
                addresses.Add(ip);
            }
        }

        int.TryParse(lobby.GetData(KeyPort) ?? "", out var port);
        if (port < 1 || port > 65535)
        {
            port = 0;
        }

        return new HostInfo(protocol, addresses, port, server);
    }

    private static void SetOrDelete(Lobby lobby, string key, string value)
    {
        if (value.Length > 0)
        {
            lobby.SetData(key, value);
        }
        else if (!string.IsNullOrEmpty(lobby.GetData(key)))
        {
            lobby.DeleteData(key);
        }
    }
}
