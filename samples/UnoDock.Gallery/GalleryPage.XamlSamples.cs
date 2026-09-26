using UnoDock.Layout;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private readonly List<Action> _releaseXamlSamples = [];
    private void ShowXamlSamples() => OpenXamlSample("XAML workspaces", new XamlSamplesPage());
    private void ShowXamlWorkbench() => OpenXamlSample("XAML workbench", new XamlWorkbenchView());
    private void ShowXamlMvvm() => OpenXamlSample("XAML MVVM", new XamlMvvmView());
    private void OpenXamlSample(string title, UserControl view)
    {
        var lifetime = (IDisposable)view;
        var owner = Dock.Layout;
        var document = new LayoutDocument
        {
            Title = title,
            ContentId = "xaml-sample:" + Guid.NewGuid().ToString("N"),
            Content = view
        };
        var released = false;
        void Release()
        {
            if (released)
                return;
            released = true;
            document.Closed -= Closed;
            Dock.LayoutChanged -= LayoutChanged;
            _releaseXamlSamples.Remove(Release);
            lifetime.Dispose();
        }

        void Closed(object? sender, EventArgs args) => Release();
        void LayoutChanged(object? sender, EventArgs args)
        {
            if (!ReferenceEquals(owner, Dock.Layout))
                Release();
        }

        document.Closed += Closed;
        Dock.LayoutChanged += LayoutChanged;
        _releaseXamlSamples.Add(Release);
        try
        {
            var pane = owner.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
            if (pane == null)
            {
                pane = new();
                owner.RootPanel.Children.Add(pane);
            }

            pane.Children.Add(document);
            document.IsActive = true;
        }
        catch
        {
            Release();
            throw;
        }
    }
}
