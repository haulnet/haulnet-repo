using System;
using HarmonyLib;
using Photon.Pun;

namespace Haulnet;

/// <summary>
/// Shows the real round-trip time to the self-hosted server on the game's region page.
/// </summary>
/// <remarks>
/// Photon measures region pings with its own ping packets, which LuxonServer does not answer, so every
/// region ends up at Photon's per-attempt timeout (800 ms). The region page (<c>MenuPageRegions</c>, game
/// code) reads <c>Region.Ping</c> right after <c>OnConnectedToMaster</c>; at that moment the client is
/// connected to our server, and the connection's round-trip time is the ping the player will actually get.
/// Cosmetic only: if the hook is missing after a game update, the page shows the old values.
/// </remarks>
internal static class RegionPing
{
    public static void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(MenuPageRegions), nameof(MenuPageRegions.OnConnectedToMaster));
        if (target == null)
        {
            Plugin.Log.LogWarning("MenuPageRegions.OnConnectedToMaster not found (game update?); region ping will show placeholder values.");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(typeof(RegionPing), nameof(AfterConnectedToMaster)));
    }

    private static void AfterConnectedToMaster()
    {
        if (!PhotonRedirect.IsActive)
        {
            return; // Photon Cloud: keep the game's real region pings.
        }

        try
        {
            var client = PhotonNetwork.NetworkingClient;
            var rtt = Math.Max(1, client.LoadBalancingPeer.RoundTripTime);
            var regions = client.RegionHandler?.EnabledRegions;
            if (regions == null)
            {
                return;
            }

            foreach (var region in regions)
            {
                region.Ping = rtt;
            }

            Plugin.Log.LogInfo($"Region page: ping to the server is {rtt} ms.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not set region ping: {e.Message}");
        }
    }
}
