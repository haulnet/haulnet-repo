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
    public const string Version = "0.3.0";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static HaulnetConfig Settings { get; private set; } = null!;
    internal static HostServer Server { get; private set; } = null!;
    internal static HostProbe Probe { get; private set; } = null!;

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

        // Optional parts: each one logs and skips itself if its game hook is missing.
        RegionPing.Apply(_harmony);
        MenuIntegration.Apply(_harmony);

        HaulnetRunner.Create();
        Log.LogInfo($"{Name} {Version} loaded.");
    }
}
