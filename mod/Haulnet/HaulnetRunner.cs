using UnityEngine;

namespace Haulnet;

/// <summary>
/// Per-frame part of Haulnet: answers host reachability probes and runs the menu coroutines (server checks).
/// All player-facing UI is in the game's own menus (see WherePage and MenuIntegration).
/// </summary>
internal sealed class HaulnetRunner : MonoBehaviour
{
    public static HaulnetRunner Create()
    {
        var go = new GameObject("Haulnet") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        return go.AddComponent<HaulnetRunner>();
    }

    private void Update()
    {
        Plugin.Probe.Tick();
    }

    private void OnDestroy() => Shutdown();

    private void OnApplicationQuit() => Shutdown();

    private static void Shutdown()
    {
        Plugin.Probe.StopResponding();
        Plugin.Server.Stop();
    }
}
