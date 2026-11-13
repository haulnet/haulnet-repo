using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace SemiLink;

/// <summary>
/// Per-frame part of SemiLink: keeps the host server and LAN discovery in line with the selected mode and
/// draws the in-game panel (IMGUI, menus only). Lives on its own hidden, persistent GameObject.
/// </summary>
internal sealed class SemiLinkRunner : MonoBehaviour
{
    private const float BaseHeight = 1080f;
    private static readonly TimeSpan AddressRefresh = TimeSpan.FromSeconds(5);

    private readonly LanDiscovery _discovery = new();

    private bool _expanded;
    private string _addressDraft = "";
    private List<AddressHint> _hints = new();
    private DateTime _nextHintRefresh;
    private string _copied = "";
    private ConnectionMode _appliedMode = (ConnectionMode)(-1);
    private GUIStyle? _wrap;

    public static SemiLinkRunner Create()
    {
        var go = new GameObject("SemiLink") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        return go.AddComponent<SemiLinkRunner>();
    }

    private void Start()
    {
        _addressDraft = Plugin.Settings.Address;
        Plugin.Settings.ModeChanged += ApplyMode;
        ApplyMode();
    }

    private void OnDestroy()
    {
        Plugin.Settings.ModeChanged -= ApplyMode;
        _discovery.Dispose();
        Plugin.Server.Stop();
    }

    private void OnApplicationQuit()
    {
        _discovery.Dispose();
        Plugin.Server.Stop();
    }

    private void Update()
    {
        _discovery.Tick();
    }

    /// <summary>Starts or stops the server and discovery for the current mode.</summary>
    private void ApplyMode()
    {
        var mode = Plugin.Settings.Mode;
        if (mode == _appliedMode)
        {
            return;
        }

        _appliedMode = mode;
        Plugin.Log.LogInfo(Plugin.Settings.Describe());

        if (mode == ConnectionMode.Host)
        {
            Plugin.Server.Start();
            _nextHintRefresh = DateTime.MinValue;
        }
        else
        {
            Plugin.Server.Stop();
        }

        if (!Plugin.Settings.LanDiscovery || mode == ConnectionMode.Cloud)
        {
            _discovery.StopAll();
        }
        else if (mode == ConnectionMode.Host)
        {
            _discovery.StartAnnouncing(PlayerName);
        }
        else
        {
            _discovery.StartListening();
        }
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
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F8 && InMenus())
        {
            _expanded = !_expanded;
            e.Use();
        }

        if (!InMenus())
        {
            return;
        }

        var scale = Mathf.Max(0.75f, Screen.height / BaseHeight);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

        const float width = 440f;
        var x = Screen.width / scale - width - 16f;
        var area = new Rect(x, 16f, width, Screen.height / scale - 32f);
        GUILayout.BeginArea(area);
        GUILayout.BeginVertical(GUI.skin.box);

        var mode = Plugin.Settings.Mode;
        if (GUILayout.Button($"{Text.Title}: {ModeName(mode)}  {StatusSuffix(mode)}   [{Text.ToggleHint}]"))
        {
            _expanded = !_expanded;
        }

        if (_expanded)
        {
            DrawModeSelector(mode);
            GUILayout.Space(6f);
            switch (mode)
            {
                case ConnectionMode.Host:
                    DrawHost();
                    break;
                case ConnectionMode.Join:
                    DrawJoin();
                    break;
                default:
                    GUILayout.Label(Text.CloudInfo, _wrap);
                    break;
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.matrix = Matrix4x4.identity;
    }

    private void DrawModeSelector(ConnectionMode mode)
    {
        var locked = PhotonNetwork.InRoom;
        GUI.enabled = !locked;
        var names = new[] { Text.ModeCloud, Text.ModeHost, Text.ModeJoin };
        var selected = GUILayout.Toolbar((int)mode, names);
        GUI.enabled = true;
        if (locked)
        {
            GUILayout.Label(Text.LockedInRoom, _wrap);
        }
        else if (selected != (int)mode)
        {
            Plugin.Settings.Mode = (ConnectionMode)selected;
        }
    }

    private void DrawHost()
    {
        GUILayout.Label(Text.HostInfo, _wrap);

        var server = Plugin.Server;
        switch (server.State)
        {
            case ServerState.Running:
                GUILayout.Label(Text.ServerRunning);
                break;
            case ServerState.Starting:
                GUILayout.Label(Text.ServerStarting);
                break;
            case ServerState.Failed:
                GUILayout.Label($"{Text.ServerFailed} {server.Error}", _wrap);
                if (GUILayout.Button(Text.Retry))
                {
                    server.Start();
                }

                break;
            default:
                GUILayout.Label(Text.ServerStopped);
                if (GUILayout.Button(Text.Retry))
                {
                    server.Start();
                }

                break;
        }

        if (DateTime.UtcNow >= _nextHintRefresh)
        {
            _hints = NetworkAddresses.Collect();
            _nextHintRefresh = DateTime.UtcNow + AddressRefresh;
        }

        GUILayout.Space(6f);
        GUILayout.Label(Text.FriendsUse, _wrap);
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

        GUILayout.Space(6f);
        GUILayout.Label(Text.FirewallNote, _wrap);
    }

    private void DrawJoin()
    {
        GUILayout.Label(Text.JoinInfo, _wrap);
        GUILayout.Label(Text.AddressLabel);
        GUI.enabled = !PhotonNetwork.InRoom;
        _addressDraft = GUILayout.TextField(_addressDraft, 100);
        GUI.enabled = true;
        if (_addressDraft.Trim() != Plugin.Settings.Address)
        {
            Plugin.Settings.Address = _addressDraft;
        }

        if (_addressDraft.Trim().Length > 0 && !ServerAddress.TryParse(_addressDraft, out _, out var error))
        {
            GUILayout.Label($"{Text.AddressInvalid} {error}", _wrap);
        }

        if (!Plugin.Settings.LanDiscovery)
        {
            return;
        }

        GUILayout.Space(6f);
        GUILayout.Label(Text.LanGames);
        var hosts = _discovery.Hosts;
        if (hosts.Count == 0)
        {
            GUILayout.Label(Text.LanNone, _wrap);
        }

        foreach (var host in hosts)
        {
            GUILayout.BeginHorizontal();
            var suffix = host.Compatible ? "" : " " + Text.Incompatible;
            GUILayout.Label($"{host.Name} ({host.Address}){suffix}", _wrap, GUILayout.Width(330f));
            GUI.enabled = host.Compatible && !PhotonNetwork.InRoom;
            if (GUILayout.Button(Text.Use))
            {
                _addressDraft = host.Address.ToString();
                Plugin.Settings.Address = _addressDraft;
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private static string ModeName(ConnectionMode mode) => mode switch
    {
        ConnectionMode.Host => Text.ModeHost,
        ConnectionMode.Join => Text.ModeJoin,
        _ => Text.ModeCloud,
    };

    private static string StatusSuffix(ConnectionMode mode)
    {
        if (mode == ConnectionMode.Host)
        {
            return Plugin.Server.State switch
            {
                ServerState.Running => "(server running)",
                ServerState.Starting => "(server starting)",
                ServerState.Failed => "(server error)",
                _ => "(server stopped)",
            };
        }

        return mode == ConnectionMode.Join && Plugin.Settings.Address.Length > 0 ? $"({Plugin.Settings.Address})" : "";
    }

    private static string PlayerName()
    {
        try
        {
            var name = Steamworks.SteamClient.Name;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch (Exception)
        {
        }

        return Environment.UserName;
    }
}
