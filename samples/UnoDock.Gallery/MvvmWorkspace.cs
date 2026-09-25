using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Layout.Serialization;

namespace UnoDock.Gallery;
[Bindable]
public sealed class MvvmWorkspace : INotifyPropertyChanged, IDisposable
{
    private readonly DockingManager _dock;
    private readonly IWorkspaceStorage _storage;
    private readonly WorkspaceTemplateSelector _templates = new();
    private readonly Action<string> _log;
    private readonly ObservableCollection<WorkspaceDocument> _files = [];
    private readonly ObservableCollection<WorkspaceTool> _tools = [];
    private readonly Dictionary<LayoutContent, List<BoundProperty>> _bindings = new(ReferenceEqualityComparer.Instance);
    private readonly WorkspaceCommand _newCommand, _saveAllCommand, _closeCommand, _saveLayoutCommand, _restoreLayoutCommand;
    private LayoutRoot? _root;
    private WorkspaceDocument? _activeDocument;
    private bool _disposed, _synchronizing, _restoring, _protectDirty = true;
    private int _nextId = 1;
    private string _status = "Ready", _savedLayout = "";
    private sealed record BoundProperty(LayoutItem Item, DependencyProperty Property, Binding Binding);
    internal MvvmWorkspace(DockingManager dock, Action<string> log, IWorkspaceStorage? storage = null)
    {
        _dock = dock;
        _log = log;
        _storage = storage ?? new LocalWorkspaceStorage();
        Files = new(_files);
        _newCommand = new(() => NewDocument(), () => IsCurrent);
        _saveAllCommand = new(() => Observe(SaveAllAsync()), () => IsCurrent && Documents.Any(d => d.IsDirty && !d.IsSaving));
        _closeCommand = new(() =>
        {
            if (ActiveDocument != null)
                CloseDocument(ActiveDocument);
        }, () => IsCurrent && ActiveDocument?.IsOpen == true && !ActiveDocument.IsSaving);
        _saveLayoutCommand = new(() =>
        {
            _savedLayout = CaptureLayout();
            Status = "Layout snapshot captured; document buffers remain application-owned.";
            _restoreLayoutCommand?.Refresh();
        }, () => IsCurrent);
        _restoreLayoutCommand = new(() => RestoreLayout(_savedLayout), () => IsCurrent && _savedLayout.Length != 0);
        var first = CreateDocument("Welcome.md", "# UnoDock MVVM workspace\n\nThese documents are view models, not controls.\nEdit a buffer to mark its tab dirty. Save writes application-owned text to local storage.\n\nUse Workspace to reopen a closed document, and the toolbar to capture and restore docking layout.\nDirty buffers are protected from closing by default.\n");
        var second = CreateDocument("Workspace.cs", "using UnoDock;\nusing UnoDock.Layout;\n\n// Observable documents, retained editors and explicit public bindings.\n// Float a tab, edit it, dock it back, then save or revert the buffer.\nvar manager = new DockingManager();\n");
        _ = CreateDocument("Notes.txt", "This closed document stays in the workspace catalogue.\nDouble-click its row to open it.\n");
        Documents.Add(first);
        first.IsOpen = true;
        Documents.Add(second);
        second.IsOpen = true;
        var explorer = new WorkspaceTool("mvvm-explorer", "Workspace", BuildExplorer());
        var properties = new WorkspaceTool("mvvm-properties", "Document properties", BuildProperties());
        var output = new WorkspaceTool("mvvm-output", "Output", BuildOutput());
        _tools.Add(explorer);
        _tools.Add(properties);
        _tools.Add(output);
        using (var batch = _dock.BeginLayoutUpdate())
        {
            _dock.DocumentsSource = null;
            _dock.AnchorablesSource = null;
            _dock.LayoutItemContainerStyle = null;
            _dock.LayoutItemTemplate = null;
            _dock.LayoutItemTemplateSelector = _templates;
            var documents = new LayoutDocumentPane();
            foreach (var document in Documents)
                documents.Children.Add(new LayoutDocument { ContentId = document.ContentId, Title = document.Title, Content = document });
            var left = new LayoutAnchorablePane(Tool(explorer))
            {
                DockWidth = new(210),
                DockMinWidth = 130
            };
            var right = new LayoutAnchorablePane(Tool(properties))
            {
                DockWidth = new(220),
                DockMinWidth = 150
            };
            var bottom = new LayoutAnchorablePane(Tool(output))
            {
                DockHeight = new(125),
                DockMinHeight = 70
            };
            var center = new LayoutPanel(documents)
            {
                Orientation = Orientation.Vertical
            };
            center.Children.Add(bottom);
            var panel = new LayoutPanel(left)
            {
                Orientation = Orientation.Horizontal
            };
            panel.Children.Add(center);
            panel.Children.Add(right);
            _dock.Layout = new()
            {
                RootPanel = panel
            };
            _dock.DocumentsSource = Documents;
            _dock.AnchorablesSource = _tools;
        }

        _dock.LayoutChanged += LayoutChanged;
        _dock.ActiveContentChanged += ActiveChanged;
        _dock.DocumentClosing += DocumentClosing;
        _dock.DocumentClosed += DocumentClosed;
        Documents.CollectionChanged += DocumentsChanged;
        ConnectRoot();
        OpenDocument(first);
    }

