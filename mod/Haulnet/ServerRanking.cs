using System;
using System.Collections.Generic;
using System.Linq;

namespace Haulnet;

/// <summary>A usable server of a city as the ranking sees it.</summary>
internal readonly struct RankedServer
{
    public RankedServer(int pingMs, int penaltyMs, int players)
    {
        PingMs = pingMs;
        PenaltyMs = penaltyMs;
        Players = players;
    }

    /// <summary>Round trip measured from this PC, or -1 when unknown.</summary>
    public int PingMs { get; }

    /// <summary>Stability penalty from the registry monitor (<see cref="ServerRanking.Penalty"/>).</summary>
    public int PenaltyMs { get; }

    public int Players { get; }

    /// <summary>Lower is better: the ping plus the stability penalty.</summary>
    public int Score => ServerRanking.Score(PingMs, PenaltyMs);
}

/// <summary>What the region page found for one city row.</summary>
internal enum CityOutcome
{
    /// <summary>A server was chosen.</summary>
    Usable,

    /// <summary>No usable server because every server of the city is about to update.</summary>
    Updating,

    /// <summary>No usable server (down, full, incompatible, or no answer in time).</summary>
    None,
}

/// <summary>
/// Orders servers and cities by quality (ADR 0024): closest and most stable first. Pure logic without game types, so
/// the rules are unit-tested. "Stable" comes from the registry monitor's uptime; the ping is this PC's own.
/// </summary>
internal static class ServerRanking
{
    /// <summary>A server within this many ms (of score, or of penalty for public games) of the best one counts as equal.</summary>
    public const int SameMs = 15;

    /// <summary>Penalty in ms for each 1 % of failed monitor checks: 99 % uptime costs as much as 20 ms of ping.</summary>
    public const int PenaltyPerPercent = 20;

    /// <summary>Cities whose scores fall in the same bucket keep the list's order, so jitter does not reshuffle rows.</summary>
    public const int BucketMs = 10;

    // Stands in for an unknown ping; far worse than any real one, small enough to add a penalty to.
    private const int UnknownPingMs = 100_000;

    /// <summary>
    /// Stability penalty from the monitor's uptime shares (0..1) over 24 hours and 7 days. The worse of the two counts,
    /// so an outage today weighs at once and a bad week is not forgotten after a good day. No data: no penalty
    /// (a new server, or the monitor is not reachable).
    /// </summary>
    public static int Penalty(double? uptime24h, double? uptime7d)
    {
        var values = new[] { uptime24h, uptime7d }.Where(v => v.HasValue && !double.IsNaN(v.Value)).Select(v => v!.Value).ToList();
        if (values.Count == 0)
        {
            return 0;
        }

        var worst = Math.Max(0.0, Math.Min(1.0, values.Min()));
        return (int)Math.Round((1.0 - worst) * 100.0 * PenaltyPerPercent);
    }

    public static int Score(int pingMs, int penaltyMs) => (pingMs >= 0 ? pingMs : UnknownPingMs) + Math.Max(0, penaltyMs);

    /// <summary>
    /// Index of the server a game in this city goes to, or -1 when <paramref name="usable"/> is empty.
    /// Private game (<paramref name="hostFlow"/>): the least loaded of the servers within <see cref="SameMs"/> of the
    /// best score. Public game: the busiest of the servers within <see cref="SameMs"/> of the lowest penalty, so
    /// public players of a city meet on one server; the penalty is the same for every player, the ping is not.
    /// </summary>
    public static int Choose(IReadOnlyList<RankedServer> usable, bool hostFlow)
    {
        if (usable.Count == 0)
        {
            return -1;
        }

        var indexed = usable.Select((server, index) => (server, index)).ToList();
        if (hostFlow)
        {
            var best = indexed.Min(x => x.server.Score);
            return indexed
                .Where(x => x.server.Score <= best + SameMs)
                .OrderBy(x => x.server.Players)
                .ThenBy(x => x.server.Score)
                .ThenBy(x => x.index)
                .First().index;
        }

        var steadiest = indexed.Min(x => x.server.PenaltyMs);
        return indexed
            .Where(x => x.server.PenaltyMs <= steadiest + SameMs)
            .OrderByDescending(x => x.server.Players)
            .ThenBy(x => x.server.Score)
            .ThenBy(x => x.index)
            .First().index;
    }

    /// <summary>
    /// Display order of city rows, as indexes into <paramref name="cities"/> (given in the list's order). Cities with a
    /// server first, by the chosen server's score in <see cref="BucketMs"/> buckets; then cities that are updating;
    /// then cities without a server. Ties keep the given order.
    /// </summary>
    public static List<int> OrderCities(IReadOnlyList<(CityOutcome outcome, int score)> cities) =>
        cities
            .Select((city, index) => (city, index))
            .OrderBy(x => (int)x.city.outcome)
            .ThenBy(x => x.city.outcome == CityOutcome.Usable ? x.city.score / BucketMs : 0)
            .ThenBy(x => x.index)
            .Select(x => x.index)
            .ToList();
}
