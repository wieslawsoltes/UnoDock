using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnoDock.SourceMaintenance;

internal static partial class Program
{
    private static CompilationUnitSyntax SeparateAccessors(CompilationUnitSyntax syntax)
    {
        // Roslyn expands the containing property but keeps consecutive expression
        // accessors on one line. Separate their trivia without rewriting bodies,
        // accessibility, attributes, comments, literals or any executable token.
        var endings = syntax.DescendantNodes().OfType<AccessorListSyntax>()
            .SelectMany(list => list.Accessors.Take(Math.Max(0, list.Accessors.Count - 1)))
            .Select(accessor => accessor.GetLastToken()).ToArray();
        return syntax.ReplaceTokens(endings, (_, token) =>
        {
            if (token.TrailingTrivia.Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                return token;
            }

            return token.WithTrailingTrivia(token.TrailingTrivia.Add(SyntaxFactory.LineFeed));
        });
    }
}
