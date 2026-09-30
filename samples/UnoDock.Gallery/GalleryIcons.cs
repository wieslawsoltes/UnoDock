using Microsoft.UI.Xaml.Media.Imaging;

namespace UnoDock.Gallery;
/// <summary>Original 32x32 icon assets (Assets/Icons) used as LayoutContent.IconSource;
/// docking chrome presents them at 16x16 DIP.</summary>
internal static class GalleryIcons
{
    [ThreadStatic]
    private static Dictionary<string, ImageSource>? _cache;
    internal static ImageSource Get(string name)
    {
        var cache = _cache ??= new(StringComparer.Ordinal);
        if (!cache.TryGetValue(name, out var source))
            cache.Add(name, source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/" + name + ".png")));
        return source;
    }
}
