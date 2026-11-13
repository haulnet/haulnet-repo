using BepInEx.Configuration;

namespace Haulnet;

/// <summary>
/// Player settings, stored in BepInEx/config/haulnet.repo.cfg. There is no global mode: where a game is
/// played is chosen per game on the region page, and joining follows the Steam invite (see <see cref="Session"/>).
/// </summary>
internal sealed class HaulnetConfig
{
    private readonly ConfigEntry<int> _hostPort;
    private readonly ConfigEntry<string> _savedServers;
    private readonly ConfigEntry<string> _recentServers;
    private readonly ConfigEntry<string> _lastAddress;

    public HaulnetConfig(ConfigFile config)
    {
        _hostPort = config.Bind(
            "Hosting",
            "Port",
            ServerAddress.DefaultPort,
            new ConfigDescription(
                "UDP port of the server when you host \"On my PC\". It uses this port and the next two " +
                "(default 27000-27002). Friends get it with the Steam invite.",
                new AcceptableValueRange<int>(1024, 65533)));

        _savedServers = config.Bind(
            "Servers",
            "Saved",
            "",
            "Servers always listed on the region page, as Name=host[:port] separated by ';'. " +
            "Example: Dacha=play.example.org;Office=203.0.113.5:27100");

        _recentServers = config.Bind(
            "Servers",
            "Recent",
            "",
            "Servers you played on recently (filled automatically, newest first, at most 5).");

        _lastAddress = config.Bind(
            "Servers",
            "LastAddress",
            "",
            "Last address typed in \"Server by address\".");
    }

    public int HostPort
    {
        get => _hostPort.Value;
        set => _hostPort.Value = value;
    }

    public string SavedServers => _savedServers.Value ?? "";

    public string RecentServers
    {
        get => _recentServers.Value ?? "";
        set => _recentServers.Value = value ?? "";
    }

    public string LastAddress
    {
        get => _lastAddress.Value?.Trim() ?? "";
        set => _lastAddress.Value = value?.Trim() ?? "";
    }
}
