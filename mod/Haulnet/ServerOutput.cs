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
/// Reads LuxonServer's console output (spdlog text: <c>[time] [logger] [level] message</c>). The server has no other
/// machine interface for its lifecycle, so the exact sentences matched here are part of the contract with the server:
/// the fork never changes existing wording, it only adds lines. Kept free of game types so it can be tested against
/// recorded output of every server version.
/// </summary>
internal static class ServerOutput
{
    public static ServerLineLevel Classify(string line)
    {
        if (line.Contains("[error]") || line.Contains("[critical]"))
        {
            return ServerLineLevel.Error;
        }

        if (line.Contains("[warning]"))
        {
            return line.Contains("Tick time budget") ? ServerLineLevel.Ignored : ServerLineLevel.Warning;
        }

        return ServerLineLevel.Debug;
    }

    /// <summary>
    /// LuxonServer logs "Starting NameServer on port N" once per listener, NameServer last (its config sections are
    /// set up in name order). The line comes before the bind, so it means "about to listen", not "listening".
    /// </summary>
    public static bool IsRunningLine(string line, int nameServerPort) => line.Contains("Starting NameServer on port " + nameServerPort);
}
