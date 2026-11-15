using System.Globalization;
using System.Text.RegularExpressions;

namespace Haulnet;

/// <summary>What a quick look at a server found. Every field is optional except <see cref="Resolved"/>.</summary>
internal sealed class ServerStatus
{
    public bool Resolved { get; set; }

    /// <summary>Round trip in ms (ICMP ping, or the TCP connect to the status endpoint), or -1.</summary>
    public int PingMs { get; set; } = -1;

    /// <summary>
    /// The machine reported the game's UDP port unreachable: no server runs there, whatever the ping says. False
    /// also when the answer is unknown (a firewall dropped the probe).
    /// </summary>
    public bool GamePortClosed { get; set; }

    /// <summary>The haulnet-server status endpoint answered with 200.</summary>
    public bool HasStatus { get; set; }

    public string Name { get; set; } = "";
    public string Luxon { get; set; } = "";
    public bool Accepting { get; set; } = true;

    /// <summary>
    /// haulnet-server 0.1.0+: an update waits for the running games to end, so the server starts no new ones
    /// (it then also says accepting: false). Older servers do not send the field.
    /// </summary>
    public bool Draining { get; set; }

    public int Players { get; set; }
    public int Rooms { get; set; }

    /// <summary>Not accepting new games because it is about to update, not because it is full or closed.</summary>
    public bool Updating => HasStatus && !Accepting && Draining;

    /// <summary>
    /// Reads the body of <c>GET /status</c>. The object is flat and its fields are fixed (haulnet-server contract),
    /// so a full JSON parser is not needed. A missing field keeps its default; the name is returned as sent.
    /// </summary>
    public static ServerStatus Parse(string body) => new()
    {
        HasStatus = true,
        Name = JsonString(body, "name"),
        Luxon = JsonString(body, "luxon"),
        Accepting = !JsonFalse(body, "accepting"),
        Draining = JsonTrue(body, "draining"),
        Players = JsonInt(body, "players"),
        Rooms = JsonInt(body, "rooms"),
    };

    private static bool JsonFalse(string json, string key) => Regex.IsMatch(json, "\"" + key + "\"\\s*:\\s*false");

    private static bool JsonTrue(string json, string key) => Regex.IsMatch(json, "\"" + key + "\"\\s*:\\s*true");

    private static string JsonString(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        return match.Success ? Regex.Unescape(match.Groups[1].Value) : "";
    }

    private static int JsonInt(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d{1,9})");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
