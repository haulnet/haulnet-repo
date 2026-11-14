using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Haulnet;

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
    /// <summary>NameServer, Master and Game ports: the configured base port and the next two (default 27000-27002).</summary>
    private int[] _ports = { ServerAddress.DefaultPort, ServerAddress.DefaultPort + 1, ServerAddress.DefaultPort + 2 };

    /// <summary>
    /// The only region the host server offers (LuxonServer name "europe", Photon code "eu"). The name has no
    /// meaning for a server on the host's PC; it only has to match on both sides.
    /// </summary>
    public const string RegionCode = "eu";
    private const string RegionName = "europe";

    private const string ExecutableName = "luxon_server.exe";
    private const int MaxRememberedLines = 20;

    private readonly string _executable;
    private readonly string _workDir;
    private readonly object _lock = new();
    private readonly Queue<string> _lastLines = new();

    private Process? _process;
    private volatile ServerState _state = ServerState.Stopped;
    private volatile ServerError _error = ServerError.None;

    public HostServer(string pluginDir, string workDir)
    {
        _executable = Path.Combine(pluginDir, "server", ExecutableName);
        _workDir = workDir;
    }

    public ServerState State => _state;

    /// <summary>Why <see cref="State"/> is <see cref="ServerState.Failed"/>; shown through Text, logged in English.</summary>
    public ServerError Error => _error;

    /// <summary>NameServer port of the running (or last started) server.</summary>
    public int BasePort => _ports[0];

    /// <summary>Starts the server on <paramref name="basePort"/> and the next two ports, unless it already runs there.</summary>
    public void Start(int basePort)
    {
        lock (_lock)
        {
            if (_state is ServerState.Starting or ServerState.Running && _process is { HasExited: false } && basePort == _ports[0])
            {
                return;
            }

            _ports = new[] { basePort, basePort + 1, basePort + 2 };

            StopLocked();
            _lastLines.Clear();

            if (!File.Exists(_executable))
            {
                Fail(new ServerError(ServerProblem.FileMissing, 0, _executable));
                return;
            }

            var busy = FindBusyPort();
            if (busy != 0)
            {
                Fail(new ServerError(ServerProblem.PortInUse, busy, ""));
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
                _error = ServerError.None;
                process.Start();
                JobObject.TryAssign(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;
                Plugin.Log.LogInfo($"Host server starting (pid {process.Id}).");
            }
            catch (Exception e)
            {
                Fail(new ServerError(ServerProblem.StartFailed, 0, e.Message));
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
            _error = ServerError.None;
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
        if (_state == ServerState.Starting && line.Contains("Starting NameServer on port " + _ports[0]))
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

        Fail(new ServerError(ServerProblem.Exited, code, tail));
    }

    private void Fail(ServerError error)
    {
        _error = error;
        _state = ServerState.Failed;
        Plugin.Log.LogError(error.ToString());
    }

    private int FindBusyPort()
    {
        // LuxonServer binds with address reuse, so a test bind can succeed although another server already
        // listens there (both then receive half of the traffic). Ask Windows for the active listeners first.
        try
        {
            var listening = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            foreach (var port in _ports)
            {
                foreach (var endpoint in listening)
                {
                    if (endpoint.Port == port)
                    {
                        return port;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"Could not list UDP listeners: {e.Message}");
        }

        foreach (var port in _ports)
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
    private string BuildConfig()
    {
        var names = new[] { "NameServer", "MasterServer", "GameServer" };
        var sb = new StringBuilder();
        sb.AppendLine("# Generated by Haulnet for Host mode. Changes are overwritten on every start.");
        sb.AppendLine("Servers:");
        for (var i = 0; i < names.Length; i++)
        {
            sb.AppendLine($"  {names[i]}:");
            sb.AppendLine($"    - port: {_ports[i]}");
            sb.AppendLine($"      external_address: 127.0.0.1:{_ports[i]}");
        }

        sb.AppendLine("EnableIPv6: true");
        sb.AppendLine("MaxConnections: 20");
        sb.AppendLine("MaxGamePeers: 0");
        sb.AppendLine("TickTimeBudget: 2000");
        sb.AppendLine("NoBanner: true");
        sb.AppendLine("Regions:");
        sb.AppendLine("  - " + RegionName);
        sb.AppendLine("HTTP:");
        sb.AppendLine("  enabled: false");
        return sb.ToString();
    }
}

internal enum ServerProblem
{
    None,
    FileMissing,
    PortInUse,
    StartFailed,
    Exited,
}

/// <summary>
/// Why the bundled server failed. <see cref="Number"/> is the busy port or the exit code; <see cref="Detail"/> the
/// missing path, the system's message or the server's last output (not translated).
/// </summary>
internal sealed class ServerError
{
    public static readonly ServerError None = new(ServerProblem.None, 0, "");

    public ServerError(ServerProblem problem, int number, string detail)
    {
        Problem = problem;
        Number = number;
        Detail = detail;
    }

    public ServerProblem Problem { get; }
    public int Number { get; }
    public string Detail { get; }

    /// <summary>English, for the log.</summary>
    public override string ToString() => Problem switch
    {
        ServerProblem.FileMissing => $"Server file is missing: {Detail}. Reinstall Haulnet.",
        ServerProblem.PortInUse => $"UDP port {Number} is already in use by another program.",
        ServerProblem.StartFailed => $"Could not start the server: {Detail}",
        ServerProblem.Exited => $"The server stopped unexpectedly (exit code {Number}). Last output: {Detail}",
        _ => "",
    };
}
