using System;
using System.Collections.Generic;
using System.Linq;

namespace Haulnet;

/// <summary>A server listed on the region page: saved by hand in the config, or played on recently.</summary>
internal sealed class KnownServer
{
    public KnownServer(ServerAddress address, string name, bool saved)
    {
        Address = address;
        Name = name;
        Saved = saved;
    }

    public ServerAddress Address { get; }

    /// <summary>Name from the config or from the server's status endpoint; may be empty.</summary>
    public string Name { get; }

    public bool Saved { get; }

    public string Display => Name.Length > 0 ? $"{Name} · {Address.Display}" : Address.Display;
}

/// <summary>
/// Saved servers (config, edited by hand) and recent servers (filled automatically, like Valheim's "Recent").
/// There is no in-game editing: the game has no list-editing components, and old recent entries drop out.
/// </summary>
internal static class KnownServers
{
    private const int MaxRecent = 5;
    private const int MaxNameLength = 32;

    /// <summary>Saved servers first, then recent ones that are not saved.</summary>
    public static List<KnownServer> All()
    {
        var saved = ParseSaved(Plugin.Settings.SavedServers);
        var recent = ParseRecent(Plugin.Settings.RecentServers)
            .Where(r => !saved.Any(s => s.Address.SameAs(r.Address)));
        return saved.Concat(recent).ToList();
    }

    /// <summary>Puts a server at the top of the recent list after a successful connection.</summary>
    public static void AddRecent(ServerAddress address, string name)
    {
        var list = ParseRecent(Plugin.Settings.RecentServers);
        var previous = list.FirstOrDefault(r => r.Address.SameAs(address));
        if (name.Length == 0 && previous != null)
        {
            name = previous.Name;
        }

        list.RemoveAll(r => r.Address.SameAs(address));
        list.Insert(0, new KnownServer(address, CleanName(name), saved: false));
        Plugin.Settings.RecentServers = string.Join(";", list.Take(MaxRecent).Select(r =>
            r.Name.Length > 0 ? $"{r.Address}|{r.Name}" : r.Address.ToString()));
    }

    /// <summary>Makes a name safe for the config format and for display (no rich-text tags, short).</summary>
    public static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var chars = name!.Where(c => !char.IsControl(c) && c != ';' && c != '|' && c != '=' && c != '<' && c != '>').ToArray();
        var clean = new string(chars).Trim();
        return clean.Length > MaxNameLength ? clean.Substring(0, MaxNameLength).TrimEnd() : clean;
    }

    private static List<KnownServer> ParseSaved(string text)
    {
        var result = new List<KnownServer>();
        foreach (var entry in Split(text))
        {
            var split = entry.IndexOf('=');
            var name = split > 0 ? entry.Substring(0, split) : "";
            var address = split > 0 ? entry.Substring(split + 1) : entry;
            if (ServerAddress.TryParse(address, out var parsed, out _))
            {
                result.Add(new KnownServer(parsed, CleanName(name), saved: true));
            }
            else
            {
                Plugin.Log.LogWarning($"Config [Servers] Saved: ignoring '{entry}' (not a valid address).");
            }
        }

        return result;
    }

    private static List<KnownServer> ParseRecent(string text)
    {
        var result = new List<KnownServer>();
        foreach (var entry in Split(text))
        {
            var split = entry.IndexOf('|');
            var address = split >= 0 ? entry.Substring(0, split) : entry;
            var name = split >= 0 ? entry.Substring(split + 1) : "";
            if (ServerAddress.TryParse(address, out var parsed, out _))
            {
                result.Add(new KnownServer(parsed, CleanName(name), saved: false));
            }
        }

        return result;
    }

    private static IEnumerable<string> Split(string text) =>
        text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0);
}
