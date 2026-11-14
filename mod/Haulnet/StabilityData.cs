using System;
using System.Collections.Generic;

namespace Haulnet;

/// <summary>
/// Reads the registry monitor's live document (<c>status.json</c>, schema 1, ADR 0022): per server id the share of
/// successful checks over 24 hours and 7 days. Only the uptime is used, as a ranking penalty (ADR 0024); the monitor's
/// online/offline state is not, because it checks from one place (and over IPv4 only), while this PC's own check
/// decides whether a server is usable.
/// </summary>
internal static class StabilityData
{
    /// <summary>Penalty in ms per server id (<see cref="ServerRanking.Penalty"/>), or null when the document is not usable.</summary>
    public static Dictionary<string, int>? ParsePenalties(string text, out string error)
    {
        error = "";
        object? root;
        try
        {
            root = MiniJson.Parse(text);
        }
        catch (Exception e)
        {
            error = "not JSON: " + e.Message;
            return null;
        }

        var version = MiniJson.Int(root, "schema_version");
        if (version != 1)
        {
            error = $"schema_version {version}, expected 1";
            return null;
        }

        var penalties = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var server in MiniJson.Arr(root, "servers"))
        {
            var id = MiniJson.Str(server, "id");
            if (id.Length == 0)
            {
                continue;
            }

            var uptime = MiniJson.Obj(server, "uptime");
            penalties[id] = ServerRanking.Penalty(Share(uptime, "24h"), Share(uptime, "7d"));
        }

        return penalties;
    }

    private static double? Share(Dictionary<string, object?>? uptime, string key) =>
        uptime != null && uptime.TryGetValue(key, out var value) && value is double share && share >= 0 && share <= 1
            ? share
            : null;
}
