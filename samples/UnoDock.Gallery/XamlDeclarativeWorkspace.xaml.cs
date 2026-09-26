namespace UnoDock.Gallery;

public sealed partial class XamlDeclarativeWorkspace : UserControl, IDisposable
{
    public XamlDeclarativeWorkspace()
    {
        InitializeComponent();
        Actions = new XamlWorkspaceActions(Dock, this);
    }

    public XamlWorkspaceActions Actions
    {
        get;
    }
    public DockingManager Manager => Dock;

    public new void Dispose() => Actions.Dispose();
}
