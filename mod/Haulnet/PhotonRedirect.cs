using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;

namespace Haulnet;

/// <summary>
/// Points the game's Photon connection at the server chosen by the player instead of Photon Cloud.
/// </summary>
/// <remarks>
/// Two hooks:
/// <list type="number">
/// <item>
/// Postfix on <c>DataDirector.PhotonSetAppId()</c> (game code). The game calls it right before every
/// <c>PhotonNetwork.ConnectUsingSettings()</c> and resets <c>AppSettings.Server</c>/<c>Port</c> there, so
/// this is the one place where our server address reliably survives until the connect.
/// </item>
/// <item>
/// Prefix on the public <c>LoadBalancingClient</c> connect methods. They install Photon's public
/// <c>AddressRewriter</c> on every client that connects, including the Photon Voice client: the
/// server hands out Master and Game server addresses configured on the host (often 127.0.0.1), and the
/// rewriter replaces their host part with the address this player used, keeping the port.
/// The same prefix withholds the player's Steam ticket from a self-hosted server (see
/// <see cref="WithholdTicket"/>).
/// </item>
/// </list>
/// Where to connect is decided by <see cref="Session"/>; for Photon Cloud both hooks leave the game's values
/// untouched.
/// </remarks>
internal static class PhotonRedirect
{
    private static readonly Func<string, ServerConnection, string> Rewriter = RewriteAddress;
    private static readonly HashSet<string> LoggedRewrites = new();

    // Photon clients live for the whole game session (PUN's client and the voice client), so a plain
    // dictionary does not leak in practice.
    private static readonly Dictionary<LoadBalancingClient, string> OriginalNameServerHosts = new();
    private static readonly Dictionary<LoadBalancingClient, PhotonPortDefinition> OriginalPorts = new();

    // Per client: the authentication values the game set, and the ticket-free copy the client carries instead.
    private static readonly Dictionary<LoadBalancingClient, WithheldAuth> Withheld = new();
    private static bool _loggedWithheld;

    // Ports for a server that does not use the game's 27000-27002, or null for the game's own port set.
    private static PhotonPortDefinition? _portOverride;

    private static ServerAddress _active;
    private static bool _isActive;

    /// <summary>True when the current connection goes to a self-hosted server instead of Photon Cloud.</summary>
    public static bool IsActive => _isActive;

