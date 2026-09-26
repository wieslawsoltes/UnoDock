namespace UnoDock.Gallery;

public sealed partial class XamlTemplateWorkspace : UserControl, IDisposable
{
    private bool _inset;
    private int _replacement;
    public XamlTemplateWorkspace()
    {
        InitializeComponent();
        Actions = new XamlWorkspaceActions(Dock, this);
    }

    public XamlWorkspaceViewModel ViewModel { get; } = new();
    public XamlWorkspaceActions Actions
    {
        get;
    }
    public DockingManager Manager => Dock;

    public void SwapTemplate()
    {
        _inset = !_inset;
        Dock.Template = (ControlTemplate)Resources[_inset ? "InsetDockTemplate" : "OutlinedDockTemplate"];
    }

    public void TogglePalette() => Dock.Theme = Dock.Theme is Themes.ResourceDictionaryTheme ? new Themes.FluentTheme() : (Themes.ResourceDictionaryTheme)Resources["OceanDockTheme"];
    private void OnTogglePalette(object sender, RoutedEventArgs args) => TogglePalette();
    private void OnSwapTemplate(object sender, RoutedEventArgs args) => SwapTemplate();
    private void OnReplaceContent(object sender, RoutedEventArgs args) => ViewModel.PrimaryItem = new XamlWorkspaceItem
    {
        Title = "Bound content " + ++_replacement,
        Text = "The LayoutDocument.Content dependency property observes the replaced application object."
    };
    public new void Dispose() => Actions.Dispose();
}
