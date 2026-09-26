using UnoDock.Layout;

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
    public ObservableCollection<XamlTool> Tools
    {
        get;
    } = [new()
    {
        ContentId = "mvvm:inspector",
        Title = "Inspector"
    }

    ];
    public DockingManager Manager => Dock;

    private int _next = 3, _nextTool = 2;
    public XamlMvvmView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Dock.Layout.ActiveContent == null && Dock.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault() is { } document)
                document.IsActive = true;
        };
    }

    private void AddDocument(object sender, RoutedEventArgs args)
    {
        var number = _next++;
        Documents.Add(new()
        {
            ContentId = "mvvm:" + number,
            Title = "Document " + number
        });
    }

    private void AddTool(object sender, RoutedEventArgs args)
    {
        var number = _nextTool++;
        Tools.Add(new()
        {
            ContentId = "mvvm:tool:" + number,
            Title = "Tool " + number
        });
    }

    private void ShowTools(object sender, RoutedEventArgs args)
    {
        foreach (var tool in Dock.Layout.Hidden.Where(tool => Tools.Any(item => ReferenceEquals(item, tool.Content))).ToArray())
            tool.Show();
    }

    private void ToggleTheme(object sender, RoutedEventArgs args) => RequestedTheme = ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
    void IDisposable.Dispose() => Dock.Dispose();
}
