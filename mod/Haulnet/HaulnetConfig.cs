using System;
using BepInEx.Configuration;

namespace Haulnet;

public enum ConnectionMode
{
    /// <summary>Unmodded behaviour: Photon Cloud.</summary>
    Cloud,

    /// <summary>Run the server inside this game; friends join this PC.</summary>
    Host,

    /// <summary>Connect to a Haulnet host or a dedicated server at <see cref="HaulnetConfig.Address"/>.</summary>
    Join,
}

/// <summary>
/// Player settings, stored in BepInEx/config/haulnet.repo.cfg.
/// Read on every connect, so changes made in game, in the file or by a config manager apply to the next connection.
/// </summary>
internal sealed class HaulnetConfig
{
    private readonly ConfigEntry<ConnectionMode> _mode;
    private readonly ConfigEntry<string> _address;
    private readonly ConfigEntry<bool> _lanDiscovery;

    public HaulnetConfig(ConfigFile config)
    {
        _mode = config.Bind(
            "Connection",
            "Mode",
            ConnectionMode.Cloud,
            "Cloud: play through Photon Cloud, as without the mod.\n" +
            "Host: run the server inside your game; friends join your PC.\n" +
            "Join: connect to the server at Address (a Haulnet host or a dedicated LuxonServer).");

        _address = config.Bind(
            "Connection",
            "Address",
            "",
            "Host address used in Join mode: IP or host name, optionally with the NameServer port.\n" +
            "Examples: 192.168.1.20, 10.8.0.2:27000, [fd00::2]:27000, myserver.example.org");

        _lanDiscovery = config.Bind(
            "Connection",
            "LanDiscovery",
            true,
            "Host: announce the game on the local network. Join: list games announced on the local network.");

        _mode.SettingChanged += (_, _) => ModeChanged?.Invoke();
    }

    /// <summary>Raised when the mode changes, from the in-game menu or a config manager.</summary>
    public event Action? ModeChanged;

    public ConnectionMode Mode
    {
        get => _mode.Value;
        set => _mode.Value = value;
    }

    public string Address
    {
        get => _address.Value?.Trim() ?? "";
        set => _address.Value = value?.Trim() ?? "";
    }

    public bool LanDiscovery => _lanDiscovery.Value;

    public string Describe() => Mode switch
    {
        ConnectionMode.Join => $"Mode: Join, address: '{Address}'.",
        ConnectionMode.Host => "Mode: Host (server runs inside this game).",
        _ => "Mode: Cloud (Photon Cloud, unmodded behaviour).",
    };
}
