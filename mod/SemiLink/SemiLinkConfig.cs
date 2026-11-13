using BepInEx.Configuration;

namespace SemiLink;

public enum ConnectionMode
{
    /// <summary>Unmodded behaviour: Photon Cloud.</summary>
    Cloud,

    /// <summary>Connect to a SemiLink host or a dedicated server at <see cref="SemiLinkConfig.Address"/>.</summary>
    Join,
}

/// <summary>
/// Player settings, stored in BepInEx/config/semilink.repo.cfg.
/// Read on every connect, so changes made in the file or by a config manager apply to the next connection.
/// </summary>
internal sealed class SemiLinkConfig
{
    private readonly ConfigEntry<ConnectionMode> _mode;
    private readonly ConfigEntry<string> _address;

    public SemiLinkConfig(ConfigFile config)
    {
        _mode = config.Bind(
            "Connection",
            "Mode",
            ConnectionMode.Cloud,
            "Cloud: play through Photon Cloud, as without the mod.\n" +
            "Join: connect to the server at Address (a SemiLink host or a dedicated LuxonServer).");

        _address = config.Bind(
            "Connection",
            "Address",
            "",
            "Host address used in Join mode: IP or host name, optionally with the NameServer port.\n" +
            "Examples: 192.168.1.20, 10.8.0.2:27000, [fd00::2]:5058, myserver.example.org");
    }

    public ConnectionMode Mode => _mode.Value;

    public string Address => _address.Value?.Trim() ?? "";

    public string Describe() => Mode == ConnectionMode.Join
        ? $"Mode: Join, address: '{Address}'."
        : "Mode: Cloud (Photon Cloud, unmodded behaviour).";
}
