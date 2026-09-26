namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private XamlSamplesPage? _xamlSamples;
    private LayoutDocument? _xamlSamplesDocument;
    private void ShowXamlSamples()
    {
        if (_xamlSamplesDocument?.Root == Dock.Layout)
        {
            _xamlSamplesDocument.IsActive = true;
            return;
        }

        DisposeXamlSamples();
        var page = new XamlSamplesPage();
        var document = new LayoutDocument
        {
            Title = "XAML workspaces",
            ContentId = "xaml-samples",
            Content = page
        };
        _xamlSamples = page;
        _xamlSamplesDocument = document;
        document.Closed += OnXamlSamplesClosed;
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First();
        pane.Children.Add(document);
        document.IsActive = true;
    }

    private void OnXamlSamplesClosed(object? sender, EventArgs args) => DisposeXamlSamples();
    private void DisposeXamlSamples()
    {
        if (_xamlSamplesDocument != null)
            _xamlSamplesDocument.Closed -= OnXamlSamplesClosed;
        _xamlSamples?.Dispose();
        _xamlSamples = null;
        _xamlSamplesDocument = null;
    }
}
