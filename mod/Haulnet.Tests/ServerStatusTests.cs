using Xunit;

namespace Haulnet.Tests;

/// <summary>The body of haulnet-server's <c>GET /status</c> as the region page reads it.</summary>
public class ServerStatusTests
{
    // As sent by haulnet-server 0.1.1 (moscow-1, 2026-11-14).
    private const string Current =
        "{\"haulnet_server\":\"0.1.1\",\"luxon\":\"v1.5\",\"name\":\"moscow-1\",\"rooms\":2,\"players\":5," +
        "\"max_rooms\":20,\"max_players\":120,\"cpu_busy\":0,\"accepting\":true,\"draining\":false,\"uptime_s\":346}";

    [Fact]
    public void ReadsTheCurrentFormat()
    {
        var status = ServerStatus.Parse(Current);

        Assert.True(status.HasStatus);
        Assert.Equal("moscow-1", status.Name);
        Assert.Equal("v1.5", status.Luxon);
        Assert.True(status.Accepting);
        Assert.False(status.Draining);
        Assert.False(status.Updating);
        Assert.Equal(5, status.Players);
        Assert.Equal(2, status.Rooms);
    }

    [Fact]
    public void DrainingServerIsUpdating()
    {
        var status = ServerStatus.Parse(Current.Replace("\"accepting\":true,\"draining\":false", "\"accepting\":false,\"draining\":true"));

        Assert.False(status.Accepting);
        Assert.True(status.Draining);
        Assert.True(status.Updating);
    }

    [Fact]
    public void AllowsWhitespaceAroundTheColon()
    {
        var status = ServerStatus.Parse("{ \"accepting\" : false,\n  \"draining\" :\ttrue }");

        Assert.False(status.Accepting);
        Assert.True(status.Draining);
        Assert.True(status.Updating);
    }

    [Fact]
    public void OlderServerWithoutTheFieldIsNotDraining()
    {
        var status = ServerStatus.Parse("{\"luxon\":\"v1.5\",\"name\":\"old\",\"rooms\":0,\"players\":0,\"accepting\":false}");

        Assert.False(status.Accepting);
        Assert.False(status.Draining);
        Assert.False(status.Updating);
    }

    [Fact]
    public void DrainingWhileStillAcceptingIsNotUpdating()
    {
        // Not sent by haulnet-server (draining implies accepting: false); the server stays usable then.
        var status = ServerStatus.Parse("{\"accepting\":true,\"draining\":true}");

        Assert.True(status.Accepting);
        Assert.True(status.Draining);
        Assert.False(status.Updating);
    }

    [Fact]
    public void FullServerIsNotUpdating()
    {
        var status = ServerStatus.Parse(Current.Replace("\"accepting\":true", "\"accepting\":false"));

        Assert.False(status.Accepting);
        Assert.False(status.Draining);
        Assert.False(status.Updating);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"draining\":\"true\",\"accepting\":\"false\"}")]
    public void MissingOrOddValuesKeepTheDefaults(string body)
    {
        var status = ServerStatus.Parse(body);

        Assert.True(status.Accepting);
        Assert.False(status.Draining);
        Assert.False(status.Updating);
        Assert.Equal("", status.Name);
        Assert.Equal(0, status.Players);
    }

    [Fact]
    public void ResultOfAFailedLookIsNotUpdating()
    {
        Assert.False(new ServerStatus { Accepting = false, Draining = true }.Updating);
    }
}
