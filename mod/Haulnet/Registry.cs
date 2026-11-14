using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Haulnet;

/// <summary>A community server from the registry (haulnet-servers, published list v1).</summary>
internal sealed class RegistryServer
{
    public RegistryServer(string id, string displayName, string cityId, ServerAddress address, int statusPort)
    {
        Id = id;
        DisplayName = displayName;
        CityId = cityId;
        Address = address;
        StatusPort = statusPort;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string CityId { get; }
    public ServerAddress Address { get; }
    public int StatusPort { get; }
}

/// <summary>A city with at least one server: one row on the region page.</summary>
internal sealed class RegistryCity
{
    public RegistryCity(string id, string displayName, List<RegistryServer> servers)
    {
        Id = id;
        DisplayName = displayName;
        Servers = servers;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public List<RegistryServer> Servers { get; }
}

/// <summary>
/// Downloads the public server list (contract: docs/server-registry.md in the workspace, URL configurable) and
/// keeps the last good copy on disk, so the list also works offline. The fetch runs when the region page opens;
/// a 304 or a failure keeps the cached list.
/// </summary>
internal static class Registry
{
    private const int TimeoutSeconds = 5;

    // The monitor's uptime changes slowly; a failed fetch is retried sooner.
    private const float StabilityMaxAgeSeconds = 300f;
    private const float StabilityRetrySeconds = 60f;

    private static readonly string CacheDir = Path.Combine(BepInEx.Paths.CachePath, "Haulnet");
    private static readonly string CacheFile = Path.Combine(CacheDir, "servers.json");
    private static readonly string EtagFile = Path.Combine(CacheDir, "servers.etag");

    private static List<RegistryCity>? _cities;
    private static string[] _compatibleLuxon = Array.Empty<string>();
    private static string _statusUrl = "";
    private static bool _fetching;

    private static Dictionary<string, int> _penalties = new();
    private static string _penaltiesUrl = "";
    private static string _stabilityTriedUrl = "";
    private static float _stabilityTriedAt = float.NegativeInfinity;
    private static bool _stabilityOk;
    private static string _stabilityFetchingUrl = "";

    /// <summary>Cities with servers, from the last good list (empty when there is none).</summary>
    public static IReadOnlyList<RegistryCity> Cities => _cities ??= ParseOrEmpty(ReadCache());

    /// <summary>LuxonServer versions that work with the current game, from the list.</summary>
    public static IReadOnlyList<string> CompatibleLuxon
    {
        get
        {
            _ = Cities;
            return _compatibleLuxon;
        }
    }

    /// <summary>
    /// The registry monitor's live document named by the list (<c>status_url</c>, optional), or empty. The list names
    /// it, not the mod, so the monitor can move and a community list can bring its own (ADR 0024).
    /// </summary>
    public static string StatusUrl
    {
        get
        {
            _ = Cities;
            return _statusUrl;
        }
    }

    /// <summary>True when no fetch of the current list's monitor is running: the penalties are as good as they get for now.</summary>
    public static bool StabilitySettled => _stabilityFetchingUrl.Length == 0 || _stabilityFetchingUrl != StatusUrl;

    /// <summary>Stability penalty of a server from the monitor's uptime; 0 when unknown.</summary>
    public static int PenaltyOf(string serverId) =>
        _penaltiesUrl == StatusUrl && _penalties.TryGetValue(serverId, out var penalty) ? penalty : 0;

    /// <summary>
    /// Fetches the monitor's uptime when the list names a monitor and the last copy is old. Kept in memory only; a
    /// failure keeps the last copy, and without any copy the ranking uses the ping alone.
    /// </summary>
    public static IEnumerator RefreshStability()
    {
        var url = StatusUrl;
        var age = Time.realtimeSinceStartup - _stabilityTriedAt;
        if (url.Length == 0 || url == _stabilityFetchingUrl
            || (url == _stabilityTriedUrl && age < (_stabilityOk ? StabilityMaxAgeSeconds : StabilityRetrySeconds)))
        {
            yield break;
        }

        _stabilityFetchingUrl = url;
        _stabilityTriedAt = Time.realtimeSinceStartup;
        _stabilityTriedUrl = url;
        using var request = UnityWebRequest.Get(url);
        request.timeout = TimeoutSeconds;
        UnityWebRequestAsyncOperation? operation = null;
        var error = "";
        try
        {
            operation = request.SendWebRequest();
        }
        catch (Exception e)
        {
            // The game refuses plain http to other hosts ("Insecure connection not allowed").
            error = e.Message;
        }

        if (operation != null)
        {
            yield return operation;
            error = request.error ?? "";
        }

        if (_stabilityFetchingUrl == url)
        {
            _stabilityFetchingUrl = "";
        }

        if (url != StatusUrl)
        {
            yield break; // A fresh list named another monitor meanwhile; its own fetch decides.
        }

        Dictionary<string, int>? penalties = null;
        if (operation != null && request.result == UnityWebRequest.Result.Success)
        {
            penalties = StabilityData.ParsePenalties(request.downloadHandler.text ?? "", out error);
        }

        _stabilityOk = penalties != null;
        if (penalties == null)
        {
            Plugin.Log.LogInfo($"Server stability from {url} not loaded ({error}); {(url == _penaltiesUrl ? "keeping the last copy" : "ranking by ping only")}.");
            yield break;
        }

        _penalties = penalties;
        _penaltiesUrl = url;
        Plugin.Log.LogInfo($"Server stability: {penalties.Count} server(s) from {url}, {penalties.Count(p => p.Value > 0)} with a penalty.");
    }

