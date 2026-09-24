"""One-use integration of reviewed preview16 source files; reference data is untouched."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent.parent

def edit(name, old, new, count=1):
    path = ROOT / name
    text = path.read_text()
    assert text.count(old) == count, (name, old[:100], text.count(old), count)
    path.write_text(text.replace(old, new))

def replace_method(name, start, next_method, replacement):
    path = ROOT / name
    text = path.read_text()
    assert text.count(start) == 1 and text.count(next_method) == 1
    a, b = text.index(start), text.index(next_method)
    assert a < b
    path.write_text(text[:a] + replacement + '\n' + text[b:])

page = 'samples/UnoDock.Gallery/GalleryPage.cs'
edit(page, '    private void AddDocument()\n    {', '    private void AddDocument()\n    {\n        if (_mvvmWorkspace?.IsCurrent == true) { _mvvmWorkspace.NewDocument(); return; }')
replace_method(page, '    private void ShowXml()', '    private async Task Save()', '''    private void ShowXml()
    {
        if (_mvvmWorkspace?.IsCurrent == true)
        {
            var xml = _mvvmWorkspace.CaptureLayout();
            var document = _mvvmWorkspace.NewDocument(); document.Name = "Layout.xml"; document.Text = xml;
            return;
        }
        using var text = new StringWriter(); new XmlLayoutSerializer(Dock).Serialize(text);
        var doc = Document("xml-" + _nextDocument++, "Layout.xml", Editor(text.ToString()));
        Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(doc); doc.IsActive = true;
    }''')
replace_method(page, '    private async Task Save()', '    private async Task Restore()', '    private async Task Save() => await SaveWorkspaceLayoutAsync();')
replace_method(page, '    private async Task Restore()', '    private void BindingDemo()', '    private async Task Restore() => await RestoreWorkspaceLayoutAsync();')
replace_method(page, '    private void BindingDemo()', '    private void Stress()', '''    private void BindingDemo()
    {
        StopMvvmWorkspace();
        _mvvmWorkspace = new MvvmWorkspace(Dock, Log);
        Log("MVVM workspace: observable documents and tools, retained buffers, guarded commands and storage.");
    }''')
edit(page, '    private void Stress()\n    {', '    private void Stress()\n    {\n        if (_mvvmWorkspace?.IsCurrent == true) { _mvvmWorkspace.AddDocuments(1000); return; }')

samples = 'samples/UnoDock.Gallery/GalleryPage.Samples.cs'
edit(samples, '        _sampleInspector?.Dispose(); _sampleInspector = null;\n        _selectingSample = true;', '        ++_workspaceEpoch; ++_restoreRequest; StopMvvmWorkspace();\n        _sampleInspector?.Dispose(); _sampleInspector = null;\n        _selectingSample = true;')
edit(samples, '    internal void RestoreSample(string xml)\n    {', '    internal void RestoreSample(string xml)\n    {\n        if (_mvvmWorkspace?.IsCurrent == true) { _mvvmWorkspace.RestoreLayout(xml); return; }')
edit(samples, '        _pageDisposed = true; _sampleInspector?.Dispose();', '        ++_workspaceEpoch; ++_restoreRequest; StopMvvmWorkspace();\n        _pageDisposed = true; _sampleInspector?.Dispose();')
edit(samples, 'UnoDock preview 15', 'UnoDock preview 16')

app = 'samples/UnoDock.Gallery/App.xaml.cs'
edit(app, '                    if (suite == "inspector-quality")', '''                    if (suite == "mvvm-workspace")
                        exitCode = await Testing.MvvmWorkspaceTests.Run(output);
                    else if (suite == "restore-ownership")
                        exitCode = await Testing.RestoreOwnershipTests.Run(output);
                    else if (suite == "inspector-quality")''')
edit(app, '                        exitCode |= await Testing.InspectorQualityTests.Run(output);', '''                        exitCode |= await Testing.InspectorQualityTests.Run(output);
                        exitCode |= await Testing.RestoreOwnershipTests.Run(output);
                        exitCode |= await Testing.MvvmWorkspaceTests.Run(output);''', 2)

workspace = 'samples/UnoDock.Gallery/MvvmWorkspace.cs'
edit(workspace, '    private readonly IWorkspaceStorage _storage;', '    private readonly IWorkspaceStorage _storage;\n    private readonly WorkspaceTemplateSelector _templates = new();')
edit(workspace, '_restoreLayoutCommand.Refresh(); }, () => IsCurrent);', '_restoreLayoutCommand?.Refresh(); }, () => IsCurrent);')
edit(workspace, '_dock.LayoutItemTemplateSelector = new WorkspaceTemplateSelector();', '_dock.LayoutItemTemplateSelector = _templates;')
edit(workspace, '        ConnectRoot(); OpenDocument(first);', '        Documents.CollectionChanged += DocumentsChanged;\n        ConnectRoot(); OpenDocument(first);')
edit(workspace, '    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)', '''    private void DocumentsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!IsCurrent) return;
        foreach (var document in _files.ToArray())
        {
            if (!IsCurrent) return;
            document.IsOpen = Documents.Contains(document);
        }
        if (!IsCurrent) return;
        SynchronizeBindings(); ActiveChanged(this, EventArgs.Empty);
        _saveAllCommand.Refresh(); _closeCommand.Refresh();
    }
    internal void AddDocuments(int count)
    {
        EnsureCurrent();
        if (count < 1 || count > 10000) throw new ArgumentOutOfRangeException(nameof(count));
        WorkspaceDocument? last = null;
        using (var batch = _dock.BeginLayoutUpdate())
        {
            for (var i = 0; i < count; i++)
            {
                EnsureCurrent();
                last = CreateDocument("Document " + _nextId + ".txt", "Lazy source-backed document " + _nextId);
                Documents.Add(last);
            }
        }
        if (last != null && IsCurrent) OpenDocument(last);
    }
    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)''')
edit(workspace, '        _dock.LayoutChanged -= LayoutChanged; _dock.ActiveContentChanged -= ActiveChanged;', '        Documents.CollectionChanged -= DocumentsChanged;\n        _dock.LayoutChanged -= LayoutChanged; _dock.ActiveContentChanged -= ActiveChanged;')
edit(workspace, '        PropertyChanged = null;\n    }', '        if (ReferenceEquals(_dock.LayoutItemTemplateSelector, _templates)) _dock.LayoutItemTemplateSelector = null;\n        PropertyChanged = null;\n    }')
edit(workspace, '    protected override DataTemplate SelectTemplateCore(object item) => (DataTemplate)Application.Current.Resources[item is WorkspaceDocument ? "WorkspaceDocumentTemplate" : "WorkspaceToolTemplate"];', '''    protected override DataTemplate SelectTemplateCore(object item) => item switch
    {
        WorkspaceDocument => (DataTemplate)Application.Current.Resources["WorkspaceDocumentTemplate"],
        WorkspaceTool => (DataTemplate)Application.Current.Resources["WorkspaceToolTemplate"],
        _ => null!
    };''')

storage = 'samples/UnoDock.Gallery/WorkspaceDocument.cs'
edit(storage, '        ContentId = contentId; _name = name; _text = _savedText = text;', '        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentNullException.ThrowIfNull(text);\n        ContentId = contentId; _name = name; _text = _savedText = text;')
edit(storage, '''        var file = await folder.CreateFileAsync(contentId + ".txt", CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, text);''', '''        StorageFile? temporary = null;
        try
        {
            temporary = await folder.CreateFileAsync(contentId + "-" + Guid.NewGuid().ToString("N") + ".tmp", CreationCollisionOption.FailIfExists);
            await FileIO.WriteTextAsync(temporary, text);
            await temporary.RenameAsync(contentId + ".txt", NameCollisionOption.ReplaceExisting);
            temporary = null;
        }
        finally { if (temporary != null) await temporary.DeleteAsync(); }''')

restore_tests = 'tests/UnoDock.Runtime.Tests/RestoreOwnershipTests.cs'
edit(restore_tests, '''    private static DockingManager Workspace() => new() { Layout = new() { RootPanel = new LayoutPanel(new LayoutDocumentPane(
        new LayoutDocument { ContentId = "first", Title = "First", Content = new object() },
        new LayoutDocument { ContentId = "second", Title = "Second", Content = new object() })) } };''', '''    private static DockingManager Workspace()
    {
        var pane = new LayoutDocumentPane(new LayoutDocument { ContentId = "first", Title = "First", Content = new object() });
        pane.Children.Add(new LayoutDocument { ContentId = "second", Title = "Second", Content = new object() });
        return new() { Layout = new() { RootPanel = new LayoutPanel(pane) } };
    }''')

edit('Directory.Build.props', '<Version>0.1.0-preview.15</Version>', '<Version>0.1.0-preview.16</Version>')
edit('README.md', '**Version: 0.1.0-preview.15.', '**Version: 0.1.0-preview.16.')
edit('README.md', '## Preview 15: typed property inspection and safe editing', '''## Preview 16: MVVM workspace and guarded layout restoration

The **MVVM binding** sample now has a source-backed file catalogue, observable open
documents and tool windows, compact editors, dirty tab titles, save/revert/close
commands, close protection, and layout capture/restore by stable identity. Save writes
application-owned text separately from layout XML. Edits made during an asynchronous
save remain dirty. Closing a tab keeps its buffer available for reopening in Workspace.
The existing classic Docking sample and its reference geometry checks remain unchanged.

The library serializer now serializes restore ownership per manager, rejects reentry
from user readers and callbacks, detects replaced/disposed workspaces, and preserves
both primary and cleanup errors. It does not overwrite a callback's replacement root.
Public/protected API mappings and original reference inventories are not relaxed.

See [MVVM workspace and restore boundaries](docs/mvvm-workspace.md). This is not a
pixel-identical recreation of a commercial sample, an arbitrary file editor, or a fix
for every platform's binding-valued style setter behavior. The new sample uses explicit
public adapter bindings and ordinary compiled Uno content templates.

## Preview 15: typed property inspection and safe editing''')
print('Preview16 product sources, sample entry points, and test selectors are wired.')
