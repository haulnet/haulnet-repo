using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Haulnet;

/// <summary>
/// Looks at a server before and while it is listed: resolves the name, measures the round trip and reads the
/// optional haulnet-server status endpoint (<c>GET http://host:27080/status</c>, see the haulnet-server README).
/// Runs on a worker thread; the menu polls the task. LuxonServer itself does not answer unknown UDP packets, so
/// the game port cannot be pinged directly.
/// </summary>
internal static class ServerCheck
{
    public const int StatusPort = 27080;
    private const int TimeoutMs = 1500;
    private const int MaxStatusBytes = 64 * 1024;

    public static Task<ServerStatus> RunAsync(ServerAddress address, int statusPort = StatusPort) =>
        Task.Run(() => Run(address, statusPort));

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
        status.PingMs = Ping(ip);
        ReadStatus(address, ip, statusPort, status);
        return status;
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

    private static int Ping(IPAddress ip)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(ip, TimeoutMs);
            if (reply != null && reply.Status == IPStatus.Success)
            {
                return (int)Math.Max(1, reply.RoundtripTime);
            }
        }
        catch (Exception)
        {
            // ICMP may be unavailable or blocked; the status endpoint's connect time is the fallback.
        }

        return -1;
    }

    /// <summary>Plain HTTP/1.0 over a socket: no TLS, no redirects, bounded size and time.</summary>
    private static void ReadStatus(ServerAddress address, IPAddress ip, int statusPort, ServerStatus status)
    {
        try
        {
            using var client = new TcpClient(ip.AddressFamily);
            var stopwatch = Stopwatch.StartNew();
            var connect = client.BeginConnect(ip, statusPort, null, null);
            if (!connect.AsyncWaitHandle.WaitOne(TimeoutMs) || !client.Connected)
            {
                return;
            }

            client.EndConnect(connect);
            var connectMs = (int)Math.Max(1, stopwatch.ElapsedMilliseconds);
            if (status.PingMs < 0)
            {
                status.PingMs = connectMs;
            }

            client.ReceiveTimeout = TimeoutMs;
            client.SendTimeout = TimeoutMs;
            var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes($"GET /status HTTP/1.0\r\nHost: {address.HostForEndpoint}\r\nConnection: close\r\n\r\n");
            stream.Write(request, 0, request.Length);

            var buffer = new byte[MaxStatusBytes];
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
            }

            var response = Encoding.UTF8.GetString(buffer, 0, total);
            var bodyStart = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (!response.StartsWith("HTTP/1.", StringComparison.Ordinal) || response.Length < 12 || response.Substring(9, 3) != "200" || bodyStart < 0)
            {
                return;
            }

            var parsed = ServerStatus.Parse(response.Substring(bodyStart + 4));
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