    public static void Apply(Harmony harmony)
    {
        var setAppId = AccessTools.Method(typeof(DataDirector), nameof(DataDirector.PhotonSetAppId))
            ?? throw new MissingMethodException("DataDirector.PhotonSetAppId not found (game update?)");
        harmony.Patch(setAppId, postfix: new HarmonyMethod(typeof(PhotonRedirect), nameof(AfterPhotonSetAppId)));

        var prefix = new HarmonyMethod(typeof(PhotonRedirect), nameof(BeforeClientConnect));
        var patched = 0;
        foreach (var name in new[]
                 {
                     nameof(LoadBalancingClient.ConnectUsingSettings),
                     nameof(LoadBalancingClient.ConnectToNameServer),
                     nameof(LoadBalancingClient.ConnectToMasterServer),
                     nameof(LoadBalancingClient.ConnectToRegionMaster),
                 })
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(LoadBalancingClient)))
            {
                if (method.Name == name && !method.IsStatic)
                {
                    harmony.Patch(method, prefix: prefix);
                    patched++;
                }
            }
        }

        if (patched == 0)
        {
            throw new MissingMethodException("No LoadBalancingClient connect methods found (Photon update?)");
        }
    }

    /// <summary>Runs after the game has written Photon Cloud settings; replaces them when not using the cloud.</summary>
    private static void AfterPhotonSetAppId()
    {
        _isActive = false;
        _portOverride = null;

        var target = Session.ResolveForConnect();
        if (target.Kind == TargetKind.Cloud)
        {
            return;
        }

        if (target.Kind == TargetKind.Host)
        {
            // Normally already running (started when "On my PC" was chosen); if not, give it a moment
            // rather than letting the connect fail.
            var server = Plugin.Server;
            if (server.State != ServerState.Running)
            {
                server.Start(target.Address.EffectivePort);
                Plugin.Probe.StartResponding();
                if (!server.WaitUntilReady(TimeSpan.FromSeconds(5)))
                {
                    Plugin.Log.LogError($"The server on this PC is not running ({server.Error}); the connection will fail.");
                    Popups.Schedule(Text.ServerFailedHeader, Text.ServerFailedBody(server.Error));
                }
            }
        }

        var address = target.Address;
        var app = PhotonNetwork.PhotonServerSettings.AppSettings;
        app.Server = address.HostForEndpoint;
        // 0 lets Photon use the game's port set (AlternativeUdpPorts: NameServer on 27000).
        app.Port = address.Port;
        app.UseNameServer = true;
        if (address.HasCustomPort)
        {
            // The game forces 27000/27001/27002 through ServerPortOverrides. For another port, connect to the
            // NameServer on that port and take the Master and Game ports from the server's answers (0 = no override).
            _portOverride = new PhotonPortDefinition { NameServerPort = (ushort)address.Port };
            PhotonNetwork.ServerPortOverrides = _portOverride.Value;
        }

        _active = address;
        _isActive = true;
        Plugin.Log.LogInfo($"{target.Kind}: connecting to {address} instead of Photon Cloud.");
    }

    /// <summary>
    /// Right before a Photon client connects: install the address rewriter for a self-hosted server, or
    /// undo everything for Photon Cloud.
    /// </summary>
    /// <remarks>
    /// Photon keeps <c>NameServerHost</c> from the last connection: <c>ConnectUsingSettings</c> sets it when
    /// <c>AppSettings.Server</c> is filled, but does not restore the cloud default when it is empty again. So
    /// the original value is remembered per client before the first redirect and put back for cloud
    /// connections; otherwise a Photon Cloud game after a Haulnet game would still go to the old server.
    /// </remarks>
    private static void BeforeClientConnect(LoadBalancingClient __instance)
    {
        if (_isActive)
        {
            if (!OriginalNameServerHosts.ContainsKey(__instance))
            {
                OriginalNameServerHosts[__instance] = __instance.NameServerHost;
            }

            __instance.AddressRewriter = Rewriter;
            if (_portOverride.HasValue)
            {
                if (!OriginalPorts.ContainsKey(__instance))
                {
                    OriginalPorts[__instance] = __instance.ServerPortOverrides;
                }

                __instance.ServerPortOverrides = _portOverride.Value;
            }
            else
            {
                RestorePorts(__instance);
            }

            WithholdTicket(__instance);
            return;
        }

        RestorePorts(__instance);
        RestoreAuthValues(__instance);

        if (__instance.AddressRewriter == Rewriter)
        {
            __instance.AddressRewriter = null;
        }

        if (OriginalNameServerHosts.TryGetValue(__instance, out var original) && __instance.NameServerHost != original)
        {
            Plugin.Log.LogInfo($"Photon Cloud: restoring NameServer host {original} (was {__instance.NameServerHost}).");
            __instance.NameServerHost = original;
        }
    }

    /// <summary>Undo a port override. The game sets PUN's own client again in PhotonSetAppId; this covers the voice client.</summary>
    private static void RestorePorts(LoadBalancingClient client)
    {
        if (OriginalPorts.TryGetValue(client, out var ports))
        {
            client.ServerPortOverrides = ports;
            OriginalPorts.Remove(client);
        }
    }

    /// <summary>
    /// Keeps the player's Steam ticket away from a self-hosted server.
    /// </summary>
    /// <remarks>
    /// Before every connect the game's <c>PlatformManager.SetPhotonAuthTicket()</c> gives Photon new
    /// authentication values: the Steam user id, the Steam authentication type and a Steam ticket as an
    /// authentication parameter. Photon Cloud needs the ticket to check the player with Steam. A self-hosted
    /// server cannot check it and takes only the user id, so the ticket would reach the server's operator for
    /// nothing. The client therefore connects with a copy that has the user id and type and no parameters. The
    /// game's own object is not changed and comes back for a Photon Cloud connection
    /// (<see cref="RestoreAuthValues"/>). On any error the game's values stay as they are.
    /// </remarks>
    private static void WithholdTicket(LoadBalancingClient client)
    {
        try
        {
            var values = client.AuthValues;
            if (values == null || string.IsNullOrEmpty(values.AuthGetParameters))
            {
                // Nothing to withhold: no ticket, or already our copy (the connect methods call each other).
                return;
            }

            var copy = new AuthenticationValues { UserId = values.UserId, AuthType = values.AuthType };
            Withheld[client] = new WithheldAuth(values, copy);
            client.AuthValues = copy;
            var kind = ReferenceEquals(client, PhotonNetwork.NetworkingClient) ? "game" : "voice";
            Plugin.Log.LogDebug($"Steam ticket withheld from the {kind} connection.");
            if (!_loggedWithheld)
            {
                _loggedWithheld = true;
                Plugin.Log.LogInfo("Steam ticket withheld: this server gets the Steam ID only.");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not withhold the Steam ticket; connecting with the game's values: {e}");
        }
    }

    /// <summary>
    /// For a Photon Cloud connection: if the client still carries our ticket-free copy, put the game's own
    /// values back. Normally the game has already set new ones and there is nothing to do.
    /// </summary>
    private static void RestoreAuthValues(LoadBalancingClient client)
    {
        if (!Withheld.TryGetValue(client, out var withheld))
        {
            return;
        }

        Withheld.Remove(client);
        if (ReferenceEquals(client.AuthValues, withheld.Copy))
        {
            client.AuthValues = withheld.Original;
            Plugin.Log.LogInfo("Photon Cloud: restoring the game's authentication values.");
        }
    }

    private readonly struct WithheldAuth
    {
        public WithheldAuth(AuthenticationValues original, AuthenticationValues copy)
        {
            Original = original;
            Copy = copy;
        }

        public AuthenticationValues Original { get; }

        public AuthenticationValues Copy { get; }
    }

    private static string RewriteAddress(string address, ServerConnection connection)
    {
        if (!_isActive || connection == ServerConnection.NameServer || string.IsNullOrEmpty(address))
        {
            return address;
        }

        var rewritten = ReplaceHost(address, _active.HostForEndpoint);
        // Region pinging rewrites the same address many times; log each mapping once.
        if (rewritten != address && LoggedRewrites.Add(address + ">" + rewritten))
        {
            Plugin.Log.LogInfo($"{connection} address {address} -> {rewritten}");
        }

        return rewritten;
    }

    /// <summary>Replaces the host in "[scheme://]host[:port][/path]", keeping everything else.</summary>
    internal static string ReplaceHost(string address, string newHost)
    {
        var schemeEnd = address.IndexOf("://", StringComparison.Ordinal);
        var hostStart = schemeEnd >= 0 ? schemeEnd + 3 : 0;

        int hostEnd;
        if (hostStart < address.Length && address[hostStart] == '[')
        {
            var close = address.IndexOf(']', hostStart);
            if (close < 0)
            {
                return address;
            }

            hostEnd = close + 1;
        }
        else
        {
            var slash = address.IndexOf('/', hostStart);
            var limit = slash >= 0 ? slash : address.Length;
            var colon = address.LastIndexOf(':', limit - 1, limit - hostStart);
            hostEnd = colon >= 0 ? colon : limit;
        }

        return address.Substring(0, hostStart) + newHost + address.Substring(hostEnd);
    }
}
