using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Haulnet.Tests;

public class TranslationsTests
{
    [Fact]
    public void ParseSkipsCommentsAndBlankLinesAndUnescapesLineBreaks()
    {
        var errors = new List<string>();
        var table = Translations.Parse("﻿# comment\r\n\r\nA\tone\\ntwo\r\n  # indented comment\nB\t\n", "test", errors);

        Assert.Empty(errors);
        Assert.Equal(2, table.Count);
        Assert.Equal("one\ntwo", table["A"]);
        Assert.Equal("", table["B"]);
    }

    [Fact]
    public void ParseReportsDuplicatesAndLinesWithoutTab()
    {
        var errors = new List<string>();
        var table = Translations.Parse("A\tfirst\nA\tsecond\nno tab here\n", "test", errors);

        Assert.Equal("first", table["A"]);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("duplicate key A"));
        Assert.Contains(errors, e => e.Contains("test.tsv:3"));
    }

    [Theory]
    [InlineData("uk-UA", "uk-UA,uk,en")]
    [InlineData("pt-BR", "pt-BR,pt,en")]
    [InlineData("ru", "ru,en")]
    [InlineData("en-US", "en-US,en")]
    [InlineData("en", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void CandidatesGoFromLocaleToLanguageToEnglish(string? locale, string expected)
    {
        Assert.Equal(expected, string.Join(",", Translations.Candidates(locale)));
    }

    [Fact]
    public void GetFallsBackPerKey()
    {
        var files = new Dictionary<string, string>
        {
            ["en"] = "A\tEnglish A\nB\tEnglish B\n",
            ["uk"] = "A\tУкраїнська A\n",
        };
        var strings = new Translations(name => files.TryGetValue(name, out var text) ? text : null);

        Assert.Equal("Українська A", strings.Get("uk-UA", "A"));
        Assert.Equal("English B", strings.Get("uk-UA", "B"));
        Assert.Equal("English A", strings.Get("de-DE", "A"));
        Assert.Null(strings.Get("uk-UA", "C"));
    }

    [Fact]
    public void FormatReplacesNamedPlaceholdersInAnyOrder()
    {
        Assert.Equal("27002 - 27000", Translations.Format("{last} - {first}", ("first", 27000), ("last", 27002)));
        Assert.Equal("a {unknown} b", Translations.Format("{x} {unknown} {y}", ("x", "a"), ("y", "b")));
        Assert.Equal("no placeholders", Translations.Format("no placeholders", ("x", 1)));
        Assert.Equal("1.5", Translations.Format("{v}", ("v", 1.5)));
    }

    [Fact]
    public void PlaceholdersAreFound()
    {
        Assert.Equal(new[] { "first", "last" }, Translations.Placeholders("UDP {first}-{last}").OrderBy(n => n));
        Assert.Empty(Translations.Placeholders("no {placeholders here}"));
    }
}
