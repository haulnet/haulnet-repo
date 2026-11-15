using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Haulnet;

/// <summary>
/// Looks at a server before and while it is listed: resolves the name, measures the round trip and reads the
/// optional haulnet-server status endpoint (<c>GET http://host:27080/status</c>, see the haulnet-server README).
/// Runs on a worker thread; the menu polls the task. LuxonServer itself does not answer unknown UDP packets, so
/// the game port cannot be pinged directly; only a closed one shows (<see cref="StartPortProbe"/>).
/// </summary>
internal static class ServerCheck
{
    public const int StatusPort = 27080;
    private const int TimeoutMs = 1500;
    private const int MaxStatusBytes = 64 * 1024;

    /// <summary>
    /// Each check gets its own thread (LongRunning), not a thread-pool worker: a check blocks for up to a few
    /// seconds, and the region page starts one per listed server at once. On the pool, a hundred blocking checks
    /// starve it, and with it the completions of Mono's asynchronous socket calls, which also run on the pool.
    /// </summary>
    public static Task<ServerStatus> RunAsync(ServerAddress address, int statusPort = StatusPort) =>
        Task.Factory.StartNew(() => Run(address, statusPort), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static ServerStatus Run(ServerAddress address, int statusPort)
    {
        var status = new ServerStatus();
        IPAddress? ip;
        try
        {
            ip = Resolve(address.Host);
        }
        catch (Exception e)
        {
            Plugin.Log.LogInfo($"Server {address}: name not resolved ({e.Message}).");
            return status;
        }

        if (ip == null)
        {
            return status;
        }

        status.Resolved = true;
        var probe = StartPortProbe(address, ip);
        status.IcmpMs = Ping(ip);
        ReadStatus(address, ip, statusPort, status);
        status.GamePortClosed = FinishPortProbe(address, probe, status.PingMs);
        return status;
    }

    /// <summary>
    /// Sends a one-byte datagram to the game's UDP port; LuxonServer ignores it without logging. When nothing listens
    /// there, the machine answers with ICMP "port unreachable", which the socket reports as an error on a later
    /// receive. That error proves no server runs; its absence proves nothing, as many firewalls drop such packets
    /// silently. Sent before the ping, read after the status check, so the answer usually waits already.
    /// </summary>
    private static (Socket Socket, Stopwatch Sent)? StartPortProbe(ServerAddress address, IPAddress ip)
    {
        Socket? socket = null;
        try
        {
            socket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(ip, address.EffectivePort));
            var sent = Stopwatch.StartNew();
            socket.Send(new byte[1]); // An empty datagram never left the game (Mono): the row kept its ping.
            socket.Blocking = false;
            return (socket, sent);
        }
        catch (Exception e)
        {
            socket?.Close();
            Plugin.Log.LogDebug($"Server {address}: game port probe not sent ({e.Message}).");
            return null;
        }
    }

