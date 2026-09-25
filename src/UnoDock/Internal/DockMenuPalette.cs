using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Layout;
using Strings = UnoDock.Properties.Resources;

namespace UnoDock.Internal;

internal readonly record struct DockMenuPalette(Brush Surface, Brush Gutter, Brush Border, Brush Foreground, Brush Disabled, Brush Hover, Brush HoverBorder, double FontSize, double RowHeight, double MinWidth, FlowDirection FlowDirection)
{
    [ThreadStatic]
    private static Brush? _light, _gutter, _disabled;
    internal static DockMenuPalette Resolve(DockingManager manager)
    {
        var p = DockChrome.Palette(manager);
        var palette = manager.Theme is Themes.DictionaryTheme || manager.ActualTheme == ElementTheme.Dark && manager.Theme is not Themes.GenericTheme;
        var background = Get("MenuBrush", palette ? p.Surface : _light ??= DockChrome.Color(0xf5f5f5));
        return new(background, Get("MenuGutterBrush", palette ? p.Header : _gutter ??= DockChrome.Color(0xf0f0f0)), Get("MenuBorderBrush", p.Border), Get("MenuForegroundBrush", p.Foreground), Get("MenuDisabledBrush", palette ? p.Border : _disabled ??= DockChrome.Color(0x707070)), Get("MenuHoverBrush", p.Hover), Get("MenuHoverBorderBrush", p.Accent), p.FontSize, Math.Max(Number("MenuRowHeight", 22, 20, 72), Math.Ceiling(p.FontSize * 1.4) + 4), Number("MenuMinWidth", 235, 0, 600), manager.FlowDirection);
        Brush Get(string key, Brush fallback) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is Brush brush ? brush : fallback;
        double Number(string key, double fallback, double min, double max) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is double d && double.IsFinite(d) ? Math.Clamp(d, min, max) : fallback;
    }
}
