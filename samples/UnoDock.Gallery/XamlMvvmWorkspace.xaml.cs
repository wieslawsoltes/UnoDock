namespace UnoDock.Gallery;

public sealed partial class XamlMvvmWorkspace : UserControl, IDisposable
{
    public XamlMvvmWorkspace()
    {
        InitializeComponent();
        Actions = new XamlWorkspaceActions(Dock, this);
        Dock.DocumentClosed += OnDocumentClosed;
    }

    public XamlWorkspaceViewModel ViewModel { get; } = new();
    public XamlWorkspaceActions Actions
    {
        get;
    }
    public DockingManager Manager => Dock;

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs args)
    {
        if (args.Document.Content is XamlWorkspaceItem item)
            ViewModel.Documents.Remove(item);
    }

    public new void Dispose()
    {
        Dock.DocumentClosed -= OnDocumentClosed;
        Actions.Dispose();
    }
}
