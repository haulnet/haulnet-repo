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

    private static readonly string CacheDir = Path.Combine(BepInEx.Paths.CachePath, "Haulnet");
    private static readonly string CacheFile = Path.Combine(CacheDir, "servers.json");
    private static readonly string EtagFile = Path.Combine(CacheDir, "servers.etag");

    private static List<RegistryCity>? _cities;
    private static string[] _compatibleLuxon = Array.Empty<string>();
    private static bool _fetching;

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

        yield return request.SendWebRequest();
        _fetching = false;

        var changed = false;
        if (request.responseCode == 304)
        {
            Plugin.Log.LogInfo("Server list: not modified.");
        }
        else if (request.result == UnityWebRequest.Result.Success)
        {
            var text = request.downloadHandler.text ?? "";
            var parsed = Parse(text, out var compatible, out var error);
            if (parsed == null)
            {
                Plugin.Log.LogWarning($"Server list from {url} rejected ({error}); keeping the cached copy.");
            }
            else
            {
                _cities = parsed;
                _compatibleLuxon = compatible;
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

        var parsed = Parse(text, out var compatible, out _);
        _compatibleLuxon = compatible;
        return parsed ?? new List<RegistryCity>();
    }

    /// <summary>Validates and converts the published list. Unknown fields are ignored.</summary>
    private static List<RegistryCity>? Parse(string text, out string[] compatible, out string error)
    {
        compatible = Array.Empty<string>();
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
