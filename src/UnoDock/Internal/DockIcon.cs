using Microsoft.UI.Xaml.Media.Imaging;

namespace UnoDock.Internal;
/// <summary>Presents a content icon in docking chrome. IconContentTemplate and its
/// selector receive the icon value itself; without a template a 16x16 default
/// element is created, and an existing default element is reused.</summary>
internal static class DockIcon
{
    internal const double Size = 16;
    internal static void Present(ContentPresenter presenter, DockingManager manager, object? icon)
    {
        var template = icon == null ? null : manager.IconTemplate(icon, presenter);
        if (template != null)
        {
            presenter.ContentTemplate = template;
            presenter.Content = icon;
        }
        else
        {
            presenter.ContentTemplate = null;
            presenter.Content = icon == null ? null : CreateDefault(icon, presenter.Content);
        }

        presenter.Visibility = presenter.Content == null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>ImageSource, Uri or string address: an Image; WinUI IconSource: an IconSourceElement.</summary>
    internal static FrameworkElement? CreateDefault(object icon, object? existing = null)
    {
        switch (icon)
        {
            case ImageSource source:
                var image = existing as Image ?? NewImage();
                image.Source = source;
                return image;
            case Uri uri:
                return existing is Image { Source: BitmapImage { UriSource: { } current } } reused && current == uri ? reused : CreateDefault(new BitmapImage(uri), existing);
            case string address when !string.IsNullOrWhiteSpace(address):
                return CreateDefault(address.Contains("://", StringComparison.Ordinal) ? new Uri(address) : new Uri("ms-appx:///" + address.TrimStart('/')), existing);
            case IconSource source:
                var element = existing as IconSourceElement ?? new IconSourceElement
                {
                    Width = Size,
                    Height = Size,
                    VerticalAlignment = VerticalAlignment.Center
                };
                element.IconSource = source;
                return element;
            default:
                return null;
        }
    }

    private static Image NewImage() => new()
    {
        Width = Size,
        Height = Size,
        Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center
    };
}
