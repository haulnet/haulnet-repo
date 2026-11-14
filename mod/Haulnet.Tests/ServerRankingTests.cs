using System.Collections.Generic;
using Xunit;

namespace Haulnet.Tests;

public class ServerRankingTests
{
    [Theory]
    [InlineData(1.0, 1.0, 0)]
    [InlineData(0.99, 1.0, 20)]
    [InlineData(1.0, 0.95, 100)]
    [InlineData(0.9, 0.99, 200)]
    [InlineData(0.0, 0.0, 2000)]
    public void PenaltyGrowsWithTheWorseUptime(double day, double week, int expected) =>
        Assert.Equal(expected, ServerRanking.Penalty(day, week));

    [Fact]
    public void NoMonitorDataIsNoPenalty()
    {
        Assert.Equal(0, ServerRanking.Penalty(null, null));
        Assert.Equal(0, ServerRanking.Penalty(double.NaN, null));
        Assert.Equal(40, ServerRanking.Penalty(null, 0.98));
    }

    [Fact]
    public void OutOfRangeSharesAreClamped()
    {
        Assert.Equal(0, ServerRanking.Penalty(1.5, null));
        Assert.Equal(2000, ServerRanking.Penalty(-1, null));
    }

    [Fact]
    public void UnknownPingScoresBelowAnyMeasuredOne() =>
        Assert.True(ServerRanking.Score(-1, 0) > ServerRanking.Score(999, 1000));

    [Fact]
    public void ChooseOnEmptyListIsMinusOne() =>
        Assert.Equal(-1, ServerRanking.Choose(new List<RankedServer>(), hostFlow: true));

    [Fact]
    public void PrivateGameTakesTheLeastLoadedOfTheClosest()
    {
        var servers = new List<RankedServer>
        {
            new(40, 0, 10), // best score, busy
            new(50, 0, 2),  // within 15 ms: least loaded wins
            new(200, 0, 0), // far
        };
        Assert.Equal(1, ServerRanking.Choose(servers, hostFlow: true));
    }

    [Fact]
    public void PrivateGameAvoidsAnUnstableServerEvenIfItIsClosest()
    {
        var servers = new List<RankedServer>
        {
            new(30, 200, 0), // 90 % uptime: score 230
            new(70, 0, 5),   // score 70
        };
        Assert.Equal(1, ServerRanking.Choose(servers, hostFlow: true));
    }

    [Fact]
    public void PublicGameTakesTheBusiestOfTheSteadiest()
    {
        var servers = new List<RankedServer>
        {
            new(30, 0, 3),
            new(90, 10, 8),  // within 15 ms of the lowest penalty, busiest: players meet here
            new(20, 300, 50), // busy but unstable
        };
        Assert.Equal(1, ServerRanking.Choose(servers, hostFlow: false));
    }

    [Fact]
    public void PublicGameIgnoresPingDifferencesBetweenPlayers()
    {
        // Two players see different pings to the same two servers; both must pick the same (busier) one.
        var near = new List<RankedServer> { new(20, 0, 1), new(80, 0, 4) };
        var far = new List<RankedServer> { new(90, 0, 1), new(30, 0, 4) };
        Assert.Equal(1, ServerRanking.Choose(near, hostFlow: false));
        Assert.Equal(1, ServerRanking.Choose(far, hostFlow: false));
    }

    [Fact]
    public void TiesKeepTheListOrder()
    {
        var servers = new List<RankedServer> { new(50, 0, 0), new(50, 0, 0) };
        Assert.Equal(0, ServerRanking.Choose(servers, hostFlow: true));
        Assert.Equal(0, ServerRanking.Choose(servers, hostFlow: false));
    }

    [Fact]
    public void CitiesWithAServerComeFirstByScore()
    {
        var order = ServerRanking.OrderCities(new List<(CityOutcome, int)>
        {
            (CityOutcome.None, 0),
            (CityOutcome.Usable, 120),
            (CityOutcome.Updating, 0),
            (CityOutcome.Usable, 45),
            (CityOutcome.Usable, 300),
        });
        Assert.Equal(new List<int> { 3, 1, 4, 2, 0 }, order);
    }

    [Fact]
    public void ScoresInOneBucketKeepTheListOrder()
    {
        // 51 and 58 ms differ by jitter only: the list order stays. 49 is in a lower bucket.
        var order = ServerRanking.OrderCities(new List<(CityOutcome, int)>
        {
            (CityOutcome.Usable, 58),
            (CityOutcome.Usable, 51),
            (CityOutcome.Usable, 49),
        });
        Assert.Equal(new List<int> { 2, 0, 1 }, order);
    }

    [Fact]
    public void CitiesWithoutAServerKeepTheListOrder()
    {
        var order = ServerRanking.OrderCities(new List<(CityOutcome, int)>
        {
            (CityOutcome.None, 0),
            (CityOutcome.None, 0),
            (CityOutcome.Updating, 0),
            (CityOutcome.Updating, 0),
        });
        Assert.Equal(new List<int> { 2, 3, 0, 1 }, order);
    }
}
