using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Haulnet;

/// <summary>A host with an optional port, as typed by the player.</summary>
internal readonly struct ServerAddress
{
    /// <summary>NameServer port R.E.P.O. uses (Photon "alternative UDP ports"); Master and Game follow it.</summary>
    public const int DefaultPort = 27000;

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

    /// <summary>The port to connect to: the given one, or <see cref="DefaultPort"/>.</summary>
    public int EffectivePort => Port > 0 ? Port : DefaultPort;

    /// <summary>True when the server uses ports other than the game's default 27000-27002.</summary>
    public bool HasCustomPort => Port > 0 && Port != DefaultPort;

    public static ServerAddress FromIp(IPAddress ip, int port) =>
        new(ip.ToString(), port, ip.AddressFamily == AddressFamily.InterNetworkV6);

    /// <summary>Host in the form Photon expects inside "host:port" strings (IPv6 in brackets).</summary>
    public string HostForEndpoint => IsIPv6 ? "[" + Host + "]" : Host;

    /// <summary>
    /// Accepts "host", "host:port", "IPv4", "IPv4:port", "IPv6", "[IPv6]" and "[IPv6]:port".
    /// </summary>
    public static bool TryParse(string? text, out ServerAddress result, out AddressError error)
    {
        result = default;
        error = default;
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = new AddressError(AddressProblem.Empty, "");
            return false;
        }

        string host;
        string? portText = null;

        if (text![0] == '[')
        {
            var close = text.IndexOf(']');
            if (close < 0)
            {
                error = new AddressError(AddressProblem.MissingBracket, "");
                return false;
            }

            host = text.Substring(1, close - 1);
            var rest = text.Substring(close + 1);
            if (rest.Length > 0)
            {
                if (rest[0] != ':')
                {
                    error = new AddressError(AddressProblem.AfterBracket, rest);
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
            error = new AddressError(AddressProblem.InvalidHost, host);
            return false;
        }

        var port = 0;
        if (portText != null &&
            (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
        {
            error = new AddressError(AddressProblem.InvalidPort, portText);
            return false;
        }

        var isIPv6 = IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6;
        if (host.Contains(":") && !isIPv6)
        {
            error = new AddressError(AddressProblem.InvalidIPv6, host);
            return false;
        }

        result = new ServerAddress(host, port, isIPv6);
        return true;
    }

    public override string ToString() => Port > 0 ? $"{HostForEndpoint}:{Port}" : HostForEndpoint;

    /// <summary>Short form for display: the port only when it is not the default.</summary>
    public string Display => HasCustomPort ? $"{HostForEndpoint}:{Port}" : HostForEndpoint;

    /// <summary>Same server, case-insensitive host, default port equal to 27000.</summary>
    public bool SameAs(ServerAddress other) =>
        string.Equals(Host, other.Host, System.StringComparison.OrdinalIgnoreCase) && EffectivePort == other.EffectivePort;
}

internal enum AddressProblem
{
    None,
    Empty,
    MissingBracket,
    AfterBracket,
    InvalidHost,
    InvalidPort,
    InvalidIPv6,
}

/// <summary>Why a text is not a server address; <see cref="Value"/> is the offending part. Shown through Text.</summary>
internal readonly struct AddressError
{
    public AddressError(AddressProblem problem, string value)
    {
        Problem = problem;
        Value = value;
    }

    public AddressProblem Problem { get; }
    public string Value { get; }

    /// <summary>English, for the log.</summary>
    public override string ToString() => Problem switch
    {
        AddressProblem.Empty => "address is empty",
        AddressProblem.MissingBracket => "missing ']' in IPv6 address",
        AddressProblem.AfterBracket => "unexpected characters after ']'",
        AddressProblem.InvalidHost => $"invalid host '{Value}'",
        AddressProblem.InvalidPort => $"invalid port '{Value}'",
        AddressProblem.InvalidIPv6 => $"invalid IPv6 address '{Value}'",
        _ => "",
    };
}
