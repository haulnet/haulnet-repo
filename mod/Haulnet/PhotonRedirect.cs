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
                server.Start();
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
            return;
        }

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
