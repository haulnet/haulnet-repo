using Xunit;

namespace Haulnet.Tests;

public class StabilityDataTests
{
    // Shape of https://monitor.haulnet.cc/v1/status.json (haulnet-servers monitor, schema 1), shortened.
    private const string Live = """
        {
         "schema_version": 1,
         "generated_at": "2026-11-14T14:27:23Z",
         "monitor": {"location": "Moscow, RU", "interval_s": 60, "offline_after": 3, "address_family": "IPv4"},
         "servers": [
          {"id": "europe-moscow-1", "state": "online", "connect_ms": 0.6, "compatible": true,
           "status": {"haulnet_server": "0.1.1", "luxon": "v1.5", "players": 0, "accepting": true},
           "uptime": {"24h": 1.0, "7d": 1.0, "30d": 1.0}},
          {"id": "asia-vladivostok-1", "state": "online",
           "uptime": {"24h": 0.95, "7d": 0.99, "30d": 0.995}},
          {"id": "new-server", "state": "pending", "uptime": {"24h": null, "7d": null, "30d": null}},
          {"id": "odd", "uptime": {"24h": "high", "7d": 7}},
          {"state": "online", "uptime": {"24h": 0.5}}
         ]
        }
        """;

    [Fact]
    public void ReadsPenaltiesPerServer()
    {
        var penalties = StabilityData.ParsePenalties(Live, out var error);
        Assert.NotNull(penalties);
        Assert.Equal("", error);
        Assert.Equal(0, penalties!["europe-moscow-1"]);
        Assert.Equal(100, penalties["asia-vladivostok-1"]); // worse of 95 % and 99 %
        Assert.Equal(0, penalties["new-server"]);           // no checks yet
        Assert.Equal(0, penalties["odd"]);                  // invalid values ignored
        Assert.Equal(4, penalties.Count);                   // entry without id skipped
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>")]
    [InlineData("{\"schema_version\": 2, \"servers\": []}")]
    [InlineData("{\"servers\": []}")]
    public void RejectsWhatIsNotTheMonitorDocument(string text)
    {
        Assert.Null(StabilityData.ParsePenalties(text, out var error));
        Assert.NotEqual("", error);
    }

    [Fact]
    public void EmptyServerListIsValid() =>
        Assert.Empty(StabilityData.ParsePenalties("{\"schema_version\": 1, \"servers\": []}", out _)!);
}
