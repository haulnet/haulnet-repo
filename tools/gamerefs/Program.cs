using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Haulnet.GameRefs;

/// <summary>
/// Makes reference assemblies for building Haulnet where the game is not installed (the release workflow).
///
/// For every assembly Haulnet.csproj references from the game folder, this writes a copy that keeps the metadata
/// (types, members, signatures, attributes) and nothing else: every method body becomes <c>throw null</c>, embedded
/// resources and initial field data are removed. The compiler needs only the metadata, so the mod builds against the
/// copies exactly as against the game; the copies cannot run anything and contain no game code, text or assets.
///
/// The output has the layout of a game folder (REPO_Data/Managed, BepInEx/core), so a build takes it as GameDir.
/// It is made from a licensed installation, stays in the project's private store and is never published.
/// </summary>
internal static class Program
{
    private const string Usage =
        "Usage: gamerefs <GameDir> <OutDir> --game-version <text> [--project <path to Haulnet.csproj>]\n" +
        "  GameDir         folder that contains REPO.exe, with BepInEx 5 installed\n" +
        "  OutDir          empty or missing folder to write the reference assemblies to\n" +
        "  --game-version  the version the game's main menu shows, e.g. v0.5.1\n" +
        "  --project       default: mod/Haulnet/Haulnet.csproj under the current directory";

    // The same properties Haulnet.csproj uses in its HintPaths, with the folder each one stands for.
    private static readonly Dictionary<string, string> Folders = new()
    {
        ["ManagedDir"] = Path.Combine("REPO_Data", "Managed"),
        ["BepInExCoreDir"] = Path.Combine("BepInEx", "core"),
    };

    // Steam's manifest of the game, two folders above GameDir; names the installed build.
    private const string SteamManifest = "appmanifest_3241660.acf";

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or BadImageFormatException)
        {
            Console.Error.WriteLine($"gamerefs: {e.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var positional = new List<string>();
        string? gameVersion = null;
        var project = Path.Combine("mod", "Haulnet", "Haulnet.csproj");
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game-version" when i + 1 < args.Length:
                    gameVersion = args[++i];
                    break;
                case "--project" when i + 1 < args.Length:
                    project = args[++i];
                    break;
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count != 2 || string.IsNullOrWhiteSpace(gameVersion))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var gameDir = Path.GetFullPath(positional[0]);
        var outDir = Path.GetFullPath(positional[1]);
        if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
        {
            throw new IOException($"{outDir} is not empty; give an empty or missing folder.");
        }

        var references = ReadReferences(project);
        if (references.Count == 0)
        {
            throw new InvalidDataException($"{project} has no references with a $(ManagedDir) or $(BepInExCoreDir) HintPath.");
        }

        var lines = new List<string>();
        foreach (var relative in references)
        {
            var source = Path.Combine(gameDir, relative);
            var target = Path.Combine(outDir, relative);
            if (!File.Exists(source))
            {
                throw new IOException($"{source} not found; is GameDir an R.E.P.O. installation with BepInEx 5?");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Strip(source, target);
            Verify(target);
            lines.Add($"{Sha256(target)}  {relative.Replace('\\', '/')}  (from {Sha256(source)}, {new FileInfo(source).Length} bytes)");
            Console.WriteLine($"{relative}: {new FileInfo(source).Length} -> {new FileInfo(target).Length} bytes");
        }

        var info = new StringBuilder();
        info.Append("Reference assemblies for building Haulnet (https://github.com/haulnet/haulnet-repo, tools/gamerefs).\n");
        info.Append("Metadata only: every method body is `throw null`; resources and field data are removed.\n");
        info.Append("Made from a licensed installation. Private build input: do not publish or redistribute.\n\n");
        info.Append($"game_version={gameVersion!.Trim()}\n");
        info.Append($"steam_build_id={SteamBuildId(gameDir) ?? "unknown"}\n");
        info.Append($"created={DateTime.UtcNow:yyyy-MM-dd}\n\n");
        info.Append("SHA-256 of each reference assembly, its path, and the SHA-256 and size of the file it was made from:\n");
        foreach (var line in lines)
        {
            info.Append(line).Append('\n');
        }

        File.WriteAllText(Path.Combine(outDir, "GAMEREFS.txt"), info.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{references.Count} reference assemblies written to {outDir}");
        return 0;
    }

    /// <summary>The assemblies the mod references from the game folder, read from the project so the list lives in one place.</summary>
    private static List<string> ReadReferences(string project)
    {
        var text = File.ReadAllText(project);
        var result = new List<string>();
        foreach (Match m in Regex.Matches(text, @"HintPath=""\$\((?<dir>\w+)\)\\(?<file>[^""\\]+\.dll)"""))
        {
            if (Folders.TryGetValue(m.Groups["dir"].Value, out var folder))
            {
                result.Add(Path.Combine(folder, m.Groups["file"].Value));
            }
        }

        return result;
    }

    private static void Strip(string source, string target)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(source)!);
        using var assembly = AssemblyDefinition.ReadAssembly(source,
            new ReaderParameters { AssemblyResolver = resolver, InMemory = true, ReadingMode = ReadingMode.Immediate });

        foreach (var module in assembly.Modules)
        {
            module.Resources.Clear();
            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    var body = new MethodBody(method);
                    var il = body.GetILProcessor();
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Throw);
                    method.Body = body;
                }

                // Array initializers and similar data blobs (fields with an RVA).
                foreach (var field in type.Fields.Where(f => f.InitialValue.Length > 0))
                {
                    field.InitialValue = Array.Empty<byte>();
                    field.Attributes &= ~FieldAttributes.HasFieldRVA;
                }
            }
        }

        assembly.Write(target);
    }

    /// <summary>Reads the written file back: no method may have more than the two-instruction body, nothing may carry data.</summary>
    private static void Verify(string path)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { InMemory = true });
        foreach (var module in assembly.Modules)
        {
            if (module.Resources.Count > 0)
            {
                throw new InvalidDataException($"{path} still has embedded resources.");
            }

            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    var code = method.Body.Instructions;
                    if (code.Count != 2 || code[0].OpCode != OpCodes.Ldnull || code[1].OpCode != OpCodes.Throw)
                    {
                        throw new InvalidDataException($"{path}: {method.FullName} still has code.");
                    }
                }

                if (type.Fields.Any(f => f.InitialValue.Length > 0))
                {
                    throw new InvalidDataException($"{path}: {type.FullName} still has field data.");
                }
            }
        }
    }

    private static string? SteamBuildId(string gameDir)
    {
        var manifest = Path.GetFullPath(Path.Combine(gameDir, "..", "..", SteamManifest));
        if (!File.Exists(manifest))
        {
            return null;
        }

        var m = Regex.Match(File.ReadAllText(manifest), @"""buildid""\s+""(\d+)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
