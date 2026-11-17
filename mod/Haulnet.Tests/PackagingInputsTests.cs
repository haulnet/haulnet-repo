using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Haulnet.Tests;

/// <summary>
/// Checks the files a release is made from against the rules that would otherwise fail only at upload time:
/// Thunderstore's package rules (manifest fields, icon size, README and changelog encoding and length), one version
/// in the project file and in Plugin.cs, and a changelog section for that version (the release notes are made from it).
/// </summary>
public class PackagingInputsTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Packaging");

    private static string Text(string name) => new UTF8Encoding(false, true).GetString(File.ReadAllBytes(Path.Combine(Folder, name)));

    private static string ProjectVersion()
    {
        var match = Regex.Match(Text("Haulnet.csproj"), @"<Version>([^<]+)</Version>");
        Assert.True(match.Success, "Haulnet.csproj has no <Version>");
        return match.Groups[1].Value.Trim();
    }

    [Fact]
    public void VersionIsThreeNumbersAndTheSameInPluginCs()
    {
        var version = ProjectVersion();
        // Thunderstore: Major.Minor.Patch, digits only, no leading zeros.
        Assert.Matches(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", version);
        Assert.Contains($"Version = \"{version}\"", Text("Plugin.cs"));
    }

    [Fact]
    public void ChangelogHasASectionForTheVersion()
    {
        var changelog = Text("CHANGELOG.md");
        Assert.Contains("\n## [Unreleased]\n", changelog);
        Assert.Matches($@"(?m)^## \[{Regex.Escape(ProjectVersion())}\] - \d{{4}}-\d{{2}}-\d{{2}}$", changelog);
    }

    [Fact]
    public void ManifestFollowsThunderstoreRules()
    {
        var manifest = MiniJson.Parse(Text("manifest.json"));
        var keys = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(manifest).Keys.OrderBy(k => k);
        Assert.Equal(new[] { "dependencies", "description", "name", "version_number", "website_url" }, keys);

        Assert.Matches("^[a-zA-Z0-9_]+$", MiniJson.Str(manifest, "name"));
        // The placeholder Packaging.targets replaces with the project version.
        Assert.Equal("0.0.0", MiniJson.Str(manifest, "version_number"));
        Assert.InRange(MiniJson.Str(manifest, "description").Length, 1, 250);
        Assert.StartsWith("https://", MiniJson.Str(manifest, "website_url"));

        var dependencies = MiniJson.Arr(manifest, "dependencies").Select(d => Assert.IsType<string>(d)).ToList();
        Assert.All(dependencies, d => Assert.Matches(@"^[a-zA-Z0-9_]+-[a-zA-Z0-9_]+-\d+\.\d+\.\d+$", d));
        Assert.Equal(dependencies.Count, dependencies.Select(d => d.Substring(0, d.LastIndexOf('-'))).Distinct().Count());
    }

    [Fact]
    public void IconIsA256PixelPng()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, "icon.png"));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes.Take(8));
        Assert.Equal("IHDR", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(256, BigEndian(bytes, 16));
        Assert.Equal(256, BigEndian(bytes, 20));
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("CHANGELOG.md")]
    public void StoreMarkdownIsUtf8WithoutBomAndShortEnough(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, name));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"{name}: save as UTF-8 without a byte-order mark");
        // Throws on bytes that are not UTF-8.
        Assert.InRange(new UTF8Encoding(false, true).GetString(bytes).Length, 1, 100_000);
    }

    private static int BigEndian(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
}
