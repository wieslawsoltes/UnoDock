using UnoDock.Controls;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

// Independent vector construction from observed window/compass appearance, not
// reference artwork, image bytes, resource definitions, or extracted Path.Data.
internal readonly record struct DockGuidePalette(Brush Background, Brush Border, Brush Ink,
    Brush Fill, Brush Window, Brush Title, Brush Selection)
{
    [ThreadStatic] private static DockGuidePalette? _light, _dark;
    internal static DockGuidePalette Resolve(DockingManager manager, DockPalette palette)
    {
        // Resolve the explicit palette, not an unrelated RequestedTheme (preview-8 contrast regression).
        var dark = palette.Header is SolidColorBrush b ? (b.Color.R * .2126 + b.Color.G * .7152 + b.Color.B * .0722) < 128 : manager.ActualTheme == ElementTheme.Dark;
        var p = dark ? _dark ??= Create(true) : _light ??= Create(false);
        Brush R(string key, Brush fallback) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is Brush brush ? brush : fallback;
        return new(R("GuideBrush", p.Background), R("GuideBorderBrush", p.Border), R("GuideAccentBrush", p.Ink),
            R("GuideFillBrush", p.Fill), R("GuideWindowBrush", p.Window), R("GuideTitleBrush", p.Title), R("GuideSelectionBrush", p.Selection));
    }
    private static DockGuidePalette Create(bool dark) => new(
        Gradient(dark ? 0x53565bu : 0xd5d5d5u, dark ? 0x33353au : 0xf4f4f4u),
        DockChrome.Color(dark ? 0x8b929fu : 0xa1a1a1u), DockChrome.Color(dark ? 0xabc7fau : 0x6865b1u),
        Gradient(dark ? 0x405d85u : 0xaec9e1u, dark ? 0x7193b7u : 0xdcecf6u),
        DockChrome.Color(dark ? 0x282c34u : 0xffffffu), Gradient(dark ? 0x416eb0u : 0x587ac2u, dark ? 0x91aee5u : 0xc0d9f3u),
        DockChrome.Color(dark ? 0x546888u : 0xc2d5f3u));
    private static LinearGradientBrush Gradient(uint a, uint b) => new()
    {
        StartPoint = new(0, 0), EndPoint = new(1, 1), GradientStops =
        {
            new GradientStop { Color = DockChrome.Color(a).Color, Offset = 0 },
            new GradientStop { Color = DockChrome.Color(b).Color, Offset = 1 }
        }
    };
}