    /// <summary>Fetches the list; <paramref name="done"/> runs on the main thread with true when it changed.</summary>
    public static IEnumerator Refresh(Action<bool> done)
    {
        if (_fetching)
        {
            yield break;
        }

        _fetching = true;
        var url = Plugin.Settings.RegistryUrl;
        using var request = UnityWebRequest.Get(url);
        request.timeout = TimeoutSeconds;
        var etag = File.Exists(EtagFile) && File.Exists(CacheFile) ? SafeRead(EtagFile) : "";
        if (etag.Length > 0 && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            request.SetRequestHeader("If-None-Match", etag);
        }

        UnityWebRequestAsyncOperation? operation = null;
        var sendError = "";
        try
        {
            operation = request.SendWebRequest();
        }
        catch (Exception e)
        {
            // The game refuses plain http to other hosts ("Insecure connection not allowed"); an escaping exception
            // would leave _fetching set and the list would never be fetched again in this game session.
            sendError = e.Message;
        }

        if (operation == null)
        {
            _fetching = false;
            var hint = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? " The game allows plain http:// only to this PC; use https:// for other hosts."
                : "";
            Plugin.Log.LogWarning($"Server list from {url} not loaded ({sendError}); using the cached copy.{hint}");
            done(false);
            yield break;
        }

        yield return operation;
        _fetching = false;

        var changed = false;
        if (request.responseCode == 304)
        {
            Plugin.Log.LogInfo("Server list: not modified.");
        }
        else if (request.result == UnityWebRequest.Result.Success)
        {
            var text = request.downloadHandler.text ?? "";
            var parsed = Parse(text, out var compatible, out var statusUrl, out var error);
            if (parsed == null)
            {
                Plugin.Log.LogWarning($"Server list from {url} rejected ({error}); keeping the cached copy.");
            }
            else
            {
                _cities = parsed;
                _compatibleLuxon = compatible;
                _statusUrl = statusUrl;
                changed = true;
                WriteCache(text, request.GetResponseHeader("ETag") ?? "");
                Plugin.Log.LogInfo($"Server list: {parsed.Sum(c => c.Servers.Count)} server(s) in {parsed.Count} city(ies) from {url}.");
            }
        }
        else
        {
            Plugin.Log.LogWarning($"Server list from {url} not loaded ({request.error}); using the cached copy.");
        }

        done(changed);
    }

    private static List<RegistryCity> ParseOrEmpty(string text)
    {
        if (text.Length == 0)
        {
            return new List<RegistryCity>();
        }

        var parsed = Parse(text, out var compatible, out var statusUrl, out _);
        _compatibleLuxon = compatible;
        _statusUrl = statusUrl;
        return parsed ?? new List<RegistryCity>();
    }

    /// <summary>Validates and converts the published list. Unknown fields are ignored.</summary>
    private static List<RegistryCity>? Parse(string text, out string[] compatible, out string statusUrl, out string error)
    {
        compatible = Array.Empty<string>();
        statusUrl = "";
        error = "";
        object? root;
        try
        {
            root = MiniJson.Parse(text);
        }
        catch (Exception e)
        {
            error = "not JSON: " + e.Message;
            return null;
        }

        var version = MiniJson.Int(root, "schema_version");
        if (version != 1)
        {
            error = $"schema_version {version}, expected 1";
            return null;
        }

        compatible = MiniJson.Arr(MiniJson.Obj(root, "compatibility"), "luxon").OfType<string>().ToArray();
        var monitor = MiniJson.Str(root, "status_url");
        statusUrl = Uri.TryCreate(monitor, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : "";
        var servers = MiniJson.Arr(root, "servers");
        var cities = new List<RegistryCity>();
        foreach (var city in MiniJson.Arr(root, "cities"))
        {
            var cityId = MiniJson.Str(city, "id");
            if (cityId.Length == 0)
            {
                continue;
            }

            var list = new List<RegistryServer>();
            foreach (var server in servers.Where(s => MiniJson.Str(s, "city") == cityId))
            {
                var host = MiniJson.Str(server, "host");
                var hostText = host.Contains(":") ? $"[{host}]" : host;
                if (ServerAddress.TryParse($"{hostText}:{MiniJson.Int(server, "port")}", out var address, out _))
                {
                    var statusPort = MiniJson.Int(server, "status_port");
                    list.Add(new RegistryServer(
                        MiniJson.Str(server, "id"),
                        KnownServers.CleanName(MiniJson.Str(server, "display_name")),
                        cityId,
                        address,
                        statusPort > 0 ? statusPort : ServerCheck.StatusPort));
                }
            }

            if (list.Count > 0)
            {
                cities.Add(new RegistryCity(cityId, KnownServers.CleanName(MiniJson.Str(city, "display_name")), list));
            }
        }

        return cities;
    }

    private static string ReadCache() => File.Exists(CacheFile) ? SafeRead(CacheFile) : "";

    private static void WriteCache(string text, string etag)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(CacheFile, text);
            File.WriteAllText(EtagFile, etag);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not cache the server list: {e.Message}");
        }
    }

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception)
        {
            return "";
        }
    }
}
