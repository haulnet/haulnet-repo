using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Haulnet.Tests;

/// <summary>
/// What the mod writes for the bundled LuxonServer and reads back from it. The fixtures are recorded output of the
/// release the mod bundles (haulnet/LuxonServer v1.5-haulnet.1, MSVC), started on Windows with
/// <c>host-config-28000.yml</c>, the file the mod wrote in the game for "On my PC" on UDP 28000: a normal start and
/// a start with the NameServer (28000) or GameServer (28002) port held by another program.
/// </summary>
public class ServerContractTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LuxonServer");

    private static string[] Lines(string name) =>
        File.ReadAllText(Path.Combine(Folder, name)).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    [Fact]
    public void ConfigIsTheFileTheGameWrote()
    {
        Assert.Equal(Normalize(File.ReadAllText(Path.Combine(Folder, "host-config-28000.yml"))), Normalize(HostConfig.Build(28000)));
    }

    // LuxonServer refuses to start on a key it does not know (ValidateAllowedKeys and the top-level switch in
    // src/server_manager.cpp); these are the keys the bundled release accepts.
    [Fact]
    public void ConfigUsesOnlyKeysTheServerAccepts()
    {
        var topLevel = new[] { "Servers", "EnableIPv6", "MaxConnections", "MaxGamePeers", "TickTimeBudget", "NoBanner", "Regions", "HTTP" };
        var perServer = new[] { "port", "external_address" };
        var lines = Normalize(HostConfig.Build(27000)).Split('\n').Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToArray();

        foreach (var line in lines.Where(l => !l.StartsWith(" ", StringComparison.Ordinal)))
        {
            Assert.Contains(line.Split(':')[0], topLevel);
        }

        foreach (var line in lines.Where(l => l.StartsWith("    ", StringComparison.Ordinal)))
        {
            Assert.Contains(line.Trim().TrimStart('-', ' ').Split(':')[0], perServer);
        }
    }

    [Theory]
    [InlineData(27000)]
    [InlineData(28000)]
    public void ConfigOpensBasePortAndTheNextTwo(int basePort)
    {
        var config = HostConfig.Build(basePort);

        for (var i = 0; i < 3; i++)
        {
            Assert.Contains($"    - port: {basePort + i}\n      external_address: 127.0.0.1:{basePort + i}", Normalize(config));
        }

        Assert.Contains("  - europe", Normalize(config)); // LuxonServer maps "europe" to Photon region "eu", HostServer.RegionCode.
    }

    [Fact]
    public void ReadyComesLastAndOnlyOnce()
    {
        var lines = Lines("release-start.log");

        Assert.Single(lines, ServerOutput.IsReadyLine);
        Assert.True(ServerOutput.IsReadyLine(lines[lines.Length - 1]));
        Assert.Contains(lines, l => l.Contains("NameServer is listening on port 28000"));
        Assert.DoesNotContain(lines, l => ServerOutput.Classify(l) is ServerLineLevel.Error or ServerLineLevel.Warning);
        Assert.DoesNotContain(lines, l => ServerOutput.BindFailurePort(l) != 0);
    }

    // A taken port: the server logs the bind error, never "ready", and its last word is the fatal line naming the
    // port (exit code 1). The NameServer port is bound last, the GameServer port first.
    [Theory]
    [InlineData("release-nameserver-port-busy.log", 28000)]
    [InlineData("release-gameserver-port-busy.log", 28002)]
    public void BusyPortIsNamedByTheFatalLine(string fixture, int port)
    {
        var lines = Lines(fixture);

        Assert.DoesNotContain(lines, ServerOutput.IsReadyLine);
        Assert.Equal(new[] { port }, lines.Select(ServerOutput.BindFailurePort).Where(p => p != 0).ToArray());
        Assert.Equal(ServerLineLevel.Error, ServerOutput.Classify(lines.Single(l => l.StartsWith("Fatal error: ", StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("Fatal error: Failed to bind NameServer to UDP port 28000: Address in use", 28000)]
    [InlineData("Fatal error: Config error at Foo: unknown top-level key: Foo", 0)]
    [InlineData("[2026-11-15 17:05:54.350] [ServerManager] [error] Failed to bind GameServer to port 28002!", 0)]
    public void OnlyTheFatalBindLineNamesAPort(string line, int port)
    {
        Assert.Equal(port, ServerOutput.BindFailurePort(line));
    }

    [Theory]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [info] Config looks alright, setting up accordingly", "Debug")]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [warning] Tick time budget exhausted! 1 servers deferred to next tick.", "Ignored")]
    [InlineData("[2026-11-15 16:45:26.153] [ConfigParser] [warning] The 'address' key at Servers.NameServer[0] is deprecated.", "Warning")]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [critical] Disconnecting due to uncaught exception", "Error")]
    [InlineData("Fatal error: Failed to bind NameServer to UDP port 28000: address in use", "Error")]
    public void LinesGoToTheLogAtTheirLevel(string line, string level)
    {
        Assert.Equal(level, ServerOutput.Classify(line).ToString());
    }
}
