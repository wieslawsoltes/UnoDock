using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnoDock.SourceMaintenance;
/// <summary>Syntax-aware top-level type organization. Nested types retain their
/// declaring type; generated files and pinned original-reference probes are excluded.</summary>
internal static partial class Program
{
    private static readonly CSharpParseOptions[] Configurations = [new(LanguageVersion.Preview), new(LanguageVersion.Preview, preprocessorSymbols: ["WINDOWS"]), new(LanguageVersion.Preview, preprocessorSymbols: ["DEBUG"]), new(LanguageVersion.Preview, preprocessorSymbols: ["WINDOWS", "DEBUG"])];
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] is not ("--apply" or "--check" or "--format" or "--check-format"))
            {
                throw new ArgumentException("Usage: SourceMaintenance --apply|--check|--format|--check-format <repository>");
            }

            var root = Path.GetFullPath(args[1]);
            if (args[0] is "--format" or "--check-format")
            {
                return FormatSources(root, args[0] == "--check-format");
            }

            var files = Files(root).ToArray();
            var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var moves = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var units = Configurations.SelectMany(configuration => Types(Parse(text, configuration))).GroupBy(Key).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                if (units.Count <= 1)
                {
                    continue;
                }

                if (args[0] == "--check")
                {
                    throw new InvalidOperationException($"Multiple top-level types in {Path.GetRelativePath(root, file)}: {string.Join(", ", units.Keys)}");
                }

                if (units.Values.Any(node => node.Modifiers().Any(SyntaxKind.FileKeyword)))
                {
                    throw new InvalidOperationException($"File-local type needs manual organization: {file}");
                }

                var stem = Path.GetFileNameWithoutExtension(file);
                var hasStatements = Configurations.Any(configuration => Parse(text, configuration).Members.OfType<GlobalStatementSyntax>().Any());
                var primary = hasStatements ? null : units.FirstOrDefault(pair => stem == Name(pair.Value) || stem.StartsWith(Name(pair.Value) + ".", StringComparison.Ordinal)).Key;
                var outputs = new List<string>();
                foreach (var pair in units.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var primaryUnit = pair.Key == primary;
                    var destination = primaryUnit ? file : Path.Combine(Path.GetDirectoryName(file)!, Name(pair.Value) + ".cs");
                    if (!primaryUnit && (File.Exists(destination) || writes.ContainsKey(destination)))
                    {
                        destination = Path.Combine(Path.GetDirectoryName(file)!, Name(pair.Value) + "." + stem + ".cs");
                    }

                    if (!primaryUnit && (File.Exists(destination) || writes.ContainsKey(destination)))
                    {
                        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Key)))[..8];
                        destination = Path.Combine(Path.GetDirectoryName(file)!, Name(pair.Value) + "." + suffix + ".cs");
                    }

                    if (writes.ContainsKey(destination) || !primaryUnit && File.Exists(destination))
                    {
                        throw new InvalidOperationException("Refusing to overwrite a source file: " + destination);
                    }

                    writes.Add(destination, Retain(text, pair.Key, false, primaryUnit));
                    outputs.Add(destination);
                }

                if (hasStatements)
                {
                    writes.Add(file, Retain(text, null, true, true));
                    outputs.Add(file);
                }
                else if (primary == null)
                {
                    // Keep assembly/module attributes exactly once, even when the
                    // original generic filename does not match any extracted type.
                    var first = outputs[0];
                    writes[first] = Retain(text, units.OrderBy(pair => pair.Key, StringComparer.Ordinal).First().Key, false, true);
                }

                foreach (var configuration in Configurations)
                {
                    var before = Fingerprints(text, configuration);
                    var after = outputs.SelectMany(path => Fingerprints(writes[path], configuration)).Order(StringComparer.Ordinal).ToArray();
                    if (!before.SequenceEqual(after, StringComparer.Ordinal))
                    {
                        throw new InvalidOperationException($"Type-token preservation failed: {file}; symbols={string.Join(',', configuration.PreprocessorSymbolNames)}");
                    }
                }

                moves.Add(file, outputs.ToArray());
            }

            if (args[0] == "--check")
            {
                Console.WriteLine($"Source layout verified: {files.Length} handwritten C# files; one top-level type identity per file.");
                return 0;
            }

            // Plan and token checks finish before any original file is removed.
            foreach (var(file, text)in writes)
            {
                File.WriteAllText(file, text, new UTF8Encoding(false));
            }

            foreach (var(file, outputs)in moves)
            {
                if (!outputs.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }

            UpdateProjectIncludes(root, moves);
            Directory.CreateDirectory(Path.Combine(root, "artifacts"));
            var report = moves.ToDictionary(pair => Path.GetRelativePath(root, pair.Key).Replace('\\', '/'), pair => pair.Value.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray());
            File.WriteAllText(Path.Combine(root, "artifacts/source-organization.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            foreach (var(source, destinations)in report)
            {
                Console.WriteLine($"{source} -> {string.Join(", ", destinations)}");
            }

            Console.WriteLine($"Organized {moves.Count} multi-type files into {moves.Values.Sum(paths => paths.Length)} files; all declaration token sequences retained in four compile configurations.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static IEnumerable<string> Files(string root) => new[]
    {
        "src",
        "samples",
        "tests",
        "tools"
    }.SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)).Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")).Where(path => !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)).Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith("tools/ReferenceProbe/", StringComparison.Ordinal)).Where(path => !File.ReadLines(path).Take(3).Any(line => line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal);
    private static CompilationUnitSyntax Parse(string text, CSharpParseOptions configuration) => CSharpSyntaxTree.ParseText(text, configuration).GetCompilationUnitRoot();
    private static IEnumerable<MemberDeclarationSyntax> Types(CompilationUnitSyntax root) => root.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).Where(node => !node.Ancestors().Any(ancestor => ancestor is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax));
    private static string Name(MemberDeclarationSyntax node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax type => type.Identifier.ValueText,
        _ => throw new ArgumentException("Not a type declaration.")};
    private static string Key(MemberDeclarationSyntax node)
    {
        var ns = string.Join(".", node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(value => value.Name.ToString()));
        var arity = node switch
        {
            TypeDeclarationSyntax type => type.TypeParameterList?.Parameters.Count ?? 0,
            DelegateDeclarationSyntax type => type.TypeParameterList?.Parameters.Count ?? 0,
            _ => 0
        };
        return ns + "." + Name(node) + "`" + arity;
    }

    private static string Retain(string text, string? key, bool keepStatements, bool keepAttributes)
    {
        foreach (var configuration in Configurations)
        {
            var root = Parse(text, configuration);
            var remove = Types(root).Where(type => Key(type) != key).Cast<SyntaxNode>().ToList();
            if (!keepStatements)
            {
                remove.AddRange(root.Members.OfType<GlobalStatementSyntax>());
            }

            root = root.RemoveNodes(remove, SyntaxRemoveOptions.KeepDirectives) ?? throw new InvalidOperationException("Missing compilation unit.");
            if (!keepAttributes)
            {
                root = root.WithAttributeLists(default);
            }

            text = root.ToFullString();
        }

        return text.TrimEnd() + "\n";
    }

    private static string[] Fingerprints(string text, CSharpParseOptions configuration) => Types(Parse(text, configuration)).Select(type => Key(type) + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", type.DescendantTokens().Select(token => token.RawKind + ":" + token.ValueText)))))).Order(StringComparer.Ordinal).ToArray();
    private static void UpdateProjectIncludes(string root, Dictionary<string, string[]> moves)
    {
        foreach (var project in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
        {
            var document = XDocument.Load(project, LoadOptions.PreserveWhitespace);
            var changed = false;
            foreach (var entry in document.Descendants().Where(element => element.Name.LocalName == "Compile").ToArray())
            {
                var include = (string? )entry.Attribute("Include");
                if (include == null || include.IndexOfAny(['*', '?', '$', ';']) >= 0)
                {
                    continue;
                }

                var absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                if (!moves.TryGetValue(absolute, out var destinations))
                {
                    continue;
                }

                foreach (var destination in destinations)
                {
                    var clone = new XElement(entry);
                    clone.SetAttributeValue("Include", Path.GetRelativePath(Path.GetDirectoryName(project)!, destination).Replace('\\', '/'));
                    if (clone.Attribute("Link")is { } link && !link.Value.Contains("%("))
                    {
                        var prefix = link.Value.Contains('/') ? link.Value[..(link.Value.LastIndexOf('/') + 1)] : "";
                        link.Value = prefix + Path.GetFileName(destination);
                    }

                    entry.AddBeforeSelf(clone, new XText("\n    "));
                }

                entry.Remove();
                changed = true;
            }

            if (changed)
            {
                var output = document.ToString(SaveOptions.DisableFormatting);
                File.WriteAllText(project, string.Join("\n", output.Split('\n').Select(line => line.TrimEnd())).TrimEnd() + "\n");
            }
        }
    }

    private static SyntaxTokenList Modifiers(this MemberDeclarationSyntax node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Modifiers,
        DelegateDeclarationSyntax type => type.Modifiers,
        _ => default
    };
}
