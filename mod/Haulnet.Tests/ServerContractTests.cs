using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Haulnet.Tests;

/// <summary>
/// What the mod writes for LuxonServer and reads back from it. The fixtures are recorded output of the server
/// binaries the mod can meet: <c>v1.5-*</c> is the niansa v1.5 release the mod ships, <c>fork-a1a2-*</c> the
/// haulnet/LuxonServer fork with "fatal bind" (A1) and "exclusive bind" (A2), both started on Windows with
/// <c>host-config-28000.yml</c>, the file the mod wrote in the game for "On my PC" on UDP 28000.
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
    // src/server_manager.cpp); these are the keys it accepted in v1.5 and in the fork.
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

    [Theory]
    [InlineData("v1.5-start.log")]
    [InlineData("fork-a1a2-start.log")]
    public void EveryServerVersionReportsRunningOnceAndNoProblem(string fixture)
    {
        var lines = Lines(fixture);

        Assert.Single(lines, l => ServerOutput.IsRunningLine(l, 28000));
        Assert.DoesNotContain(lines, l => ServerOutput.Classify(l) is ServerLineLevel.Error or ServerLineLevel.Warning);
        Assert.False(lines.Any(l => ServerOutput.IsRunningLine(l, 27000)), "another port's line must not count");
    }

    // The fork adds lines and never changes existing ones (docs/luxonserver-code-style.md, section 5), so the mod's
    // "running" line still comes; "Server is ready" is new and not yet used by the mod.
    [Fact]
    public void ForkKeepsTheLineTheModWaitsForAndAddsReady()
    {
        var lines = Lines("fork-a1a2-start.log");
        var running = Array.FindIndex(lines, l => ServerOutput.IsRunningLine(l, 28000));

        Assert.True(running >= 0);
        Assert.Contains("NameServer is listening on port 28000", lines[running + 1]);
        Assert.EndsWith("Server is ready", lines[lines.Length - 1]);
        Assert.DoesNotContain(Lines("v1.5-start.log"), l => l.Contains("Server is ready"));
    }

    // With the NameServer port taken, both versions print the "running" line before the failure (it comes before the
    // bind). The mod therefore checks the ports itself first (HostServer.FindBusyPort); v1.5 then keeps running,
    // the fork exits with code 1 and the reason on stderr, which the mod reports through the exit code.
    [Theory]
    [InlineData("v1.5-nameserver-port-busy.log", false)]
    [InlineData("fork-a1a2-nameserver-port-busy.log", true)]
    public void BusyPortIsLoggedAfterTheRunningLine(string fixture, bool fatalLine)
    {
        var lines = Lines(fixture);
        var running = Array.FindIndex(lines, l => ServerOutput.IsRunningLine(l, 28000));
        var failed = Array.FindIndex(lines, l => l.Contains("Failed to bind NameServer to port 28000!"));

        Assert.True(running >= 0 && failed > running);
        Assert.Equal(ServerLineLevel.Error, ServerOutput.Classify(lines[failed]));
        Assert.Equal(fatalLine, lines.Any(l => l.StartsWith("Fatal error: Failed to bind NameServer to UDP port 28000: ", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [info] Config looks alright, setting up accordingly", "Debug")]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [warning] Tick time budget exhausted! 1 servers deferred to next tick.", "Ignored")]
    [InlineData("[2026-11-15 16:45:26.153] [ConfigParser] [warning] The 'address' key at Servers.NameServer[0] is deprecated.", "Warning")]
    [InlineData("[2026-11-15 16:45:26.153] [ServerManager] [critical] Disconnecting due to uncaught exception", "Error")]
    [InlineData("Fatal error: Failed to bind NameServer to UDP port 28000: address in use", "Debug")]
    public void LinesGoToTheLogAtTheirLevel(string line, string level)
    {
        Assert.Equal(level, ServerOutput.Classify(line).ToString());
    }
}
