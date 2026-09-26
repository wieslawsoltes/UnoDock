namespace UnoDock.Gallery;

public sealed partial class XamlMvvmView : UserControl, IDisposable
{
    public ObservableCollection<XamlDocument> Documents
    {
        get;
    } = [new()
    {
        ContentId = "mvvm:first",
        Title = "Project.cs"
    }, new()
    {
        ContentId = "mvvm:second",
        Title = "Settings.xaml"
    }

    ];
    public DockingManager Manager => Dock;

    private int _next = 3;
    public XamlMvvmView() => InitializeComponent();
    private void AddDocument(object sender, RoutedEventArgs args)
    {
        var number = _next++;
        Documents.Add(new()
        {
            ContentId = "mvvm:" + number,
            Title = "Document " + number
        });
    }

    private void ToggleTheme(object sender, RoutedEventArgs args) => RequestedTheme = ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
    void IDisposable.Dispose() => Dock.Dispose();
}
