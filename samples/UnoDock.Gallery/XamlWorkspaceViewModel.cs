using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace UnoDock.Gallery;

public sealed class XamlWorkspaceViewModel : INotifyPropertyChanged
{
    private object? _activeDocument;
    private XamlWorkspaceItem _primaryItem;
    private int _sequence = 2;
    public XamlWorkspaceViewModel()
    {
        _primaryItem = new XamlWorkspaceItem
        {
            ContentId = "xaml-overview",
            Title = "Workspace.xaml",
            Text = "This editor is constructed by a compiled XAML DataTemplate.\n\nEdit its title or text; switch tabs, float, and dock it again.\nThe view model remains application-owned."
        };
        Documents.Add(_primaryItem);
        Documents.Add(new XamlWorkspaceItem { ContentId = "xaml-notes", Title = "Notes.md", Text = "# Notes\n\nBindings, commands, styles and templates are declared in XAML." });
        Tools.Add(new XamlWorkspaceItem { ContentId = "xaml-explorer", Title = "Explorer", IsTool = true, CanClose = false, Text = "Workspace.xaml\nNotes.md\nShared resources\nLayout snapshots" });
        Tools.Add(new XamlWorkspaceItem { ContentId = "xaml-properties", Title = "Properties", IsTool = true, CanClose = false, Text = "Application-owned objects\nStable content identifiers\nTwo-way title and selection bindings" });
        AddDocumentCommand = new WorkspaceCommand(() =>
        {
            var item = new XamlWorkspaceItem
            {
                ContentId = "xaml-document-" + ++_sequence,
                Title = "Document " + _sequence,
                Text = "New observable source item."
            };
            Documents.Add(item);
            ActiveDocument = item;
        });
        RemoveDocumentCommand = new WorkspaceCommand(() =>
        {
            if (ActiveDocument is XamlWorkspaceItem item && item.CanClose)
                Documents.Remove(item);
        });
    }

    public ObservableCollection<XamlWorkspaceItem> Documents { get; } = [];
    public ObservableCollection<XamlWorkspaceItem> Tools { get; } = [];
    public ICommand AddDocumentCommand
    {
        get;
    }
    public ICommand RemoveDocumentCommand
    {
        get;
    }
    public XamlWorkspaceItem PrimaryItem
    {
        get => _primaryItem;
        set => Set(ref _primaryItem, value);
    }
    public object? ActiveDocument
    {
        get => _activeDocument;
        set => Set(ref _activeDocument, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}
