using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SemiLink;

internal enum ServerState
{
    Stopped,
    Starting,
    Running,
    Failed,
}

/// <summary>
/// Runs the bundled LuxonServer for Host mode: writes its config, starts it as a child process tied to
/// the game, reports readiness and errors, and stops it when hosting ends.
/// </summary>
internal sealed class HostServer
{
    /// <summary>The ports R.E.P.O. uses (Photon "alternative UDP ports"): NameServer, Master, Game.</summary>
    public static readonly int[] Ports = { 27000, 27001, 27002 };

    private const string ExecutableName = "luxon_server.exe";
    private const int MaxRememberedLines = 20;

    private readonly string _executable;
    private readonly string _workDir;
    private readonly object _lock = new();
    private readonly Queue<string> _lastLines = new();

    private Process? _process;
    private volatile ServerState _state = ServerState.Stopped;
    private volatile string _error = "";

    public HostServer(string pluginDir, string workDir)
    {
        _executable = Path.Combine(pluginDir, "server", ExecutableName);
        _workDir = workDir;
    }

    public ServerState State => _state;

    /// <summary>Human-readable reason when <see cref="State"/> is <see cref="ServerState.Failed"/>.</summary>
    public string Error => _error;

    /// <summary>Starts the server unless it is already starting or running.</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_state is ServerState.Starting or ServerState.Running && _process is { HasExited: false })
            {
                return;
            }

            StopLocked();
            _lastLines.Clear();

            if (!File.Exists(_executable))
            {
                Fail($"Server file is missing: {_executable}. Reinstall SemiLink.");
                return;
            }

            var busy = FindBusyPort();
            if (busy != 0)
            {
                Fail($"UDP port {busy} is already in use. Close other R.E.P.O. instances or servers and try again.");
                return;
            }

            try
            {
                Directory.CreateDirectory(_workDir);
                File.WriteAllText(Path.Combine(_workDir, "config.yml"), BuildConfig(), new UTF8Encoding(false));

                var startInfo = new ProcessStartInfo(_executable)
                {
                    WorkingDirectory = _workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.OutputDataReceived += (_, e) => OnOutput(e.Data);
                process.ErrorDataReceived += (_, e) => OnOutput(e.Data);
                process.Exited += (_, _) => OnExited(process);

                _state = ServerState.Starting;
                _error = "";
                process.Start();
                JobObject.TryAssign(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;
                Plugin.Log.LogInfo($"Host server starting (pid {process.Id}).");
            }
            catch (Exception e)
            {
                Fail($"Could not start the server: {e.Message}");
            }
        }
    }

    /// <summary>Blocks up to <paramref name="timeout"/> until the server is running or has failed.</summary>
    public bool WaitUntilReady(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (_state == ServerState.Starting && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        return _state == ServerState.Running;
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopLocked();
            _state = ServerState.Stopped;
            _error = "";
        }
    }

    private void StopLocked()
    {
        var process = _process;
        _process = null;
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(2000);
                Plugin.Log.LogInfo("Host server stopped.");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not stop the server cleanly: {e.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void OnOutput(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (_lastLines)
        {
            _lastLines.Enqueue(line!);
            while (_lastLines.Count > MaxRememberedLines)
            {
                _lastLines.Dequeue();
            }
        }

        // Keep the server's own messages in the BepInEx log so bug reports contain them.
        if (line!.Contains("[error]") || line.Contains("[critical]"))
        {
            Plugin.Log.LogError("[server] " + line);
        }
        else if (line.Contains("[warning]"))
        {
            // The server warns about its tick budget on every busy tick; not useful for players.
            if (!line.Contains("Tick time budget"))
            {
                Plugin.Log.LogWarning("[server] " + line);
            }
        }
        else
        {
            Plugin.Log.LogDebug("[server] " + line);
        }

        // LuxonServer logs "Starting NameServer on port N" once per listener, NameServer last.
        if (_state == ServerState.Starting && line.Contains("Starting NameServer on port " + Ports[0]))
        {
            _state = ServerState.Running;
            Plugin.Log.LogInfo("Host server is running.");
        }
    }

    private void OnExited(Process process)
    {
        if (!ReferenceEquals(process, _process))
        {
            return; // Stopped on purpose.
        }

        string tail;
        lock (_lastLines)
        {
            tail = string.Join(" | ", _lastLines.ToArray());
        }

        var code = 0;
        try
        {
            code = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
        }

        Fail($"The server stopped unexpectedly (exit code {code}). Last output: {tail}");
    }

    private void Fail(string message)
    {
        _state = ServerState.Failed;
        _error = message;
        Plugin.Log.LogError(message);
    }

    private static int FindBusyPort()
    {
        foreach (var port in Ports)
        {
            try
            {
                using var probe = new UdpClient(AddressFamily.InterNetwork);
                probe.Client.ExclusiveAddressUse = true;
                probe.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (SocketException)
            {
                return port;
            }
        }

        return 0;
    }

    /// <summary>
    /// LuxonServer configuration for Host mode. Only the ports the game uses are opened. The advertised
    /// address is loopback: the host's own game uses it as is, and joining players rewrite it to the
    /// address they used (see PhotonRedirect). A single region keeps the game's region page short.
    /// </summary>
    private static string BuildConfig()
    {
        var names = new[] { "NameServer", "MasterServer", "GameServer" };
        var sb = new StringBuilder();
        sb.AppendLine("# Generated by SemiLink for Host mode. Changes are overwritten on every start.");
        sb.AppendLine("Servers:");
        for (var i = 0; i < names.Length; i++)
        {
            sb.AppendLine($"  {names[i]}:");
            sb.AppendLine($"    - port: {Ports[i]}");
            sb.AppendLine($"      external_address: 127.0.0.1:{Ports[i]}");
        }

        sb.AppendLine("EnableIPv6: true");
        sb.AppendLine("MaxConnections: 20");
        sb.AppendLine("MaxGamePeers: 0");
        sb.AppendLine("TickTimeBudget: 2000");
        sb.AppendLine("NoBanner: true");
        sb.AppendLine("Regions:");
        sb.AppendLine("  - europe");
        sb.AppendLine("HTTP:");
        sb.AppendLine("  enabled: false");
        return sb.ToString();
    }
}
