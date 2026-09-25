using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;

namespace UnoDock.SourceMaintenance;

internal static partial class Program
{
    private static readonly Lazy<AdhocWorkspace> FormattingWorkspace = new(() => new AdhocWorkspace(MefHostServices.Create(MefHostServices.DefaultAssemblies.Append(typeof(CSharpFormattingOptions).Assembly).Distinct())));
    private static int FormatSources(string root, bool verify)
    {
        VerifyFormatter();
        var files = Files(root).ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException("No handwritten source files were selected.");
        }

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var original = File.ReadAllText(file);
            var formatted = FormatText(original);
            foreach (var configuration in Configurations)
            {
                var before = Parse(original, configuration).DescendantTokens().Select(token => (token.RawKind, token.Text));
                var after = Parse(formatted, configuration).DescendantTokens().Select(token => (token.RawKind, token.Text));
                if (!before.SequenceEqual(after))
                {
                    throw new InvalidOperationException($"Formatting changed executable tokens: {file}");
                }
            }

            if (!string.Equals(original, formatted, StringComparison.Ordinal))
            {
                changes.Add(file, formatted);
            }
        }

        if (verify)
        {
            foreach (var path in changes.Keys)
            {
                Console.Error.WriteLine("Formatting required: " + Path.GetRelativePath(root, path));
            }
        }
        else
        {
            // Complete all syntax/configuration checks before writing any file.
            foreach (var (path, text) in changes)
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
        }

        Console.WriteLine($"Source whitespace: {files.Length} explicit files, {Configurations.Length} compile configurations, {changes.Count} {(verify ? "differences" : "formatted files")}.");
        return verify && changes.Count != 0 ? 1 : 0;
    }

    private static string FormatText(string text)
    {
        var workspace = FormattingWorkspace.Value;
        var options = workspace.Options.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false).WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4).WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4).WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\n").WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, false).WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, false);
        for (var pass = 0; pass < 4; pass++)
        {
            var previous = text;
            foreach (var configuration in Configurations)
            {
                // Normalization expands the original compressed layout. The full
                // C# formatter then applies language-aware trivia rules, including
                // pattern/when spacing, nullable arrays and property accessors.
                // Neither stage may alter tokens in any supported configuration.
                var syntax = Parse(text, configuration).NormalizeWhitespace("    ", "\n", false);
                syntax = SeparateAccessors(syntax);
                text = Formatter.Format(syntax, workspace, options).ToFullString().TrimEnd() + "\n";
            }

            if (text == previous)
            {
                return text;
            }
        }

        throw new InvalidOperationException("Conditional source formatting did not reach a stable layout.");
    }

    private static void VerifyFormatter()
    {
        const string original = "namespace FormattingProbe;public class Sentinel{public object?[] Items{get{return [];}}public void Run(object? x){int a=1;int b=2;if((x)is string){return;}try{}catch(System.Exception e)when(e!=null){throw;}}}\n";
        var formatted = FormatText(original);
        if (formatted == original || !formatted.Contains("class Sentinel\n{", StringComparison.Ordinal) || !formatted.Contains("int a = 1;\n", StringComparison.Ordinal) || !formatted.Contains("object?[] Items", StringComparison.Ordinal) || !formatted.Contains("(x) is string", StringComparison.Ordinal) || !formatted.Contains(") when (", StringComparison.Ordinal) || FormatText(formatted) != formatted)
        {
            throw new InvalidOperationException("The formatter failed its expansion, spacing or idempotence sentinel.");
        }
    }
}
