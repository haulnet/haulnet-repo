using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SemiLink;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "semilink.repo";
    public const string Name = "SemiLink";
    public const string Version = "0.1.0";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static SemiLinkConfig Settings { get; private set; } = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        Settings = new SemiLinkConfig(Config);

        _harmony = new Harmony(Guid);
        try
        {
            PhotonRedirect.Apply(_harmony);
        }
        catch (Exception e)
        {
            // Fail safe: without our patches the game keeps using Photon Cloud exactly as unmodded.
            Log.LogError($"Could not install patches, SemiLink is inactive and the game uses Photon Cloud: {e}");
            _harmony.UnpatchSelf();
            return;
        }

        Log.LogInfo($"{Name} {Version} loaded. {Settings.Describe()}");
    }
}
