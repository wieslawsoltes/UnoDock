using System.Globalization;

namespace Xceed.Wpf.AvalonDock.Layout;

internal static class LayoutDiagnostics
{
    internal static void Write(ILayoutElement root, TextWriter writer, int indentation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfNegative(indentation);
        var pending = new Stack<(ILayoutElement Element, int Depth)>();
        var seen = new HashSet<ILayoutElement>(ReferenceEqualityComparer.Instance);
        pending.Push((root, indentation));
        while (pending.TryPop(out var entry))
        {
            writer.Write(new string(' ', Math.Min(entry.Depth, 128) * 2));
            writer.Write(entry.Element.GetType().Name);
            if (!seen.Add(entry.Element)) { writer.WriteLine(" [already visited]"); continue; }
            if (entry.Element is LayoutContent content)
            {
                writer.Write(" Id=\""); writer.Write(Escape(content.ContentId));
                writer.Write("\" Title=\""); writer.Write(Escape(content.Title)); writer.Write('"');
                if (content.IsSelected) writer.Write(" Selected");
                if (content.IsActive) writer.Write(" Active");
                if (content.IsFloating) writer.Write(" Floating");
                if (content is LayoutAnchorable { IsHidden: true }) writer.Write(" Hidden");
                if (content is LayoutAnchorable { IsAutoHidden: true }) writer.Write(" AutoHidden");
            }
            if (entry.Element is ILayoutOrientableGroup oriented)
            { writer.Write(" Orientation="); writer.Write(oriented.Orientation); }
            if (entry.Element is ILayoutContainer container)
            {
                // Snapshot the child sequence; traversal never evaluates user Content.
                var children = container.Children.ToArray();
                writer.Write(" Children="); writer.Write(children.Length.ToString(CultureInfo.InvariantCulture));
                var depth = Math.Min(entry.Depth, 128) + 1;
                for (var index = children.Length - 1; index >= 0; index--) pending.Push((children[index], depth));
            }
            writer.WriteLine();
        }
    }
    private static string Escape(string? text) => text?.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal) ?? "";
}
