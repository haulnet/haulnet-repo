using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haulnet;

/// <summary>
/// Per-frame part of Haulnet: answers host reachability probes and draws the diagnostics panel (F8, menus
/// only, hidden by default). Normal use happens through the game's own menus (see MenuIntegration).
/// </summary>
internal sealed class HaulnetRunner : MonoBehaviour
{
    private const float BaseHeight = 1080f;
    private static readonly TimeSpan AddressRefresh = TimeSpan.FromSeconds(5);

    private bool _visible;
    private string _addressDraft = "";
    private List<AddressHint> _hints = new();
    private DateTime _nextHintRefresh;
    private string _copied = "";
    private GUIStyle? _wrap;

    public static HaulnetRunner Create()
    {
        var go = new GameObject("Haulnet") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        return go.AddComponent<HaulnetRunner>();
    }

    private void Start()
    {
        _addressDraft = Plugin.Settings.LastManualAddress;
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

    private static bool InMenus()
    {
        try
        {
            return RunManager.instance != null && SemiFunc.MenuLevel();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnGUI()
    {
        if (!InMenus())
        {
            return;
        }

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F8)
        {
            _visible = !_visible;
            e.Use();
        }

        if (!_visible)
        {
            return;
        }

        var scale = Mathf.Max(0.75f, Screen.height / BaseHeight);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

        const float width = 460f;
        GUILayout.BeginArea(new Rect(Screen.width / scale - width - 16f, 16f, width, Screen.height / scale - 32f));
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"{Text.Title} {Plugin.Version}   [F8]");

        GUILayout.Label($"{Text.PanelSession} {DescribeNext()}", _wrap);
        DrawServer();
        DrawManualJoin();

        GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.matrix = Matrix4x4.identity;
    }

    private static string DescribeNext()
    {
        if (Session.HostingOnMyPc)
        {
            return Text.SessionHost;
        }

        return Session.ManualAddress.HasValue ? Text.SessionManual(Session.ManualAddress.Value.ToString()) : Text.SessionCloud;
    }

    private void DrawServer()
    {
        var server = Plugin.Server;
        if (!Session.HostingOnMyPc && server.State == ServerState.Stopped)
        {
            return;
        }

        GUILayout.Space(6f);
        switch (server.State)
        {
            case ServerState.Running:
                GUILayout.Label(Text.ServerRunning);
                break;
            case ServerState.Starting:
                GUILayout.Label(Text.ServerStarting);
                break;
            case ServerState.Failed:
                GUILayout.Label($"{Text.ServerError} {server.Error}", _wrap);
                if (GUILayout.Button(Text.Retry))
                {
                    server.Start();
                }

                break;
            default:
                GUILayout.Label(Text.ServerStopped);
                break;
        }

        if (DateTime.UtcNow >= _nextHintRefresh)
        {
            _hints = NetworkAddresses.Collect();
            _nextHintRefresh = DateTime.UtcNow + AddressRefresh;
        }

        GUILayout.Label(Text.YourAddresses, _wrap);
        if (_hints.Count == 0)
        {
            GUILayout.Label(Text.NoAddresses, _wrap);
        }

        foreach (var hint in _hints)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{hint.Text}  —  {hint.Label}", _wrap, GUILayout.Width(330f));
            if (GUILayout.Button(_copied == hint.Text ? Text.Copied : Text.Copy))
            {
                GUIUtility.systemCopyBuffer = hint.Text;
                _copied = hint.Text;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Label(Text.FirewallNote, _wrap);
    }

    private void DrawManualJoin()
    {
        GUILayout.Space(6f);
        GUILayout.Label(Text.ManualJoin, _wrap);
        _addressDraft = GUILayout.TextField(_addressDraft, 100);

        var valid = ServerAddress.TryParse(_addressDraft, out var address, out var error);
        if (_addressDraft.Trim().Length > 0 && !valid)
        {
            GUILayout.Label($"{Text.AddressInvalid} {error}", _wrap);
        }

        GUILayout.BeginHorizontal();
        GUI.enabled = valid;
        if (GUILayout.Button(Text.Apply))
        {
            Session.SetManualAddress(address);
        }

        GUI.enabled = Session.ManualAddress.HasValue;
        if (GUILayout.Button(Text.Clear))
        {
            Session.SetManualAddress(null);
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }
}
