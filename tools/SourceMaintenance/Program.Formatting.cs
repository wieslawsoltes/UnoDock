using System.Text;
using Microsoft.CodeAnalysis;

namespace UnoDock.SourceMaintenance;
internal static partial class Program
{
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
            // All files and compile configurations are validated before writing.
            foreach (var(path, text)in changes)
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
        }

        Console.WriteLine($"Source whitespace: {files.Length} explicit files, {Configurations.Length} compile configurations, {changes.Count} {(verify ? "differences" : "formatted files")}.");
        return verify && changes.Count != 0 ? 1 : 0;
    }

    private static string FormatText(string text)
    {
        // NormalizeWhitespace expands existing one-line blocks and statements;
        // the ordinary whitespace formatter otherwise preserves their layout.
        // Parse every active branch rather than rewriting disabled C# as text.
        for (var pass = 0; pass < 4; pass++)
        {
            var previous = text;
            foreach (var configuration in Configurations)
            {
                text = Parse(text, configuration).NormalizeWhitespace("    ", "\n", false).ToFullString().TrimEnd() + "\n";
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
        const string original = "namespace FormattingProbe;public class Sentinel{public void Run(){int a=1;int b=2;}}\n";
        var formatted = FormatText(original);
        if (formatted == original || !formatted.Contains("class Sentinel\n{", StringComparison.Ordinal) || !formatted.Contains("int a = 1;\n", StringComparison.Ordinal) || FormatText(formatted) != formatted)
        {
            throw new InvalidOperationException("The formatter failed its expansion and idempotence sentinel.");
        }
    }
}
