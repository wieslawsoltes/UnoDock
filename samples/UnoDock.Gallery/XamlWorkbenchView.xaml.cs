using UnoDock.Layout;
using UnoDock.Layout.Serialization;

namespace UnoDock.Gallery;

public sealed partial class XamlWorkbenchView : UserControl, IDisposable
{
    public XamlDocument Document { get; } = new();
    public DockingManager Manager => Dock;

    private string? _savedLayout;
    private readonly Dictionary<string, object> _content = new(StringComparer.Ordinal);
    public XamlWorkbenchView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Dock.Layout.ActiveContent == null && FindDocument() is { } document)
                document.IsActive = true;
        };
    }

    private LayoutDocument? FindDocument() => Dock.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => d.ContentId == "xaml:document");
    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        RequestedTheme = ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        Status.Text = "Theme: " + RequestedTheme;
    }

    private void ToggleFloating(object sender, RoutedEventArgs e)
    {
        var document = FindDocument();
        if (document == null)
        {
            Status.Text = "Document closed. Reopen the sample to restore it.";
            return;
        }

        if (document.IsFloating)
            document.Dock();
        else
            document.Float();
        Status.Text = document.IsFloating ? "Native document window · drag its caption to dock" : "Document docked";
    }

    private void ToggleNotes(object sender, RoutedEventArgs e)
    {
        Dock.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == "xaml:notes")?.ToggleAutoHide();
    }

    private void ToggleLayout(object sender, RoutedEventArgs e)
    {
        var serializer = new XmlLayoutSerializer(Dock);
        if (_savedLayout == null)
        {
            _content.Clear();
            foreach (var model in Dock.Layout.Descendents().OfType<LayoutContent>())
                if (model.ContentId is { } id && model.Content is { } content)
                    _content[id] = content;
            using var writer = new StringWriter();
            serializer.Serialize(writer);
            _savedLayout = writer.ToString();
            Status.Text = "Layout captured · rearrange, then press again to restore";
        }
        else
        {
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                if (args.Model.ContentId is { } id && _content.TryGetValue(id, out var content))
                    args.Content = content;
                else
                    args.Cancel = true;
            };
            serializer.Deserialize(new StringReader(_savedLayout));
            _savedLayout = null;
            Status.Text = "Layout restored by stable ContentId; editor payloads retained";
        }
    }

    void IDisposable.Dispose() => Dock.Dispose();
}
