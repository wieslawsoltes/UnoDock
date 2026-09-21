// Independent declaration-only inventory. Never emits bodies, comments, attributes,
// property initializers, non-constant field initializers, or upstream resources.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 2) throw new ArgumentException("Usage: ApiScan <source-directory> <output-prefix> [preprocessor-symbols-comma-separated]");
var root = Path.GetFullPath(args[0]);
var prefix = Path.GetFullPath(args[1]);
var symbols = args.Length > 2 ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
var entries = new SortedSet<string>(StringComparer.Ordinal);
var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
    if (relative.Split('/').Any(p => p is "obj" or "bin")) continue;
    var text = File.ReadAllText(path);
    files[relative] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: symbols));
    if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        throw new InvalidDataException("Parse failed: " + relative);
    foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
    {
        if (!Visible(node)) continue;
        var owner = string.Join(".", node.Ancestors().Reverse().Select(n => n switch
        {
            BaseNamespaceDeclarationSyntax ns => ns.Name.ToString(),
            TypeDeclarationSyntax t => t.Identifier.Text + Flat(t.TypeParameterList),
            EnumDeclarationSyntax e => e.Identifier.Text,
            _ => null
        }).Where(n => n != null));
        var head = Mods(node);
        string? declaration = node switch
        {
            TypeDeclarationSyntax t => $"{head} {t.Keyword.Text} {t.Identifier.Text}{Flat(t.TypeParameterList)}{Flat(t.BaseList)} {string.Join(" ", t.ConstraintClauses.Select(Flat))}",
            EnumDeclarationSyntax e => $"{head} enum {e.Identifier.Text}{Flat(e.BaseList)}",
            EnumMemberDeclarationSyntax e => $"enum-value {e.Identifier.Text}{Flat(e.EqualsValue)}",
            DelegateDeclarationSyntax d => $"{head} delegate {Flat(d.ReturnType)} {d.Identifier.Text}{Flat(d.TypeParameterList)}{Flat(d.ParameterList)} {string.Join(" ", d.ConstraintClauses.Select(Flat))}",
            ConstructorDeclarationSyntax c => $"{head} {c.Identifier.Text}{Flat(c.ParameterList)}",
            MethodDeclarationSyntax m => $"{head} {Flat(m.ReturnType)} {Flat(m.ExplicitInterfaceSpecifier)}{m.Identifier.Text}{Flat(m.TypeParameterList)}{Flat(m.ParameterList)} {string.Join(" ", m.ConstraintClauses.Select(Flat))}",
            PropertyDeclarationSyntax p => $"{head} {Flat(p.Type)} {Flat(p.ExplicitInterfaceSpecifier)}{p.Identifier.Text} {{ {Accessors(p.AccessorList, p.ExpressionBody != null)} }}",
            IndexerDeclarationSyntax i => $"{head} {Flat(i.Type)} this{Flat(i.ParameterList)} {{ {Accessors(i.AccessorList, i.ExpressionBody != null)} }}",
            EventDeclarationSyntax e => $"{head} event {Flat(e.Type)} {e.Identifier.Text}",
            OperatorDeclarationSyntax o => $"{head} {Flat(o.ReturnType)} operator {o.OperatorToken.Text}{Flat(o.ParameterList)}",
            ConversionOperatorDeclarationSyntax o => $"{head} {o.ImplicitOrExplicitKeyword.Text} operator {Flat(o.Type)}{Flat(o.ParameterList)}",
            _ => null
        };
        if (node is BaseFieldDeclarationSyntax f)
            foreach (var variable in f.Declaration.Variables)
                entries.Add($"{owner} | {head} {(node is EventFieldDeclarationSyntax ? "event " : "")}{Flat(f.Declaration.Type)} {variable.Identifier.Text}{(f.Modifiers.Any(SyntaxKind.ConstKeyword) ? Flat(variable.Initializer) : "")}".Trim());
        else if (declaration != null) entries.Add(owner + " | " + declaration.Trim());
    }
}
Directory.CreateDirectory(Path.GetDirectoryName(prefix)!);
var canonical = string.Join("\n", entries) + "\n";
File.WriteAllText(prefix + ".txt", canonical, new UTF8Encoding(false));
File.WriteAllText(prefix + ".json", JsonSerializer.Serialize(new
{
    schema = 1,
    scanner = "UnoDock.ApiScan/declaration-only-v1",
    visibility = "public and protected declarations, including externally-visible enclosing types",
    profile = symbols,
    sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
    count = entries.Count,
    files,
    declarations = entries
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Scanned {files.Count} files; {entries.Count} declarations; SHA256 {Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}");

static string Flat(SyntaxNode? node) => node?.WithoutTrivia().NormalizeWhitespace().ToFullString() ?? "";
static string Mods(MemberDeclarationSyntax node) => string.Join(" ", node.Modifiers.Where(t => !t.IsKind(SyntaxKind.PartialKeyword)).Select(t => t.Text));
static bool Visible(MemberDeclarationSyntax node)
{
    if (node is BaseNamespaceDeclarationSyntax) return false;
    if (node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any(t => !t.Modifiers.Any(SyntaxKind.PublicKeyword) && !t.Modifiers.Any(SyntaxKind.ProtectedKeyword))) return false;
    return node is EnumMemberDeclarationSyntax || node.Parent is InterfaceDeclarationSyntax || node.Modifiers.Any(SyntaxKind.PublicKeyword) || (node.Modifiers.Any(SyntaxKind.ProtectedKeyword) && !node.Modifiers.Any(SyntaxKind.PrivateKeyword));
}
static string Accessors(AccessorListSyntax? list, bool expression)
{
    if (expression) return "get;";
    if (list == null) return "";
    return string.Join(" ", list.Accessors.Select(a => (string.Join(" ", a.Modifiers.Select(t => t.Text)) + " " + a.Keyword.Text + ";").Trim()));
}
