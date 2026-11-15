using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Haulnet;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "haulnet.repo";
    public const string Name = "Haulnet";
    public const string Version = "0.4.0";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static HaulnetConfig Settings { get; private set; } = null!;
    internal static HostServer Server { get; private set; } = null!;
    internal static HostProbe Probe { get; private set; } = null!;
    internal static HaulnetRunner Runner { get; private set; } = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        Settings = new HaulnetConfig(Config);
        Server = new HostServer(
            Path.GetDirectoryName(Info.Location)!,
            Path.Combine(Paths.CachePath, "Haulnet", "server"));
        Probe = new HostProbe();

        _harmony = new Harmony(Guid);
        try
        {
            PhotonRedirect.Apply(_harmony);
        }
        catch (Exception e)
        {
            // Fail safe: without the redirect the game keeps using Photon Cloud exactly as unmodded.
            Log.LogError($"Could not install patches, Haulnet is inactive and the game uses Photon Cloud: {e}");
            _harmony.UnpatchSelf();
            return;
        }

        // Optional parts: each one logs and skips itself if its game hook is missing. The guard is for anything else
        // a game update breaks: it turns off that part only, and the rest (the INCOMPATIBLE popup too) still loads.
        Runner = HaulnetRunner.Create();
        var harmony = _harmony;
        TryApply(nameof(WherePage), () => WherePage.Apply(harmony));
        TryApply(nameof(MenuIntegration), () => MenuIntegration.Apply(harmony));
        TryApply(nameof(HostAuthority), () => HostAuthority.Apply(harmony, Runner.gameObject));
        TryApply(nameof(JoinGuard), () => JoinGuard.Apply(harmony, Runner.gameObject));
        Log.LogInfo($"{Name} {Version} loaded.");
    }

    private static void TryApply(string part, Action apply)
    {
        try
        {
            apply();
        }
        catch (Exception e)
        {
            Log.LogError($"{part} could not be installed (game update?); that part of Haulnet is off: {e}");
        }
    }
}