    /// <summary>True only when the system reported the game port unreachable (see <see cref="StartPortProbe"/>).</summary>
    private static bool FinishPortProbe(ServerAddress address, (Socket Socket, Stopwatch Sent)? probe, int pingMs)
    {
        if (probe is not { } p)
        {
            return false;
        }

        using var socket = p.Socket;
        // The ICMP answer takes about one round trip; allow for jitter, never the full check timeout.
        var waitMs = pingMs >= 0 ? Math.Min(1000, 2 * pingMs + 100) : 500;
        var buffer = new byte[1];
        while (true)
        {
            try
            {
                socket.Receive(buffer);
                return false; // Something answered on the game port: not closed.
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused)
            {
                Plugin.Log.LogInfo($"Server {address}: UDP {address.EffectivePort} unreachable, no game server runs there.");
                return true;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock)
            {
                if (p.Sent.ElapsedMilliseconds >= waitMs)
                {
                    return false;
                }

                socket.Poll(20_000, SelectMode.SelectRead);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"Server {address}: game port probe failed ({e.Message}).");
                return false;
            }
        }
    }

    private static IPAddress? Resolve(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return literal;
        }

        var addresses = Dns.GetHostAddresses(host);
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
               ?? addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
    }

    /// <summary>
    /// Best of two ICMP round trips, timed here around the whole call. Mono's <c>Ping.Send</c> starts its own clock
    /// only after sending, so a pause right after the send (garbage collection, a hundred checks starting at once)
    /// made a 117 ms server read as 1 ms; timed outside, a pause can only make it look slower, and the second try
    /// absorbs a single pause. The ranking orders servers by this value.
    /// </summary>
    private static int Ping(IPAddress ip)
    {
        var best = -1;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var ping = new Ping();
                var stopwatch = Stopwatch.StartNew();
                var reply = ping.Send(ip, TimeoutMs);
                var ms = (int)Math.Max(1, stopwatch.ElapsedMilliseconds);
                if (reply == null || reply.Status != IPStatus.Success)
                {
                    break; // No answer: a second try would only cost another timeout.
                }

                best = best < 0 ? ms : Math.Min(best, ms);
            }
            catch (Exception)
            {
                // ICMP may be unavailable or blocked; the status answer time is the fallback (ServerStatus.AnswerMs).
                break;
            }
        }

        return best;
    }

    /// <summary>
    /// Connects within <see cref="TimeoutMs"/> without <c>BeginConnect</c>: in Mono its completion is a thread-pool
    /// work item (<c>IOSelectorJob</c>), so waiting on it from a busy pool can time out although the server answered.
    /// A non-blocking connect and <c>Poll</c> wait on the socket itself.
    /// </summary>
    private static bool Connect(Socket socket, IPEndPoint endPoint)
    {
        socket.Blocking = false;
        try
        {
            socket.Connect(endPoint);
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress)
        {
            // Windows reports a refused connect as an error condition, not as writable: check both, in slices.
            var waited = Stopwatch.StartNew();
            while (!socket.Poll(20_000, SelectMode.SelectWrite))
            {
                if (socket.Poll(0, SelectMode.SelectError) || waited.ElapsedMilliseconds >= TimeoutMs)
                {
                    return false;
                }
            }

            if ((int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)! != 0)
            {
                return false;
            }
        }

        socket.Blocking = true;
        return socket.Connected;
    }

    /// <summary>Plain HTTP/1.0 over a socket: no TLS, no redirects, bounded size and time.</summary>
    private static void ReadStatus(ServerAddress address, IPAddress ip, int statusPort, ServerStatus status)
    {
        try
        {
            using var client = new TcpClient(ip.AddressFamily);
            var stopwatch = Stopwatch.StartNew();
            if (!Connect(client.Client, new IPEndPoint(ip, statusPort)))
            {
                Plugin.Log.LogDebug($"Server {address}: status port {statusPort} not reached after {stopwatch.ElapsedMilliseconds} ms.");
                return;
            }

            client.SendTimeout = TimeoutMs;
            var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes($"GET /status HTTP/1.0\r\nHost: {address.HostForEndpoint}\r\nConnection: close\r\n\r\n");
            var answer = Stopwatch.StartNew();
            stream.Write(request, 0, request.Length);

            // Stop as soon as the answer is complete instead of waiting for the server to close: behind some
            // proxying VPN clients the close arrives seconds after the answer. The whole read shares one time limit.
            var buffer = new byte[MaxStatusBytes];
            var total = 0;
            var ended = false;
            var reading = Stopwatch.StartNew();
            while (total < buffer.Length && !StatusResponse.IsComplete(buffer, total))
            {
                var remainingMs = TimeoutMs - (int)reading.ElapsedMilliseconds;
                if (remainingMs <= 0)
                {
                    break;
                }

                client.ReceiveTimeout = remainingMs;
                int read;
                try
                {
                    read = stream.Read(buffer, total, buffer.Length - total);
                }
                catch (IOException)
                {
                    break;
                }

                if (read == 0)
                {
                    ended = true;
                    break;
                }

                if (total == 0)
                {
                    status.AnswerMs = (int)Math.Max(1, answer.ElapsedMilliseconds);
                }

                total += read;
            }

            if (!ended && total < buffer.Length && !StatusResponse.IsComplete(buffer, total))
            {
                Plugin.Log.LogDebug($"Server {address}: status answer incomplete after {reading.ElapsedMilliseconds} ms ({total} bytes).");
                return;
            }

            var body = StatusResponse.Body(buffer, total);
            if (body == null)
            {
                return;
            }

            var parsed = ServerStatus.Parse(body);
            status.HasStatus = true;
            status.Name = KnownServers.CleanName(parsed.Name);
            status.Luxon = parsed.Luxon;
            status.Accepting = parsed.Accepting;
            status.Draining = parsed.Draining;
            status.Players = parsed.Players;
            status.Rooms = parsed.Rooms;
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"Server {address}: no status endpoint ({e.Message}).");
        }
    }
}
