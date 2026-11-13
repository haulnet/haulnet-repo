using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SemiLink;

/// <summary>A host found on the local network.</summary>
internal sealed class DiscoveredHost
{
    public DiscoveredHost(string name, IPAddress address, int protocol)
    {
        Name = name;
        Address = address;
        Protocol = protocol;
    }

    public string Name { get; }
    public IPAddress Address { get; }
    public int Protocol { get; }
    public DateTime LastSeen { get; set; }
    public bool Compatible => Protocol == LanDiscovery.ProtocolVersion;
}

/// <summary>
/// LAN discovery: a host broadcasts a small UDP announcement every few seconds; joining players listen
/// and list the hosts they hear. Runs on the main thread from <see cref="Tick"/>, no background threads.
/// </summary>
/// <remarks>Announcement: <c>SEMILINK|&lt;protocol&gt;|&lt;name&gt;</c>, UTF-8, on UDP port <see cref="Port"/>.</remarks>
internal sealed class LanDiscovery : IDisposable
{
    /// <summary>SemiLink host/client compatibility version. Bump when hosts and clients stop being compatible.</summary>
    public const int ProtocolVersion = 1;

    public const int Port = 47777;

    private const string Magic = "SEMILINK";
    private static readonly TimeSpan AnnounceInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HostTimeout = TimeSpan.FromSeconds(7);

    private readonly Dictionary<string, DiscoveredHost> _hosts = new();
    private UdpClient? _sender;
    private UdpClient? _listener;
    private DateTime _nextAnnounce;
    private Func<string> _hostName = () => "";

    public IReadOnlyList<DiscoveredHost> Hosts =>
        _hosts.Values.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public void StartAnnouncing(Func<string> hostName)
    {
        StopListening();
        _hostName = hostName;
        if (_sender != null)
        {
            return;
        }

        try
        {
            _sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            _nextAnnounce = DateTime.UtcNow;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"LAN discovery: cannot announce: {e.Message}");
        }
    }

    public void StartListening()
    {
        StopAnnouncing();
        if (_listener != null)
        {
            return;
        }

        try
        {
            var listener = new UdpClient(AddressFamily.InterNetwork);
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            _listener = listener;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"LAN discovery: cannot listen on UDP {Port}: {e.Message}");
        }
    }

    public void StopAll()
    {
        StopAnnouncing();
        StopListening();
    }

    /// <summary>Call every frame.</summary>
    public void Tick()
    {
        var now = DateTime.UtcNow;
        if (_sender != null && now >= _nextAnnounce)
        {
            _nextAnnounce = now + AnnounceInterval;
            var name = _hostName().Replace("|", "/");
            var payload = Encoding.UTF8.GetBytes($"{Magic}|{ProtocolVersion}|{name}");
            foreach (var target in NetworkAddresses.BroadcastAddresses())
            {
                try
                {
                    _sender.Send(payload, payload.Length, new IPEndPoint(target, Port));
                }
                catch (SocketException)
                {
                    // Some adapters reject broadcasts; others may still deliver it.
                }
            }
        }

        if (_listener != null)
        {
            try
            {
                while (_listener.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = _listener.Receive(ref from);
                    Handle(data, from.Address, now);
                }
            }
            catch (SocketException)
            {
            }

            foreach (var key in _hosts.Where(p => now - p.Value.LastSeen > HostTimeout).Select(p => p.Key).ToList())
            {
                _hosts.Remove(key);
            }
        }
    }

    public void Dispose() => StopAll();

    private void Handle(byte[] data, IPAddress from, DateTime now)
    {
        if (data.Length > 512)
        {
            return;
        }

        var parts = Encoding.UTF8.GetString(data).Split('|');
        if (parts.Length < 3 || parts[0] != Magic || !int.TryParse(parts[1], out var protocol))
        {
            return;
        }

        var name = parts[2].Length > 40 ? parts[2].Substring(0, 40) : parts[2];
        var key = from.ToString();
        if (!_hosts.TryGetValue(key, out var host) || host.Name != name || host.Protocol != protocol)
        {
            host = new DiscoveredHost(name, from, protocol);
            _hosts[key] = host;
        }

        host.LastSeen = now;
    }

    private void StopAnnouncing()
    {
        _sender?.Close();
        _sender = null;
    }

    private void StopListening()
    {
        _listener?.Close();
        _listener = null;
        _hosts.Clear();
    }
}
