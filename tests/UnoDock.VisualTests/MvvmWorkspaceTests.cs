using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Themes;
using UnoDock.VisualValidation;
using System.Xml.Linq;

namespace UnoDock.Testing;

internal static class MvvmWorkspaceTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        WorkspaceTextTests.Register(tests);
        using var dock = new DockingManager
        {
            Width = 1000,
            Height = 640,
            FontSize = 12,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var window = new Window
        {
            Content = dock,
            Title = "UnoDock MVVM acceptance"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1100,
            Height = 800
        });
        window.Activate();
        try
        {
            await Wait(() => dock.IsLoaded && dock.ActualWidth > 0);
            Add("MVVM: source-backed documents and tools are not UI-element models", (workspace, _) =>
            {
                Check.Equal(2, workspace.Documents.Count);
                Check.Equal(3, workspace.Files.Count);
                Check.Same(workspace.Documents, dock.DocumentsSource);
                Check.Equal(2, Models().Length);
                Check.Equal(3, dock.Layout.Descendents().OfType<LayoutAnchorable>().Count());
                Check.True(Models().All(model => model.Content is WorkspaceDocument));
                Check.True(dock.Layout.Descendents().OfType<LayoutAnchorable>().All(model => model.Content is WorkspaceTool));
                return Task.CompletedTask;
            });
            Add("MVVM: compiled template realizes the actual editable view", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                var initial = document.Text;
                var editor = Editor(document);
                // The view is CR-based; application text must retain its LF bytes.
                // Assert both contracts, not equality between different projections.
                Check.Equal(document.EditorText, editor.Editor.Text);
                Check.Same(initial, document.Text);
                Check.False(document.IsDirty);
                editor.Editor.Text = "native dependency-property edit\nsecond line";
                await Wait(() => document.Text == "native dependency-property edit\nsecond line");
                Check.Equal("native dependency-property edit\rsecond line", editor.Editor.Text);
                Check.True(document.IsDirty);
                Check.Equal(2, document.LineCount);
            });
            foreach (var delimiter in new[]
            {
                "\n",
                "\r\n",
                "\r"
            }

            )
            {
                var label = delimiter.Replace("\r", "CR", StringComparison.Ordinal).Replace("\n", "LF", StringComparison.Ordinal);
                Add("MVVM: native edit, save and revert preserve " + label, async (workspace, store) =>
                {
                    var document = workspace.Documents[0];
                    var editor = Editor(document);
                    var original = "alpha" + delimiter + "beta" + delimiter;
                    document.Text = original;
                    document.AcceptSaved(original);
                    await Settle();
                    Check.Equal("alpha\rbeta\r", editor.Editor.Text);
                    Check.Same(original, document.Text);
                    Check.False(document.IsDirty);
                    editor.Editor.Text = "alpha\rbeta+\r";
                    var expected = "alpha" + delimiter + "beta+" + delimiter;
                    await Wait(() => document.Text == expected);
                    Check.True(await workspace.SaveAsync(document));
                    Check.Equal(expected, store.Writes.Single().Text);
                    Check.False(document.IsDirty);
                    editor.Editor.Text = "temporary";
                    await Wait(() => document.Text == "temporary");
                    workspace.RevertDocument(document);
                    await Settle();
                    Check.Equal(expected, document.Text);
                    Check.Equal("alpha\rbeta+\r", editor.Editor.Text);
                    Check.False(document.IsDirty);
                });
            }

            Add("MVVM: mixed endings survive realization, tab switches and XML restore", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                const string original = "alpha\r\nbeta\ngamma\rdelta\r\n";
                document.Text = original;
                document.AcceptSaved(original);
                await Settle();
                Check.Equal("alpha\rbeta\rgamma\rdelta\r", Editor(document).Editor.Text);
                workspace.OpenDocument(workspace.Documents[1]);
                await Settle();
                workspace.OpenDocument(document);
                await Settle();
                workspace.RestoreLayout(workspace.CaptureLayout());
                await Settle();
                Check.Same(original, document.Text);
                Check.False(document.IsDirty);
                Check.Equal("alpha\rbeta\rgamma\rdelta\r", Editor(document).Editor.Text);
            });
            foreach (var seam in new[]
            {
                (Name: "leading", Original: "a\nb\rc", Edited: "a\rb\r\rc", Expected: "a\nb\r\r\nc"),
                (Name: "trailing", Original: "a\rb\nc", Edited: "a\rx\r\rc", Expected: "a\rx\r\r\nc"),
                (Name: "deletion", Original: "a\nb\rcX\nd", Edited: "a\rb\r\rd", Expected: "a\nb\r\r\nd")
            }

            )
                Add("MVVM: mixed delimiter splice keeps both logical lines: " + seam.Name, async (workspace, store) =>
                {
                    var document = workspace.Documents[0];
                    document.Text = seam.Original;
                    document.AcceptSaved(seam.Original);
                    await Settle();
                    Editor(document).Editor.Text = seam.Edited;
                    await Wait(() => document.Text == seam.Expected);
                    Check.Equal(seam.Edited, document.EditorText);
                    Check.Equal(seam.Edited, Editor(document).Editor.Text);
                    Check.True(await workspace.SaveAsync(document));
                    Check.Equal(seam.Expected, store.Writes.Single().Text);
                });
            Add("MVVM: dirty title reaches model adapter and rendered tab", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                document.Text += "new edit";
                await Wait(() => Model(document).Title == document.Title);
                await Settle();
                Check.Equal(document.Title, dock.GetLayoutItemFromModel(Model(document)).Title);
                var tab = dock.FindVisualChildren<LayoutDocumentTabItem>().Single(t => ReferenceEquals(t.Model, Model(document)));
                Check.True(tab.FindVisualChildren<TextBlock>().Any(t => t.Text == document.Title), "Dirty indicator was not rendered in the real tab.");
            });
            Add("MVVM: active document follows docking activation and survives tool focus", async (workspace, _) =>
            {
                var document = workspace.Documents[1];
                Model(document).IsActive = true;
                await Settle();
                Check.Same(document, workspace.ActiveDocument);
                dock.Layout.Descendents().OfType<LayoutAnchorable>().First().IsActive = true;
                await Settle();
                Check.Same(document, workspace.ActiveDocument);
            });
            Add("MVVM: tab switching retains the actual editor and draft", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                var editor = Editor(document);
                editor.Editor.Text = "retained draft";
                await Wait(() => document.Text == "retained draft");
                workspace.OpenDocument(workspace.Documents[1]);
                await Settle();
                workspace.OpenDocument(document);
                await Settle();
                Check.Same(editor, Editor(document));
                Check.Equal("retained draft", editor.Editor.Text);
            });
            Add("MVVM: float then dock retains template editor and buffer", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                var model = Model(document);
                var editor = Editor(document);
                document.Text = "floating buffer";
                model.Float();
                await Settle();
                Check.True(model.IsFloating);
                model.Dock();
                await Settle();
                Check.Same(editor, Editor(document));
                Check.Equal("floating buffer", document.Text);
            });
            Add("MVVM: dirty close is cancelled through the real docking path", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                document.Text += "dirty";
                dock.GetLayoutItemFromModel(Model(document)).CloseCommand!.Execute(null);
                await Settle();
                Check.True(document.IsOpen);
                Check.Equal(2, workspace.Documents.Count);
                Check.True(workspace.Status.Contains("Unsaved", StringComparison.Ordinal));
            });
            Add("MVVM: clean close removes the source entry but keeps catalogue identity", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                document.CloseCommand.Execute(null);
                await Settle();
                Check.False(document.IsOpen);
                Check.False(workspace.Documents.Contains(document));
                Check.True(workspace.Files.Contains(document));
                Check.False(Models().Any(m => ReferenceEquals(m.Content, document)));
                workspace.OpenDocument(document);
                await Settle();
                Check.Same(document, Model(document).Content);
                Check.Equal(2, workspace.Documents.Count);
            });
            Add("MVVM: explicit dirty-close policy never discards the catalogue buffer", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                document.Text = "unsaved catalogue content";
                workspace.ProtectDirtyDocuments = false;
                workspace.CloseDocument(document);
                await Settle();
                workspace.OpenDocument(document);
                await Settle();
                Check.Equal("unsaved catalogue content", Editor(document).Editor.Text);
                Check.True(document.IsDirty);
            });
            Add("MVVM: save stores the requested text and clears only its dirty state", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "saved text";
                Check.True(await workspace.SaveAsync(document));
                Check.Equal((document.ContentId, "saved text"), store.Writes.Single());
                Check.False(document.IsDirty);
                Check.False(document.IsSaving);
                Check.False(document.SaveCommand.CanExecute(null));
                await Settle();
                Check.Equal(document.Name, Model(document).Title);
            });
            Add("MVVM: edits during asynchronous save remain dirty", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "snapshot";
                store.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = workspace.SaveAsync(document);
                await Wait(() => store.Writes.Count == 1);
                document.Text = "newer edits";
                store.Hold.SetResult(true);
                Check.True(await operation);
                Check.True(document.IsDirty);
                Check.Equal("newer edits", document.Text);
                workspace.RevertDocument(document);
                Check.Equal("snapshot", document.Text);
            });
            Add("MVVM: a second save cannot race the in-flight write", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "snapshot";
                store.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = workspace.SaveAsync(document);
                Check.False(await workspace.SaveAsync(document));
                Check.Equal(1, store.Writes.Count);
                store.Hold.SetResult(true);
                await operation;
            });
            Add("MVVM: in-flight save protects close even when dirty protection is disabled", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "snapshot";
                workspace.ProtectDirtyDocuments = false;
                store.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = workspace.SaveAsync(document);
                Model(document).Close();
                Check.True(document.IsOpen);
                Check.Equal(2, workspace.Documents.Count);
                store.Hold.SetResult(true);
                await operation;
            });
            Add("MVVM: failed save preserves dirty buffer and permits retry", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "must survive";
                store.Failure = new IOException("storage unavailable");
                var failed = false;
                try
                {
                    await workspace.SaveAsync(document);
                }
                catch (IOException)
                {
                    failed = true;
                }

                Check.True(failed);
                Check.True(document.IsDirty);
                Check.False(document.IsSaving);
                Check.Equal("must survive", document.Text);
                store.Failure = null;
                Check.True(await workspace.SaveAsync(document));
                Check.False(document.IsDirty);
            });
            Add("MVVM: disposed session ignores delayed save completion", async (workspace, store) =>
            {
                var document = workspace.Documents[0];
                document.Text = "unsaved";
                store.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = workspace.SaveAsync(document);
                workspace.Dispose();
                var status = workspace.Status;
                store.Hold.SetResult(true);
                Check.False(await operation);
                Check.True(document.IsDirty);
                Check.Equal(status, workspace.Status);
                Check.False(document.SaveCommand.CanExecute(null));
                Check.False(document.CloseCommand.CanExecute(null));
            });
            Add("MVVM: revert restores exact original content and rendered title", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                var saved = document.Text;
                document.Text = "temporary";
                document.RevertCommand.Execute(null);
                await Settle();
                Check.Equal(saved, document.Text);
                Check.False(document.IsDirty);
                Check.Equal(document.Name, Model(document).Title);
            });
            Add("MVVM: save all captures open dirty documents only", async (workspace, store) =>
            {
                workspace.Documents[0].Text = "one";
                workspace.Documents[1].Text = "two";
                workspace.Files[2].Text = "closed buffer";
                await workspace.SaveAllAsync();
                Check.Equal(2, store.Writes.Count);
                Check.True(workspace.Files[2].IsDirty);
            });
            Add("MVVM: read-only binding changes the actual editor", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                document.IsReadOnly = true;
                await Wait(() => Editor(document).Editor.IsReadOnly);
                document.IsReadOnly = false;
                await Wait(() => !Editor(document).Editor.IsReadOnly);
            });
            Add("MVVM: layout restore reopens saved identities without resetting dirty text", async (workspace, _) =>
            {
                var first = workspace.Documents[0];
                var second = workspace.Documents[1];
                var xml = workspace.CaptureLayout();
                first.Text = "not in the layout XML";
                workspace.CloseDocument(second);
                var added = workspace.NewDocument();
                Check.Equal(2, workspace.Documents.Count);
                workspace.RestoreLayout(xml);
                await Settle();
                Check.Equal(2, workspace.Documents.Count);
                Check.True(workspace.Documents.Contains(first));
                Check.True(workspace.Documents.Contains(second));
                Check.False(workspace.Documents.Contains(added));
                Check.False(added.IsOpen);
                Check.True(second.IsOpen);
                Check.Equal("not in the layout XML", first.Text);
                Check.True(first.IsDirty);
                Check.Equal(1, Models().Count(m => ReferenceEquals(m.Content, first)));
                Check.Equal(1, Models().Count(m => ReferenceEquals(m.Content, second)));
            });
            Add("MVVM: layout snapshots never serialize buffer text", (workspace, _) =>
            {
                workspace.Documents[0].Text = "DO-NOT-STORE-BUFFER-9fb1f1";
                Check.False(workspace.CaptureLayout().Contains("DO-NOT-STORE-BUFFER-9fb1f1", StringComparison.Ordinal));
                return Task.CompletedTask;
            });
            Add("MVVM: malformed restore preserves root, sources and buffers", async (workspace, _) =>
            {
                var root = dock.Layout;
                var documents = workspace.Documents.ToArray();
                Check.Throws<System.Xml.XmlException>(() => workspace.RestoreLayout("<LayoutRoot>"));
                Check.Same(root, dock.Layout);
                Check.Equal(documents.Length, workspace.Documents.Count);
                workspace.RestoreLayout(workspace.CaptureLayout());
                await Settle();
                Check.Same(documents[0], workspace.Documents[0]);
            });
            Add("MVVM: source removal updates open state and stale commands cannot close another document", async (workspace, _) =>
            {
                var document = workspace.Documents[0];
                workspace.Documents.Remove(document);
                await Settle();
                Check.False(document.IsOpen);
                Check.False(document.CloseCommand.CanExecute(null));
                Check.Equal(1, Models().Length);
            });
            Add("MVVM: foreign documents are rejected before changing sources", (workspace, _) =>
            {
                using var otherDock = new DockingManager();
                using var other = new MvvmWorkspace(otherDock, _ =>
                {
                }, new MemoryStore());
                Check.Throws<ArgumentException>(() => workspace.OpenDocument(other.Documents[0]));
                Check.Equal(2, workspace.Documents.Count);
                return Task.CompletedTask;
            });
            foreach (var scenario in new[]
            {
                "light",
                "dark",
                "rtl",
                "dirty",
                "restored"
            }

            )
                Add("MVVM: rendered workspace capture and finite geometry: " + scenario, async (workspace, _) =>
                {
                    dock.RequestedTheme = scenario == "dark" ? ElementTheme.Dark : ElementTheme.Light;
                    dock.Theme = new FluentTheme(dock.RequestedTheme);
                    dock.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    workspace.OpenDocument(workspace.Documents[1]);
                    if (scenario == "dirty")
                        workspace.Documents[1].Text += "\n// Unsaved change";
                    if (scenario == "restored")
                        workspace.RestoreLayout(workspace.CaptureLayout());
                    await Settle();
                    var editor = Editor(workspace.ActiveDocument!);
                    Check.True(editor.ActualWidth > 100 && editor.ActualHeight > 100);
                    var directory = Path.Combine(output, "visuals");
                    Directory.CreateDirectory(directory);
                    await VisualCapture.Save(dock, Path.Combine(directory, "mvvm-workspace-" + scenario + ".png"));
                    new XDocument(VisualScene.Measure(dock, "mvvm-workspace-" + scenario)).Save(Path.Combine(directory, "mvvm-workspace-" + scenario + ".xml"));
                    Check.Equal(2, Models().Length);
                    Check.True(dock.FindVisualChildren<LayoutDocumentPaneControl>().Any(p => p.ActualWidth > 100));
                });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                Add("XTEST: MVVM editor Save button invokes the real command", async (workspace, store) =>
                {
                    var document = workspace.Documents[0];
                    document.Text = "native save";
                    await Settle();
                    window.Activate();
                    var button = Button(Editor(document), "MvvmEditorSave");
                    Check.True(button.IsEnabled);
                    using var input = new X11TestInput();
                    input.MoveTo(button, new(button.ActualWidth / 2, button.ActualHeight / 2));
                    input.Press();
                    await Task.Delay(40);
                    input.Release();
                    await Wait(() => store.Writes.Count == 1 && !document.IsDirty);
                    Check.Equal("native save", store.Writes[0].Text);
                });
                Add("XTEST: MVVM editor Revert button restores the buffer", async (workspace, _) =>
                {
                    var document = workspace.Documents[0];
                    var saved = document.Text;
                    document.Text = "native revert";
                    await Settle();
                    window.Activate();
                    var button = Button(Editor(document), "MvvmEditorRevert");
                    using var input = new X11TestInput();
                    input.MoveTo(button, new(button.ActualWidth / 2, button.ActualHeight / 2));
                    input.Press();
                    await Task.Delay(40);
                    input.Release();
                    await Wait(() => document.Text == saved);
                    Check.False(document.IsDirty);
                });
            }

            return await tests.Run(output, "mvvm-workspace");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }

        void Add(string name, Func<MvvmWorkspace, MemoryStore, Task> body) => tests.Test(name, async () =>
        {
            dock.RequestedTheme = ElementTheme.Light;
            dock.Theme = new GenericTheme();
            dock.FlowDirection = FlowDirection.LeftToRight;
            var store = new MemoryStore();
            using var workspace = new MvvmWorkspace(dock, _ =>
            {
            }, store);
            window.Activate();
            await Settle();
            await body(workspace, store);
        });
        async Task Settle()
        {
            dock.Refresh();
            dock.UpdateLayout();
            await Task.Delay(80);
            dock.UpdateLayout();
        }

        LayoutDocument[] Models() => dock.Layout.Descendents().OfType<LayoutDocument>().ToArray();
        LayoutDocument Model(WorkspaceDocument document) => Models().Single(model => ReferenceEquals(model.Content, document));
        MvvmDocumentEditor Editor(WorkspaceDocument document) => dock.GetLayoutItemFromModel(Model(document)).View.FindVisualChildren<MvvmDocumentEditor>().Single();
    }

    private static Button Button(FrameworkElement root, string id) => root.FindVisualChildren<Button>().Single(button => AutomationProperties.GetAutomationId(button) == id);
    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 150 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "MVVM condition did not converge in the bounded wait.");
    }

    private sealed class MemoryStore : IWorkspaceStorage
    {
        public List<(string Id, string Text)> Writes { get; } = [];
        public TaskCompletionSource<bool>? Hold
        {
            get;
            set;
        }
        public Exception? Failure
        {
            get;
            set;
        }

        public async Task WriteAsync(string contentId, string text)
        {
            Writes.Add((contentId, text));
            if (Hold != null)
                await Hold.Task;
            if (Failure != null)
                throw Failure;
        }
    }
}
