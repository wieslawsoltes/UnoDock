// Metadata-only public/protected API extraction. No assembly is executed and no IL,
// method body, embedded resource, XAML template or artwork is read or emitted.
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length < 2)
    throw new ArgumentException("Usage: ApiMetadata <assembly.dll> <output-prefix> [--ref-dir <directory>]... [--profile <name>]");
var assemblyPath = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var directories = new List<string> { Path.GetDirectoryName(assemblyPath)! };
var profile = "default";
for (var i = 2; i < args.Length; i++)
{
    if (i + 1 == args.Length) throw new ArgumentException("Missing option value: " + args[i]);
    switch (args[i++])
    {
        case "--ref-dir": directories.Add(Path.GetFullPath(args[i])); break;
        case "--profile": profile = args[i]; break;
        default: throw new ArgumentException("Unknown option: " + args[i - 1]);
    }
}
var paths = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var directory in directories)
    foreach (var path in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.Ordinal))
        paths.TryAdd(Path.GetFileName(path), path);
paths[Path.GetFileName(assemblyPath)] = assemblyPath;
var references = new List<MetadataReference>();
var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var (name, path) in paths)
{
    try
    {
        var reference = MetadataReference.CreateFromFile(path, MetadataReferenceProperties.Assembly);
        // Validate the PE eagerly so native DLLs do not poison symbol resolution.
        if (reference.GetMetadata() is not AssemblyMetadata) continue;
        references.Add(reference);
        hashes[name] = Hash(File.ReadAllBytes(path));
    }
    catch (BadImageFormatException) { /* Native dependency; not part of a managed API. */ }
}
var compilation = CSharpCompilation.Create("UnoDock.ApiMetadata", references: references,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
        metadataImportOptions: MetadataImportOptions.All));
var targetReference = references.OfType<PortableExecutableReference>().Single(r => r.FilePath == assemblyPath);
var assembly = compilation.GetAssemblyOrModuleSymbol(targetReference) as IAssemblySymbol
    ?? throw new InvalidDataException("Input is not a managed assembly.");
