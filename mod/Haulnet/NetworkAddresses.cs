using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Haulnet;

/// <summary>
/// The addresses of this PC, as Windows reports them: every unicast address of every adapter that is up, except
/// loopback, link-local and multicast. No guessing which one a friend will use: the friend's mod tries them all
/// (see <see cref="HostProbe"/>) and takes the one that answers.
/// </summary>
internal static class NetworkAddresses
{
    public static List<IPAddress> Collect()
    {
        var result = new List<IPAddress>();
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not list network adapters: {e.Message}");
            return result;
        }

        foreach (var nic in interfaces)
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            IPInterfaceProperties props;
            try
            {
                props = nic.GetIPProperties();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var unicast in props.UnicastAddresses)
            {
                var ip = unicast.Address;
                if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || IsLinkLocalV4(ip))
                {
                    continue;
                }

                if ((ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6) && !result.Contains(ip))
                {
                    result.Add(ip);
                }
            }
        }

        // IPv4 first: every Photon client handles it; IPv6 after.
        return result.OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).ToList();
    }

    private static bool IsLinkLocalV4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 169 && b[1] == 254;
    }
}
