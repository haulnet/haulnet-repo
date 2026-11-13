using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SemiLink;

/// <summary>A host with an optional port, as typed by the player.</summary>
internal readonly struct ServerAddress
{
    private ServerAddress(string host, int port, bool isIPv6)
    {
        Host = host;
        Port = port;
        IsIPv6 = isIPv6;
    }

    /// <summary>Host name or IP literal, without brackets.</summary>
    public string Host { get; }

    /// <summary>Port, or 0 when the player did not specify one.</summary>
    public int Port { get; }

    public bool IsIPv6 { get; }

    /// <summary>Host in the form Photon expects inside "host:port" strings (IPv6 in brackets).</summary>
    public string HostForEndpoint => IsIPv6 ? "[" + Host + "]" : Host;

    /// <summary>
    /// Accepts "host", "host:port", "IPv4", "IPv4:port", "IPv6", "[IPv6]" and "[IPv6]:port".
    /// </summary>
    public static bool TryParse(string? text, out ServerAddress result, out string error)
    {
        result = default;
        error = "";
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = "address is empty";
            return false;
        }

        string host;
        string? portText = null;

        if (text![0] == '[')
        {
            var close = text.IndexOf(']');
            if (close < 0)
            {
                error = "missing ']' in IPv6 address";
                return false;
            }

            host = text.Substring(1, close - 1);
            var rest = text.Substring(close + 1);
            if (rest.Length > 0)
            {
                if (rest[0] != ':')
                {
                    error = "unexpected characters after ']'";
                    return false;
                }

                portText = rest.Substring(1);
            }
        }
        else
        {
            var firstColon = text.IndexOf(':');
            var lastColon = text.LastIndexOf(':');
            if (firstColon >= 0 && firstColon == lastColon)
            {
                host = text.Substring(0, lastColon);
                portText = text.Substring(lastColon + 1);
            }
            else
            {
                // No colon, or several colons without brackets: a bare IPv6 literal.
                host = text;
            }
        }

        if (host.Length == 0 || host.IndexOfAny(new[] { ' ', '/', '\\' }) >= 0)
        {
            error = $"invalid host '{host}'";
            return false;
        }

        var port = 0;
        if (portText != null &&
            (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
        {
            error = $"invalid port '{portText}'";
            return false;
        }

        var isIPv6 = IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6;
        if (host.Contains(":") && !isIPv6)
        {
            error = $"invalid IPv6 address '{host}'";
            return false;
        }

        result = new ServerAddress(host, port, isIPv6);
        return true;
    }

    public override string ToString() => Port > 0 ? $"{HostForEndpoint}:{Port}" : HostForEndpoint;
}
