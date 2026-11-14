using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Haulnet.Tests;

/// <summary>
/// Checks every file in mod/Haulnet/Localization against the English source (ADR 0018). Failures: broken lines,
/// duplicate keys, keys that English does not have, placeholders that differ from English, a byte-order mark.
/// Reported only: missing keys (English is shown instead) and texts longer than the "#@max" limit in en.tsv.
/// </summary>
public class TranslationFilesTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Localization");

    private readonly ITestOutputHelper _output;

    public TranslationFilesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Languages() =>
        Directory.GetFiles(Folder, "*.tsv").Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    [Fact]
    public void EnglishSourceExists()
    {
        Assert.True(File.Exists(Path.Combine(Folder, "en.tsv")), "en.tsv is the source and must exist");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void FileMatchesTheEnglishSource(string language)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, language + ".tsv"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"{language}.tsv: save as UTF-8 without a byte-order mark");

        var errors = new List<string>();
        var table = Translations.Parse(new UTF8Encoding(false, true).GetString(bytes), language, errors);
        var english = Translations.Parse(Read("en"), "en");

        foreach (var (key, text) in table)
        {
            if (!english.TryGetValue(key, out var source))
            {
                errors.Add($"{language}.tsv: key {key} is not in en.tsv");
                continue;
            }

            var expected = Translations.Placeholders(source);
            var actual = Translations.Placeholders(text);
            if (!expected.SetEquals(actual))
            {
                errors.Add($"{language}.tsv: {key} uses {{{string.Join("}, {", actual.OrderBy(n => n))}}}, " +
                           $"English uses {{{string.Join("}, {", expected.OrderBy(n => n))}}}");
            }
        }

        var missing = english.Keys.Where(k => !table.ContainsKey(k)).ToList();
        _output.WriteLine($"{language}: {table.Count}/{english.Count} keys" +
                          (missing.Count > 0 ? $", missing (English shown): {string.Join(", ", missing)}" : ""));

        var limits = Limits();
        foreach (var (key, text) in table)
        {
            if (limits.TryGetValue(key, out var max) && text.Length > max)
            {
                _output.WriteLine($"{language}: {key} has {text.Length} characters, limit {max}: {text}");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static string Read(string language) => File.ReadAllText(Path.Combine(Folder, language + ".tsv"), Encoding.UTF8);

    /// <summary>Key → limit from the "#@max N" lines in en.tsv (each applies to the keys below it).</summary>
    private static Dictionary<string, int> Limits()
    {
        var limits = new Dictionary<string, int>();
        var current = 0;
        foreach (var raw in Read("en").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("#@max ", StringComparison.Ordinal))
            {
                current = int.Parse(line.Substring(6).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (current > 0 && !line.StartsWith("#", StringComparison.Ordinal) && line.IndexOf('\t') > 0)
            {
                limits[line.Substring(0, line.IndexOf('\t'))] = current;
            }
        }

        return limits;
    }
}