    public ObservableCollection<WorkspaceDocument> Documents { get; } = [];
    public ReadOnlyObservableCollection<WorkspaceDocument> Files { get; }
    public WorkspaceDocument? ActiveDocument => _activeDocument;

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
                return;
            _status = value;
            Changed();
        }
    }

    public bool ProtectDirtyDocuments
    {
        get => _protectDirty;
        set
        {
            if (_protectDirty == value)
                return;
            _protectDirty = value;
            Changed();
        }
    }

    public System.Windows.Input.ICommand NewCommand => _newCommand;
    public System.Windows.Input.ICommand SaveAllCommand => _saveAllCommand;
    public System.Windows.Input.ICommand CloseDocumentCommand => _closeCommand;
    public System.Windows.Input.ICommand CaptureLayoutCommand => _saveLayoutCommand;
    public System.Windows.Input.ICommand RestoreLayoutCommand => _restoreLayoutCommand;

    public event PropertyChangedEventHandler? PropertyChanged;
    internal bool IsCurrent => !_disposed && ReferenceEquals(_dock.DocumentsSource, Documents) && ReferenceEquals(_dock.AnchorablesSource, _tools);

    public WorkspaceDocument NewDocument()
    {
        EnsureCurrent();
        var document = CreateDocument("Untitled " + _nextId + ".txt", "");
        OpenDocument(document);
        return document;
    }

    private WorkspaceDocument CreateDocument(string name, string text)
    {
        WorkspaceDocument document = null!;
        document = new("mvvm-" + _nextId++, name, text, () => Observe(SaveAsync(document)), () => RevertDocument(document), () => CloseDocument(document));
        document.PropertyChanged += DocumentChanged;
        _files.Add(document);
        return document;
    }

    public void OpenDocument(WorkspaceDocument document)
    {
        EnsureOwned(document);
        if (!Documents.Contains(document))
            Documents.Add(document);
        if (!IsCurrent || !Documents.Contains(document))
            return;
        document.IsOpen = true;
        SynchronizeBindings();
        var model = Model(document);
        if (model != null)
            model.IsActive = true;
        SetActive(document);
        Status = "Opened " + document.Name;
    }

    public void CloseDocument(WorkspaceDocument document)
    {
        EnsureOwned(document);
        if (document.IsSaving)
        {
            Status = "A save is in progress; the document stays open.";
            return;
        }

        Model(document)?.Close();
    }

    public void RevertDocument(WorkspaceDocument document)
    {
        EnsureOwned(document);
        if (!document.IsOpen || document.IsSaving)
            return;
        document.Revert();
        if (IsCurrent)
            Status = "Reverted " + document.Name + " to its last saved buffer.";
    }

    internal async Task<bool> SaveAsync(WorkspaceDocument document)
    {
        EnsureOwned(document);
        if (!document.IsOpen || document.IsSaving)
            return false;
        var snapshot = document.Text;
        try
        {
            document.IsSaving = true;
            if (!IsCurrent || !Documents.Contains(document))
                return false;
            Status = "Saving " + document.Name + "…";
            await _storage.WriteAsync(document.ContentId, snapshot);
            // An old async completion cannot modify a replacement sample. New edits
            // made during the write remain dirty relative to the snapshot just saved.
            if (!IsCurrent || !Documents.Contains(document))
                return false;
            document.AcceptSaved(snapshot);
            if (IsCurrent)
            {
                Status = "Saved " + document.Name + (document.IsDirty ? "; newer edits remain unsaved." : ".");
                _log(Status);
            }

            return true;
        }
        finally
        {
            if (IsCurrent)
                document.IsSaving = false;
        }
    }

    internal async Task SaveAllAsync()
    {
        EnsureCurrent();
        foreach (var document in Documents.Where(d => d.IsDirty).ToArray())
        {
            if (!IsCurrent)
                break;
            if (Documents.Contains(document) && !document.IsSaving)
                await SaveAsync(document);
        }
    }

    private async void Observe(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception error)
        {
            if (IsCurrent)
            {
                Status = "Operation failed: " + error.Message;
                _log(Status);
            }
        }
    }

    public string CaptureLayout()
    {
        EnsureCurrent();
        using var text = new StringWriter();
        new XmlLayoutSerializer(_dock).Serialize(text);
        return text.ToString();
    }

    public void RestoreLayout(string xml)
    {
        EnsureCurrent();
        ArgumentNullException.ThrowIfNull(xml);
        if (_restoring)
            throw new InvalidOperationException("This workspace already has a layout restore in progress.");
        _restoring = true;
        try
        {
            using (var batch = _dock.BeginLayoutUpdate())
            {
                var serializer = new XmlLayoutSerializer(_dock);
                serializer.LayoutSerializationCallback += (_, e) =>
                {
                    EnsureCurrent();
                    var document = _files.FirstOrDefault(d => d.ContentId == e.Model.ContentId);
                    var tool = _tools.FirstOrDefault(t => t.ContentId == e.Model.ContentId);
                    e.Content = e.Model is LayoutDocument ? document : tool;
                    e.Cancel = e.Content == null;
                };
                serializer.Deserialize(new StringReader(xml));
                EnsureCurrent();
                var wanted = _dock.Layout.Descendents().OfType<LayoutDocument>().Select(d => d.Content).OfType<WorkspaceDocument>().ToArray();
                foreach (var document in Documents.Where(d => !wanted.Contains(d)).ToArray())
                    Documents.Remove(document);
                for (var i = 0; i < wanted.Length; i++)
                {
                    var index = Documents.IndexOf(wanted[i]);
                    if (index < 0)
                        Documents.Insert(i, wanted[i]);
                    else if (index != i)
                        Documents.Move(index, i);
                }

                foreach (var document in _files)
                    document.IsOpen = Documents.Contains(document);
            }

            if (IsCurrent)
            {
                ConnectRoot();
                ActiveChanged(this, EventArgs.Empty);
                Status = "Layout restored by stable identity; unsaved buffers were preserved.";
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    private LayoutDocument? Model(WorkspaceDocument document) => _dock.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => ReferenceEquals(d.Content, document));
    private void DocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (!IsCurrent || e.Document.Content is not WorkspaceDocument document || !Documents.Contains(document))
            return;
        if (document.IsSaving || (ProtectDirtyDocuments && document.IsDirty))
        {
            e.Cancel = true;
            Status = document.IsSaving ? "Save in progress; close cancelled." : "Unsaved changes: save or revert " + document.Name + " before closing.";
        }
    }

    private void DocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (!IsCurrent || e.Document.Content is not WorkspaceDocument document || !Documents.Contains(document))
            return;
        Documents.Remove(document);
        document.IsOpen = false;
        if (!IsCurrent)
            return;
        ReleaseBinding(e.Document);
        if (ReferenceEquals(ActiveDocument, document))
            SetActive(_dock.Layout.LastFocusedDocument?.Content as WorkspaceDocument ?? Documents.FirstOrDefault());
        Status = "Closed " + document.Name + "; its buffer remains in Workspace.";
    }

    private void LayoutChanged(object? sender, EventArgs e)
    {
        if (IsCurrent)
            ConnectRoot();
    }

    private void ConnectRoot()
    {
        if (!IsCurrent)
            return;
        if (!ReferenceEquals(_root, _dock.Layout))
        {
            if (_root != null)
                _root.Updated -= RootUpdated;
            foreach (var model in _bindings.Keys.ToArray())
                ReleaseBinding(model);
            _root = _dock.Layout;
            _root.Updated += RootUpdated;
        }

        SynchronizeBindings();
    }

    private void RootUpdated(object? sender, EventArgs e)
    {
        if (IsCurrent)
            SynchronizeBindings();
    }

    private void SynchronizeBindings()
    {
        if (_synchronizing || !IsCurrent)
            return;
        _synchronizing = true;
        try
        {
            foreach (var stale in _bindings.Keys.Where(m => !ReferenceEquals(m.Root, _dock.Layout)).ToArray())
                ReleaseBinding(stale);
            foreach (var model in _dock.Layout.Descendents().OfType<LayoutDocument>().ToArray())
            {
                if (!IsCurrent)
                    break;
                if (_bindings.ContainsKey(model) || model.Content is not WorkspaceDocument document || !_files.Contains(document))
                    continue;
                var item = _dock.GetLayoutItemFromModel(model);
                var registrations = new List<BoundProperty>();
                _bindings.Add(model, registrations);
                Bind(LayoutItem.TitleProperty, nameof(WorkspaceDocument.Title));
                Bind(LayoutItem.ContentIdProperty, nameof(WorkspaceDocument.ContentId));
                Bind(LayoutItem.CloseCommandProperty, nameof(WorkspaceDocument.CloseCommand));
                void Bind(DependencyProperty property, string path)
                {
                    if (!IsCurrent || !ReferenceEquals(model.Root, _dock.Layout))
                        return;
                    var binding = new Binding
                    {
                        Source = document,
                        Path = new(path),
                        Mode = BindingMode.OneWay
                    };
                    registrations.Add(new(item, property, binding));
                    item.SetBinding(property, binding);
                }
            }
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void ReleaseBinding(LayoutContent model)
    {
        if (!_bindings.Remove(model, out var bindings))
            return;
        foreach (var registration in bindings)
            if (ReferenceEquals(registration.Item.GetBindingExpression(registration.Property)?.ParentBinding, registration.Binding))
                registration.Item.ClearValue(registration.Property);
    }

    private void ActiveChanged(object? sender, EventArgs e)
    {
        if (!IsCurrent)
            return;
        if (_dock.Layout.LastFocusedDocument?.Content is WorkspaceDocument document && Documents.Contains(document))
            SetActive(document);
        else if (ActiveDocument != null && !Documents.Contains(ActiveDocument))
            SetActive(Documents.FirstOrDefault());
    }

    private void SetActive(WorkspaceDocument? document)
    {
        if (ReferenceEquals(_activeDocument, document))
            return;
        _activeDocument = document;
        Changed(nameof(ActiveDocument));
        _closeCommand.Refresh();
    }

    private void DocumentsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!IsCurrent)
            return;
        foreach (var document in _files.ToArray())
        {
            if (!IsCurrent)
                return;
            document.IsOpen = Documents.Contains(document);
        }

        if (!IsCurrent)
            return;
        SynchronizeBindings();
        ActiveChanged(this, EventArgs.Empty);
        _saveAllCommand.Refresh();
        _closeCommand.Refresh();
    }

    internal void AddDocuments(int count)
    {
        EnsureCurrent();
        if (count < 1 || count > 10000)
            throw new ArgumentOutOfRangeException(nameof(count));
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

        if (last != null && IsCurrent)
            OpenDocument(last);
    }

    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsCurrent)
        {
            _saveAllCommand.Refresh();
            _closeCommand.Refresh();
        }
    }

    private void EnsureCurrent()
    {
        if (!IsCurrent)
            throw new InvalidOperationException("This MVVM workspace is no longer attached to its docking sources.");
    }

    private void EnsureOwned(WorkspaceDocument document)
    {
        EnsureCurrent();
        ArgumentNullException.ThrowIfNull(document);
        if (!_files.Contains(document))
            throw new ArgumentException("Document belongs to another workspace.", nameof(document));
    }

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private FrameworkElement BuildExplorer()
    {
        var grid = new Grid
        {
            Padding = new(6)
        };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
        tools.Children.Add(CommandButton("New", NewCommand, "MvvmNewDocument"));
        tools.Children.Add(CommandButton("Save all", SaveAllCommand, "MvvmSaveAll"));
        grid.Children.Add(tools);
        var list = new ListView
        {
            ItemsSource = Files,
            ItemTemplate = (DataTemplate)Application.Current.Resources["WorkspaceFileTemplate"],
            SelectionMode = ListViewSelectionMode.Single
        };
        list.ItemContainerStyle = new Style(typeof(ListViewItem))
        {
            Setters =
            {
                new Setter(Control.MinHeightProperty, 26d),
                new Setter(Control.PaddingProperty, new Thickness(4, 1, 4, 1))
            }
        };
        AutomationProperties.SetAutomationId(list, "MvvmFiles");
        list.DoubleTapped += (_, _) =>
        {
            if (IsCurrent && list.SelectedItem is WorkspaceDocument document)
                OpenDocument(document);
        };
        list.KeyDown += (_, e) =>
        {
            if (!e.Handled && e.Key == Windows.System.VirtualKey.Enter && IsCurrent && list.SelectedItem is WorkspaceDocument document)
            {
                OpenDocument(document);
                e.Handled = true;
            }
        };
        Grid.SetRow(list, 1);
        grid.Children.Add(list);
        return grid;
    }

    private FrameworkElement BuildProperties()
    {
        var stack = new StackPanel
        {
            Padding = new(9),
            Spacing = 9,
            DataContext = this
        };
        Text("ActiveDocument.Name", 13);
        Text("ActiveDocument.ContentId", 11);
        var readOnly = new CheckBox
        {
            Content = "Read-only editor",
            FontSize = 12,
            MinHeight = 26
        };
        readOnly.SetBinding(CheckBox.IsCheckedProperty, new Binding { Path = new("ActiveDocument.IsReadOnly"), Mode = BindingMode.TwoWay });
        stack.Children.Add(readOnly);
        var protect = new CheckBox
        {
            Content = "Protect unsaved buffers",
            FontSize = 12,
            MinHeight = 26
        };
        protect.SetBinding(CheckBox.IsCheckedProperty, new Binding { Source = this, Path = new(nameof(ProtectDirtyDocuments)), Mode = BindingMode.TwoWay });
        stack.Children.Add(protect);
        stack.Children.Add(new TextBlock { Text = "Characters / Lines", FontSize = 11 });
        Text("ActiveDocument.CharacterCount", 12);
        Text("ActiveDocument.LineCount", 12);
        stack.Children.Add(CommandButton("Capture layout", CaptureLayoutCommand, "MvvmCaptureLayout"));
        stack.Children.Add(CommandButton("Restore layout", RestoreLayoutCommand, "MvvmRestoreLayout"));
        stack.Children.Add(new TextBlock { Text = "Double-click a Workspace row to open it. Layout snapshots store IDs and placement, never editor text.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .75 });
        return new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        void Text(string path, double size)
        {
            var text = new TextBlock
            {
                FontSize = size,
                TextWrapping = TextWrapping.Wrap
            };
            text.SetBinding(TextBlock.TextProperty, new Binding { Path = new(path) });
            stack.Children.Add(text);
        }
    }

    private FrameworkElement BuildOutput()
    {
        var text = new TextBlock
        {
            Margin = new(10),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        text.SetBinding(TextBlock.TextProperty, new Binding { Source = this, Path = new(nameof(Status)) });
        return text;
    }

    private static Button CommandButton(string text, System.Windows.Input.ICommand command, string id)
    {
        var button = new Button
        {
            Content = text,
            Command = command,
            FontSize = 12,
            MinHeight = 24,
            Padding = new(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }

    private static LayoutAnchorable Tool(WorkspaceTool tool) => new()
    {
        ContentId = tool.ContentId,
        Title = tool.Title,
        Content = tool,
        CanClose = false
    };
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Documents.CollectionChanged -= DocumentsChanged;
        _dock.LayoutChanged -= LayoutChanged;
        _dock.ActiveContentChanged -= ActiveChanged;
        _dock.DocumentClosing -= DocumentClosing;
        _dock.DocumentClosed -= DocumentClosed;
        if (_root != null)
            _root.Updated -= RootUpdated;
        _root = null;
        foreach (var model in _bindings.Keys.ToArray())
            ReleaseBinding(model);
        foreach (var document in _files)
        {
            document.PropertyChanged -= DocumentChanged;
            document.DetachCommands();
        }

        _newCommand.Detach();
        _saveAllCommand.Detach();
        _closeCommand.Detach();
        _saveLayoutCommand.Detach();
        _restoreLayoutCommand.Detach();
        if (ReferenceEquals(_dock.LayoutItemTemplateSelector, _templates))
            _dock.LayoutItemTemplateSelector = null;
        PropertyChanged = null;
    }
}
