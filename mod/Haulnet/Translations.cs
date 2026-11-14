using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Haulnet;

/// <summary>
/// Haulnet's own strings, one TSV file per language (ADR 0018), in the game's override format: <c>KEY&lt;TAB&gt;text</c>,
/// <c>\n</c> for a line break, lines starting with <c>#</c> are comments. English (<c>en</c>) is the source and the
/// fallback for every key. No Unity references, so the test project can use this file directly.
/// </summary>
internal sealed class Translations
{
    public const string SourceLanguage = "en";

    private static readonly Regex PlaceholderPattern = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.CultureInvariant);

    private readonly Func<string, string?> _loadFile;
    private readonly Dictionary<string, Dictionary<string, string>?> _tables = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="loadFile">Returns the content of <c>&lt;name&gt;.tsv</c> (for example <c>uk</c>), or null.</param>
    public Translations(Func<string, string?> loadFile)
    {
        _loadFile = loadFile;
    }

    /// <summary>Problems found while parsing (duplicate keys, lines without a tab), for the log.</summary>
    public List<string> LoadErrors { get; } = new();

    /// <summary>
    /// The text for <paramref name="key"/> in the game's locale (for example <c>uk-UA</c>): the exact code, then the
    /// language part (<c>uk</c>), then English. Null when even English has no such key.
    /// </summary>
    public string? Get(string? localeCode, string key)
    {
        foreach (var name in Candidates(localeCode))
        {
            var table = Table(name);
            if (table != null && table.TryGetValue(key, out var text))
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>The file names tried for a locale code, most specific first; English is always last.</summary>
    public static IEnumerable<string> Candidates(string? localeCode)
    {
        var code = (localeCode ?? "").Trim();
        if (code.Length > 0 && !code.Equals(SourceLanguage, StringComparison.OrdinalIgnoreCase))
        {
            yield return code;
            var dash = code.IndexOf('-');
            if (dash > 0)
            {
                var language = code.Substring(0, dash);
                if (!language.Equals(SourceLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    yield return language;
                }
            }
        }

        yield return SourceLanguage;
    }

    private Dictionary<string, string>? Table(string name)
    {
        if (_tables.TryGetValue(name, out var table))
        {
            return table;
        }

        var content = _loadFile(name);
        table = content == null ? null : Parse(content, name, LoadErrors);
        _tables[name] = table;
        return table;
    }

    /// <summary>Parses one TSV file. Later duplicates are ignored and reported.</summary>
    public static Dictionary<string, string> Parse(string content, string fileName, List<string>? errors = null)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (i == 0)
            {
                line = line.TrimStart('﻿');
            }

            if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var tab = line.IndexOf('\t');
            if (tab <= 0)
            {
                errors?.Add($"{fileName}.tsv:{i + 1}: expected KEY<TAB>text");
                continue;
            }

            var key = line.Substring(0, tab).Trim();
            var text = line.Substring(tab + 1).Replace("\\n", "\n");
            if (table.ContainsKey(key))
            {
                errors?.Add($"{fileName}.tsv:{i + 1}: duplicate key {key}");
                continue;
            }

            table[key] = text;
        }

        return table;
    }

    /// <summary>The placeholder names used in a text, for example <c>{server}</c> → <c>server</c>.</summary>
    public static HashSet<string> Placeholders(string text)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in PlaceholderPattern.Matches(text))
        {
            names.Add(match.Groups[1].Value);
        }

        return names;
    }

    /// <summary>Replaces <c>{name}</c> with the argument of that name; unknown placeholders stay as they are.</summary>
    public static string Format(string text, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0 || text.IndexOf('{') < 0)
        {
            return text;
        }

        return PlaceholderPattern.Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            foreach (var (argName, value) in args)
            {
                if (argName == name)
                {
                    return value is IFormattable formattable
                        ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        : value?.ToString() ?? "";
                }
            }

            return match.Value;
        });
    }

    /// <summary>Reads an embedded file as UTF-8 text, or null when it does not exist.</summary>
    public static string? ReadResource(System.Reflection.Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using var reader = new System.IO.StreamReader(stream, new UTF8Encoding(false));
        return reader.ReadToEnd();
    }
}
