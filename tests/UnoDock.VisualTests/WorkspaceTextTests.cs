using System.Text;
using UnoDock.Gallery;

namespace UnoDock.Testing;
internal static class WorkspaceTextTests
{
    internal static void Register(TestRunner tests)
    {
        var fixtures = new (string Name, string Text, string Editor, int Lines)[]
        {
            ("empty", "", "", 1),
            ("single", "plain text", "plain text", 1),
            ("LF", "first\nsecond\n", "first\rsecond\r", 3),
            ("CRLF", "first\r\nsecond\r\n", "first\rsecond\r", 3),
            ("CR", "first\rsecond\r", "first\rsecond\r", 3),
            ("mixed", "a\r\nb\nc\rd\r\n", "a\rb\rc\rd\r", 5),
            ("adjacent", "\r\n\n\r\r\n", "\r\r\r\r", 5),
            ("Unicode", "\U0001F642 e\u0301\r\n\u03A9\n\u6F22\r", "\U0001F642 e\u0301\r\u03A9\r\u6F22\r", 4),
            ("separators are not CRLF", "a\u2028b\u2029c", "a\u2028b\u2029c", 1)
        };
        foreach (var fixture in fixtures)
        {
            tests.Test("text projection: exact native representation and line count: " + fixture.Name, () =>
            {
                Check.Equal(fixture.Editor, WorkspaceTextProjection.ForEditor(fixture.Text));
                Check.Equal(fixture.Lines, WorkspaceTextProjection.CountLines(fixture.Text));
            });
            tests.Test("text projection: native no-op keeps exact buffer identity: " + fixture.Name, () =>
            {
                var original = fixture.Text;
                Check.Same(original, WorkspaceTextProjection.ApplyEditorEdit(original, fixture.Editor));
                Check.Same(original, WorkspaceTextProjection.ApplyEditorEdit(original, fixture.Editor.Replace("\r", "\r\n", StringComparison.Ordinal)));
            });
            tests.Test("text projection: model no-op has no dirty/title/command side effects: " + fixture.Name, () =>
            {
                var document = Document(fixture.Text);
                var notifications = 0;
                document.PropertyChanged += (_, _) => notifications++;
                document.SaveCommand.CanExecuteChanged += (_, _) => notifications++;
                var projection = document.EditorText;
                document.EditorText = fixture.Editor;
                Check.Equal(0, notifications);
                Check.False(document.IsDirty);
                Check.Same(fixture.Text, document.Text);
                Check.Same(projection, document.EditorText);
                Check.Equal(fixture.Text.Length, document.CharacterCount);
                Check.Equal(fixture.Lines, document.LineCount);
            });
        }

        foreach (var delimiter in new[]
        {
            "\n",
            "\r\n",
            "\r"
        }

        )
        {
            var label = delimiter.Replace("\r", "CR", StringComparison.Ordinal).Replace("\n", "LF", StringComparison.Ordinal);
            tests.Test("text projection: inserted lines use first original delimiter: " + label, () =>
            {
                var original = "first" + delimiter + "last";
                Check.Equal("first" + delimiter + "new" + delimiter + "last", WorkspaceTextProjection.ApplyEditorEdit(original, "first\rnew\rlast"));
            });
            tests.Test("text projection: trailing line removal preserves preceding delimiter: " + label, () =>
            {
                var original = "first" + delimiter + "last" + delimiter;
                Check.Equal("first" + delimiter + "last", WorkspaceTextProjection.ApplyEditorEdit(original, "first\rlast"));
            });
        }

        tests.Test("text projection: mixed unchanged prefix and suffix remain verbatim", () =>
        {
            const string original = "a\r\nb\nc\rd\r\n";
            Check.Equal("a\r\nb+\nc\rd\r\n", WorkspaceTextProjection.ApplyEditorEdit(original, "a\rb+\rc\rd\r"));
            Check.Equal("a\r\nb\nc\rnew\r\nd\r\n", WorkspaceTextProjection.ApplyEditorEdit(original, "a\rb\rc\rnew\rd\r"));
        });
        tests.Test("text projection: wholly replaced content uses documented delimiter policy", () =>
        {
            Check.Equal("x\r\ny", WorkspaceTextProjection.ApplyEditorEdit("a\r\nb", "x\ny"));
            Check.Equal("x\ny", WorkspaceTextProjection.ApplyEditorEdit("no original line breaks", "x\ry"));
            Check.Equal("", WorkspaceTextProjection.ApplyEditorEdit("a\r\nb\n", ""));
        });
        tests.Test("text projection: UTF16 edits preserve surrounding surrogate pairs and accents", () =>
        {
            const string original = "\U0001F642\r\n\u03A9 e\u0301\n\U0001F680";
            Check.Equal("\U0001F642\r\n\u03A9 e\u0301!\n\U0001F680", WorkspaceTextProjection.ApplyEditorEdit(original, "\U0001F642\r\u03A9 e\u0301!\r\U0001F680"));
        });
        tests.Test("text projection: save baseline and revert retain exact mixed text", () =>
        {
            var document = Document("a\r\nb\nc\r");
            document.EditorText = "a\rb+\rc\r";
            var saved = document.Text;
            Check.Equal("a\r\nb+\nc\r", saved);
            Check.True(document.IsDirty);
            document.AcceptSaved(saved);
            Check.False(document.IsDirty);
            document.EditorText += "later";
            Check.True(document.IsDirty);
            document.Revert();
            Check.Same(saved, document.Text);
            Check.False(document.IsDirty);
        });
        tests.Test("text projection: cache invalidates before model observers run", () =>
        {
            var document = Document("old\ntext");
            _ = document.EditorText;
            string? observed = null;
            document.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkspaceDocument.Text))
                    observed = document.EditorText;
            };
            document.Text = "new\r\ntext";
            Check.Equal("new\rtext", observed);
            Check.Equal("new\rtext", document.EditorText);
        });
        tests.Test("text projection: deterministic 10000 edit sequences preserve visible content and no-op identity", () =>
        {
            var random = new Random(160924);
            var alphabet = new[]
            {
                "a",
                "b",
                " ",
                "\t",
                "\u03A9",
                "\U0001F642",
                "\r",
                "\n",
                "\r\n"
            };
            for (var iteration = 0; iteration < 10000; iteration++)
            {
                var builder = new StringBuilder();
                for (var i = 0; i < random.Next(1, 70); i++)
                    builder.Append(alphabet[random.Next(alphabet.Length)]);
                var original = builder.ToString();
                var before = Canonical(original);
                var start = random.Next(before.Length + 1);
                var end = random.Next(start, before.Length + 1);
                var edited = before[..start] + alphabet[random.Next(alphabet.Length)] + before[end..];
                var expected = Canonical(edited);
                var result = WorkspaceTextProjection.ApplyEditorEdit(original, edited);
                Check.Equal(expected, Canonical(result));
                Check.Same(result, WorkspaceTextProjection.ApplyEditorEdit(result, expected));
                Check.Equal(expected.Count(c => c == '\r') + 1, WorkspaceTextProjection.CountLines(result));
                if (before == expected)
                    Check.Same(original, result);
            }
        });
        tests.Test("text projection: null inputs rejected at all public sample boundaries", () =>
        {
            Check.Throws<ArgumentNullException>(() => WorkspaceTextProjection.ForEditor(null!));
            Check.Throws<ArgumentNullException>(() => WorkspaceTextProjection.ApplyEditorEdit(null!, ""));
            Check.Throws<ArgumentNullException>(() => WorkspaceTextProjection.ApplyEditorEdit("", null!));
            Check.Throws<ArgumentNullException>(() => WorkspaceTextProjection.CountLines(null!));
        });
    }

    private static WorkspaceDocument Document(string text) => new("projection-test", "Probe.txt", text, () =>
    {
    }, () =>
    {
    }, () =>
    {
    });
    // Independent small transducer for invariant expectations: it neither invokes
    // production projection nor rewrites CRLF with the production implementation.
    private static string Canonical(string input)
    {
        var result = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '\r')
            {
                result.Append('\r');
                if (i + 1 < input.Length && input[i + 1] == '\n')
                    i++;
            }
            else
                result.Append(c == '\n' ? '\r' : c);
        }

        return result.ToString();
    }
}