var unresolved = new SortedSet<string>(StringComparer.Ordinal);
var types = Enumerate(assembly.GlobalNamespace).Where(VisibleType).OrderBy(MetadataName, StringComparer.Ordinal).ToArray();
var records = new List<object>();
var declarations = new SortedSet<string>(StringComparer.Ordinal);
foreach (var type in types)
{
    CheckType(type.BaseType); foreach (var contract in type.Interfaces) CheckType(contract);
    var members = type.GetMembers().Where(VisibleMember).OrderBy(MemberKey, StringComparer.Ordinal).ToArray();
    var baseClasses = new List<string>();
    for (var parent = type.BaseType; parent != null; parent = parent.BaseType) { CheckType(parent); baseClasses.Add(TypeName(parent)); }
    var typeLine = $"{MetadataName(type)} | {Access(type)} {TypeFlags(type)} {type.TypeKind.ToString().ToLowerInvariant()} : {TypeName(type.BaseType)} interfaces[{string.Join(",", type.Interfaces.Select(TypeName).Order(StringComparer.Ordinal))}] {TypeParameters(type.TypeParameters)}";
    declarations.Add(typeLine.Trim());
    var memberRecords = new List<object>();
    foreach (var member in members)
    {
        var line = MemberKey(member);
        declarations.Add(MetadataName(type) + " | " + line);
        memberRecords.Add(new
        {
            key = line,
            kind = member.Kind.ToString(),
            metadataName = member.MetadataName,
            documentationId = member.GetDocumentationCommentId(),
            attributes = Attributes(member.GetAttributes()),
            returnAttributes = member is IMethodSymbol method ? Attributes(method.GetReturnTypeAttributes()) : [],
            parameters = member switch
            {
                IMethodSymbol m => ParameterRecords(m.Parameters),
                IPropertySymbol p => ParameterRecords(p.Parameters),
                _ => Array.Empty<object>()
            }
        });
    }
    records.Add(new
    {
        name = MetadataName(type),
        key = typeLine.Trim(),
        assembly = type.ContainingAssembly.Identity.Name,
        attributes = Attributes(type.GetAttributes()),
        baseClasses,
        interfaces = type.AllInterfaces.Select(TypeName).Order(StringComparer.Ordinal).ToArray(),
        members = memberRecords
    });
}
// A resolved signature is essential: matching unresolved names would give false evidence.
if (unresolved.Count != 0) throw new InvalidDataException("Unresolved API types: " + string.Join(", ", unresolved));
var canonical = string.Join("\n", declarations) + "\n";
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output + ".txt", canonical, new UTF8Encoding(false));
File.WriteAllText(output + ".json", JsonSerializer.Serialize(new
{
    schema = 2,
    scanner = "UnoDock.ApiMetadata/v1",
    method = "Roslyn symbols imported from PE metadata; no IL bodies, resources or assembly execution",
    profile,
    assembly = assembly.Identity.ToString(),
    sha256 = Hash(Encoding.UTF8.GetBytes(canonical)),
    inputHashes = hashes,
    typeCount = types.Length,
    count = declarations.Count,
    forwardedTypes = assembly.GetForwardedTypes().Select(TypeName).Order(StringComparer.Ordinal).ToArray(),
    declarations,
    types = records
}, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
Console.WriteLine($"Metadata: {types.Length} exported types; {declarations.Count} declared API entries; SHA256 {Hash(Encoding.UTF8.GetBytes(canonical))}");

string MemberKey(ISymbol member)
{
    var prefix = Access(member) + " " + Flags(member);
    return (member switch
    {
        IFieldSymbol f => $"{prefix} field {TypeNameChecked(f.Type)} {f.MetadataName}{(f.IsReadOnly ? " readonly" : "")}{(f.IsConst ? " const=" + Constant(f.ConstantValue) : "")}",
        IEventSymbol e => $"{prefix} event {TypeNameChecked(e.Type)} {e.MetadataName}",
        IPropertySymbol p => $"{prefix} property {Ref(p.RefKind)}{TypeNameChecked(p.Type)} {p.MetadataName}{Parameters(p.Parameters)} {{ {Accessor(p.GetMethod, "get")}{Accessor(p.SetMethod, p.SetMethod?.IsInitOnly == true ? "init" : "set")} }}",
        IMethodSymbol m => $"{prefix} {(m.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor ? "constructor" : "method")} {Ref(m.RefKind)}{TypeNameChecked(m.ReturnType)} {m.MetadataName}{TypeParameters(m.TypeParameters)}{Parameters(m.Parameters)}",
        _ => throw new NotSupportedException(member.Kind.ToString())
    }).Trim();
}
string TypeNameChecked(ITypeSymbol type) { CheckType(type); return TypeName(type); }
void CheckType(ITypeSymbol? type)
{
    switch (type)
    {
        case null: return;
        case IErrorTypeSymbol error: unresolved.Add(error.ToDisplayString()); return;
        case IArrayTypeSymbol array: CheckType(array.ElementType); break;
        case IPointerTypeSymbol pointer: CheckType(pointer.PointedAtType); break;
        case INamedTypeSymbol named:
            foreach (var argument in named.TypeArguments) CheckType(argument);
            if (named.ContainingType != null) CheckType(named.ContainingType);
            break;
        case IFunctionPointerTypeSymbol function:
            CheckType(function.Signature.ReturnType); foreach (var p in function.Signature.Parameters) CheckType(p.Type);
            break;
    }
}
string TypeParameters(ImmutableArray<ITypeParameterSymbol> parameters) => parameters.Length == 0 ? "" : "<" + string.Join(",", parameters.Select(p =>
{
    foreach (var constraint in p.ConstraintTypes) CheckType(constraint);
    var constraints = new List<string>();
    if (p.HasReferenceTypeConstraint) constraints.Add("class");
    if (p.HasValueTypeConstraint) constraints.Add("struct");
    if (p.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
    if (p.HasNotNullConstraint) constraints.Add("notnull");
    constraints.AddRange(p.ConstraintTypes.Select(TypeName).Order(StringComparer.Ordinal));
    if (p.HasConstructorConstraint) constraints.Add("new()");
    return (p.Variance == VarianceKind.None ? "" : p.Variance.ToString().ToLowerInvariant() + " ") + p.Name + (constraints.Count == 0 ? "" : ":" + string.Join("&", constraints));
})) + ">";
string Parameters(ImmutableArray<IParameterSymbol> parameters) => "(" + string.Join(",", parameters.Select(p =>
    (p.IsParams ? "params " : "") + Ref(p.RefKind) + TypeNameChecked(p.Type) + " " + p.Name +
    (p.HasExplicitDefaultValue ? "=" + Constant(p.ExplicitDefaultValue) : p.IsOptional ? "=optional" : ""))) + ")";
object[] ParameterRecords(ImmutableArray<IParameterSymbol> parameters) => parameters.Select(p => (object)new
{
    p.Name, type = TypeNameChecked(p.Type), refKind = p.RefKind.ToString(), p.IsParams, p.IsOptional,
    p.HasExplicitDefaultValue, defaultValue = p.HasExplicitDefaultValue ? Constant(p.ExplicitDefaultValue) : null,
    attributes = Attributes(p.GetAttributes())
}).ToArray();
static string Accessor(IMethodSymbol? method, string name) => method != null && Visible(method.DeclaredAccessibility) ? Access(method) + " " + name + "; " : "";
static string Ref(RefKind kind) => kind switch { RefKind.None => "", RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ", _ => "ref readonly " };
static string Access(ISymbol symbol) => symbol.DeclaredAccessibility switch
{ Accessibility.Public => "public", Accessibility.Protected => "protected", Accessibility.ProtectedOrInternal => "protected internal", _ => symbol.DeclaredAccessibility.ToString() };
static string Flags(ISymbol symbol) => string.Join(" ", new[]
{ symbol.IsStatic ? "static" : "", symbol.IsAbstract ? "abstract" : "", symbol.IsVirtual ? "virtual" : "", symbol.IsOverride ? "override" : "", symbol.IsSealed ? "sealed" : "" }.Where(s => s.Length > 0));
static string TypeFlags(INamedTypeSymbol type) => Flags(type) + (type.IsReadOnly ? " readonly" : "") + (type.IsRefLikeType ? " ref" : "");
static bool Visible(Accessibility accessibility) => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
static bool VisibleType(INamedTypeSymbol type) => Visible(type.DeclaredAccessibility) && (type.ContainingType == null || VisibleType(type.ContainingType));
static bool VisibleMember(ISymbol symbol) => Visible(symbol.DeclaredAccessibility) && symbol is not INamedTypeSymbol &&
    (symbol is not IMethodSymbol method || method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion or MethodKind.Destructor);
static IEnumerable<INamedTypeSymbol> Enumerate(INamespaceSymbol ns)
{
    foreach (var child in ns.GetNamespaceMembers()) foreach (var type in Enumerate(child)) yield return type;
    foreach (var type in ns.GetTypeMembers()) foreach (var nested in EnumerateType(type)) yield return nested;
}
static IEnumerable<INamedTypeSymbol> EnumerateType(INamedTypeSymbol type)
{ yield return type; foreach (var child in type.GetTypeMembers()) foreach (var nested in EnumerateType(child)) yield return nested; }
static string MetadataName(INamedTypeSymbol type) => type.ContainingType != null ? MetadataName(type.ContainingType) + "+" + type.MetadataName :
    (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + type.MetadataName;
static string TypeName(ITypeSymbol? type) => type?.ToDisplayString(new SymbolDisplayFormat(
    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)) ?? "";
static string Constant(object? value) => value switch
{
    null => "null",
    string text => JsonSerializer.Serialize(text),
    char c => "char:" + ((int)c).ToString(CultureInfo.InvariantCulture),
    bool b => b ? "true" : "false",
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? "null"
};
static string Typed(TypedConstant value) => value.Kind switch
{
    TypedConstantKind.Array => value.IsNull ? "null" : "[" + string.Join(",", value.Values.Select(Typed)) + "]",
    TypedConstantKind.Type => "typeof(" + TypeName(value.Value as ITypeSymbol) + ")",
    TypedConstantKind.Enum => TypeName(value.Type) + ":" + Constant(value.Value),
    _ => Constant(value.Value)
};
static string[] Attributes(ImmutableArray<AttributeData> attributes) => attributes.Select(a =>
    TypeName(a.AttributeClass) + "(" + string.Join(",", a.ConstructorArguments.Select(Typed)) + ")" +
    "{" + string.Join(",", a.NamedArguments.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + Typed(p.Value))) + "}")
    .Order(StringComparer.Ordinal).ToArray();
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
