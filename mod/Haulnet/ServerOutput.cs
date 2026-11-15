using System.Globalization;
using System.Text.RegularExpressions;

namespace Haulnet;

/// <summary>How a line of LuxonServer's console output goes into the BepInEx log.</summary>
internal enum ServerLineLevel
{
    /// <summary>Not logged: the tick budget warning, printed on every busy tick.</summary>
    Ignored,
    Debug,
    Warning,
    Error,
}

/// <summary>
/// Reads the console output of the bundled LuxonServer (Haulnet's fork, spdlog text: <c>[time] [logger] [level]
/// message</c>, plus one <c>Fatal error:</c> line on stderr before it exits with code 1). The exact sentences matched
/// here are part of the contract with the server; the fork never changes existing wording. Kept free of game types so
/// it can be tested against recorded output of the release the mod bundles.
/// </summary>
internal static class ServerOutput
{
    private const string FatalPrefix = "Fatal error: ";

    // "Fatal error: Failed to bind NameServer to UDP port 28000: address in use"; the reason comes from the C library.
    private static readonly Regex BindFailure = new(@"^Fatal error: Failed to bind \w+ to UDP port (\d{1,5}):", RegexOptions.CultureInvariant);

    public static ServerLineLevel Classify(string line)
    {
        if (line.Contains("[error]") || line.Contains("[critical]") || line.StartsWith(FatalPrefix, System.StringComparison.Ordinal))
        {
            return ServerLineLevel.Error;
        }

        if (line.Contains("[warning]"))
        {
            return line.Contains("Tick time budget") ? ServerLineLevel.Ignored : ServerLineLevel.Warning;
        }

        return ServerLineLevel.Debug;
    }

    /// <summary>Logged once every configured port is bound and watched: the server accepts connections.</summary>
    public static bool IsReadyLine(string line) => line.EndsWith("] Server is ready", System.StringComparison.Ordinal);

    /// <summary>The UDP port in the fatal "Failed to bind" line, or 0 for any other line.</summary>
    public static int BindFailurePort(string line)
    {
        var match = BindFailure.Match(line);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
