using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Haulnet;

internal enum AddressKind
{
    Vpn,
    Lan,
    IPv6,
    Public,
}

/// <summary>An address of this PC that a friend may be able to use, with a label explaining it.</summary>
internal sealed class AddressHint
{
    public AddressHint(AddressKind kind, string label, IPAddress address)
    {
        Kind = kind;
        Label = label;
        Address = address;
    }

    public AddressKind Kind { get; }
    public string Label { get; }
    public IPAddress Address { get; }

    /// <summary>Address as text (IPv6 in brackets).</summary>
    public string Text => Address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{Address}]" : Address.ToString();
}

/// <summary>Lists this PC's addresses with labels: VPN adapters, LAN, IPv6, public IPv4.</summary>
internal static class NetworkAddresses
{
    // Adapter names/descriptions of common VPNs. Matching is case-insensitive.
    private static readonly string[] VpnMarkers =
    {
        "wireguard", "wintun", "radmin", "zerotier", "tailscale", "hamachi", "openvpn", "tap-windows",
        "nordlynx", "amnezia", "outline", "softether", "netbird", "headscale",
    };

    // Virtual adapters that are almost never how a friend reaches this PC.
    private static readonly string[] IgnoredMarkers =
    {
        "vethernet", "hyper-v", "virtualbox", "vmware", "wsl", "docker", "loopback", "teredo", "isatap", "bluetooth",
    };

    public static List<AddressHint> Collect()
    {
        var result = new List<AddressHint>();
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

            var text = (nic.Name + " " + nic.Description).ToLowerInvariant();
            var vpn = VpnMarkers.FirstOrDefault(text.Contains);
            if (vpn == null && IgnoredMarkers.Any(text.Contains))
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
                if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6Multicast)
                {
                    continue;
                }

                if (vpn != null)
                {
                    // A VPN adapter's IPv6 addresses are usually internal duplicates; prefer IPv4.
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        result.Add(new AddressHint(AddressKind.Vpn, Text.LabelVpn(nic.Name), ip));
                    }

                    continue;
                }

                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (IsLinkLocalV4(ip))
                    {
                        continue;
                    }

                    result.Add(IsPrivateV4(ip)
                        ? new AddressHint(AddressKind.Lan, Text.LabelLan(nic.Name), ip)
                        : new AddressHint(AddressKind.Public, Text.LabelPublic, ip));
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && IsGlobalV6(ip))
                {
                    result.Add(new AddressHint(AddressKind.IPv6, Text.LabelIPv6, ip));
                }
            }
        }

        // Temporary IPv6 privacy addresses multiply quickly; one IPv6 hint is enough.
        var firstV6 = result.FirstOrDefault(h => h.Kind == AddressKind.IPv6);
        result.RemoveAll(h => h.Kind == AddressKind.IPv6 && !ReferenceEquals(h, firstV6));

        return result.OrderBy(h => h.Kind).ToList();
    }

    private static bool IsPrivateV4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 100 && b[1] >= 64 && b[1] <= 127); // CGNAT range, also used by some VPNs
    }

    private static bool IsLinkLocalV4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 169 && b[1] == 254;
    }

    private static bool IsGlobalV6(IPAddress ip)
    {
        var first = ip.GetAddressBytes()[0];
        return (first & 0xE0) == 0x20; // 2000::/3
    }
}
