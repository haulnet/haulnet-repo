using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Haulnet;

/// <summary>
/// Reachability check between a joining player and the host, before the game connects.
/// </summary>
/// <remarks>
/// The host answers small UDP probes on <see cref="Port"/> while it hosts. A joining player sends a probe to
/// every address the host published in the Steam lobby and uses the first one that answers. The game's own
/// server (LuxonServer) cannot be used for this: it does not answer unknown packets.
/// Wire format (UTF-8): request <c>HAULNET?|nonce</c>, reply <c>HAULNET!|nonce|protocol</c>.
/// </remarks>
internal sealed class HostProbe : IDisposable
{
    /// <summary>Host/joiner compatibility version. Bump when hosts and joiners stop being compatible.</summary>
    public const int ProtocolVersion = 1;

    public const int Port = 47777;

    private const string Request = "HAULNET?";
    private const string Reply = "HAULNET!";

    private UdpClient? _responder;

    public bool IsResponding => _responder != null;

    /// <summary>Host side: start answering probes. Call <see cref="Tick"/> every frame.</summary>
    public void StartResponding()
    {
        if (_responder != null)
        {
            return;
        }

        try
        {
            var socket = new UdpClient(AddressFamily.InterNetworkV6);
            socket.Client.DualMode = true;
            socket.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));
            _responder = socket;
            Plugin.Log.LogInfo($"Answering reachability probes on UDP {Port}.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Cannot answer reachability probes on UDP {Port}: {e.Message}. Friends will connect without the check.");
        }
    }

    public void StopResponding()
    {
        _responder?.Close();
        _responder = null;
    }

    public void Tick()
    {
        var socket = _responder;
        if (socket == null)
        {
            return;
        }

        try
        {
            while (socket.Available > 0)
            {
                var from = new IPEndPoint(IPAddress.IPv6Any, 0);
                var data = socket.Receive(ref from);
                if (data.Length > 128)
                {
                    continue;
                }

                var parts = Encoding.UTF8.GetString(data).Split('|');
                if (parts.Length == 2 && parts[0] == Request)
                {
                    var reply = Encoding.UTF8.GetBytes($"{Reply}|{parts[1]}|{ProtocolVersion}");
                    socket.Send(reply, reply.Length, from);
                }
            }
        }
        catch (SocketException)
        {
            // A probe from an unreachable sender can surface as an ICMP error; ignore and keep listening.
        }
    }

    public void Dispose() => StopResponding();

    /// <summary>
    /// Joiner side: probes all <paramref name="addresses"/> in parallel and returns the first that answers
    /// within <paramref name="timeout"/>, or null. Blocks the caller for at most the timeout.
    /// </summary>
    public static ProbeResult? FindReachable(IReadOnlyList<IPAddress> addresses, TimeSpan timeout)
    {
        if (addresses.Count == 0)
        {
            return null;
        }

        var nonce = Guid.NewGuid().ToString("N").Substring(0, 12);
        var request = Encoding.UTF8.GetBytes($"{Request}|{nonce}");
        try
        {
            using var socket = new UdpClient(AddressFamily.InterNetworkV6);
            socket.Client.DualMode = true;
            socket.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

            var stopwatch = Stopwatch.StartNew();
            var nextSend = TimeSpan.Zero;
            while (stopwatch.Elapsed < timeout)
            {
                if (stopwatch.Elapsed >= nextSend)
                {
                    // Re-send a few times in case a packet is lost.
                    nextSend = stopwatch.Elapsed + TimeSpan.FromMilliseconds(300);
                    foreach (var address in addresses)
                    {
                        try
                        {
                            socket.Send(request, request.Length, new IPEndPoint(MapForDualMode(address), Port));
                        }
                        catch (SocketException)
                        {
                            // Unroutable address (for example IPv6 without IPv6 connectivity): skip it.
                        }
                    }
                }

                if (!socket.Client.Poll(20_000, SelectMode.SelectRead))
                {
                    continue;
                }

                var from = new IPEndPoint(IPAddress.IPv6Any, 0);
                byte[] data;
                try
                {
                    data = socket.Receive(ref from);
                }
                catch (SocketException)
                {
                    continue;
                }

                var parts = Encoding.UTF8.GetString(data).Split('|');
                if (parts.Length == 3 && parts[0] == Reply && parts[1] == nonce && int.TryParse(parts[2], out var protocol))
                {
                    var sender = from.Address.IsIPv4MappedToIPv6 ? from.Address.MapToIPv4() : from.Address;
                    return new ProbeResult(sender, protocol, (int)stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Reachability probe failed: {e.Message}");
        }

        return null;
    }

    private static IPAddress MapForDualMode(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork ? address.MapToIPv6() : address;
}

internal sealed class ProbeResult
{
    public ProbeResult(IPAddress address, int protocol, int roundTripMs)
    {
        Address = address;
        Protocol = protocol;
        RoundTripMs = roundTripMs;
    }

    public IPAddress Address { get; }
    public int Protocol { get; }
    public int RoundTripMs { get; }
}
