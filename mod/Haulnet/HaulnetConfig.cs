using BepInEx.Configuration;

namespace Haulnet;

/// <summary>
/// Player settings, stored in BepInEx/config/haulnet.repo.cfg. There is no global mode: where a game is
/// hosted is chosen per game, and joining follows the Steam invite (see <see cref="Session"/>).
/// </summary>
internal sealed class HaulnetConfig
{
    private readonly ConfigEntry<bool> _lastChoiceMyPc;
    private readonly ConfigEntry<string> _lastManualAddress;

    public HaulnetConfig(ConfigFile config)
    {
        _lastChoiceMyPc = config.Bind(
            "Hosting",
            "LastChoiceOnMyPc",
            false,
            "Remembers the last answer to \"Where to host?\" so it is offered first next time.");

        _lastManualAddress = config.Bind(
            "Connection",
            "LastManualAddress",
            "",
            "Last server address entered in the Haulnet panel (F8).");
    }

    public bool LastChoiceOnMyPc
    {
        get => _lastChoiceMyPc.Value;
        set => _lastChoiceMyPc.Value = value;
    }

    public string LastManualAddress
    {
        get => _lastManualAddress.Value?.Trim() ?? "";
        set => _lastManualAddress.Value = value?.Trim() ?? "";
    }
}
