using System.Text;

namespace UnoDock.Gallery;
/// <summary>Separates a TextBox's CR-based editing view from exact application text.
/// Unedited text is retained verbatim. A changed contiguous region uses the first
/// existing line delimiter, while unchanged prefix/suffix spans retain mixed endings.
/// A CR is inserted at a splice that would otherwise join two logical breaks into
/// one CRLF. This is a UTF16 text projection, not a byte encoding or arbitrary diff.</summary>
internal static class WorkspaceTextProjection
{
    internal static string ForEditor(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
    }

    internal static string ApplyEditorEdit(string original, string edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);
        var before = ForEditor(original);
        var after = ForEditor(edited);
        if (string.Equals(before, after, StringComparison.Ordinal))
            return original;
        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix && before[before.Length - suffix - 1] == after[after.Length - suffix - 1])
            suffix++;
        var left = OriginalOffset(original, prefix);
        var right = OriginalOffset(original, before.Length - suffix);
        var inserted = after.Substring(prefix, after.Length - prefix - suffix).Replace("\r", PreferredDelimiter(original), StringComparison.Ordinal);
        // Projection is not homomorphic at CR|LF concatenation boundaries. Those
        // characters came from distinct logical breaks, not a single CRLF token.
        // Preserve both untouched spans and disambiguate only the affected seam.
        var leadingSeam = left > 0 && original[left - 1] == '\r' && inserted.Length > 0 && inserted[0] == '\n';
        var trailingSeam = right < original.Length && original[right] == '\n' && (inserted.Length > 0 ? inserted[^1] == '\r' : left > 0 && original[left - 1] == '\r');
        if (!leadingSeam && !trailingSeam)
            return string.Concat(original.AsSpan(0, left), inserted.AsSpan(), original.AsSpan(right));
        var result = new StringBuilder();
        result.Append(original, 0, left);
        if (leadingSeam)
            result.Append('\r');
        result.Append(inserted);
        if (trailingSeam)
            result.Append('\r');
        result.Append(original, right, original.Length - right);
        return result.ToString();
    }

    internal static int CountLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                count++;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
            else if (text[i] == '\n')
                count++;
        }

        return count;
    }

    private static int OriginalOffset(string text, int projectedOffset)
    {
        var index = 0;
        while (projectedOffset-- > 0)
        {
            if (text[index++] == '\r' && index < text.Length && text[index] == '\n')
                index++;
        }

        return index;
    }

    private static string PreferredDelimiter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
                return i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";
            if (text[i] == '\n')
                return "\n";
        }

        return "\n";
    }
}
