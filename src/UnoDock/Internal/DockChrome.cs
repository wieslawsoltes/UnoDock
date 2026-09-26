using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

internal static class DockChrome
{
    [ThreadStatic]
    private static ControlTemplate? _buttonTemplate;
    [ThreadStatic]
    private static ControlTemplate? _thumbTemplate;
    internal static ControlTemplate ThumbTemplate => _thumbTemplate ??= (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='{TemplateBinding Background}'/></ControlTemplate>");

    [ThreadStatic]
    private static Brush? _transparent;
    internal static Brush Transparent => _transparent ??= new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    [ThreadStatic]
    private static DockPalette? _light, _dark;
    internal static ControlTemplate ButtonTemplate => _buttonTemplate ??= (ControlTemplate)XamlReader.Load("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}">
            <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}"
                Foreground="{TemplateBinding Foreground}" Padding="{TemplateBinding Padding}"
                HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
                VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />
          </Border>
        </ControlTemplate>
        """);

    internal static DockPalette Palette(DockingManager manager)
    {
        var dark = DockThemeResources.EffectiveTheme(manager) == ElementTheme.Dark;
        if (manager.Theme is Themes.FluentTheme fluent)
            fluent.UpdateResources(manager);
        var p = Default(dark);
        var fontSize = N("FontSize", p.FontSize, 8, 32);
        var textScale = Math.Max(1, fontSize / 12);
        double Fit(string key, double fallback, double min, double max) => Math.Max(N(key, fallback, min, max), Math.Ceiling(fallback * textScale));
        return new(B("PaneBrush", p.Surface), B("HeaderBrush", p.Header), B("InactiveTabBrush", p.Tab), B("BorderBrush", p.Border), B("ForegroundBrush", p.Foreground), B("HoverBrush", p.Hover), B("PressedBrush", p.Pressed), B("AccentBrush", p.Accent), B("ActiveTitleBrush", p.ActiveTitle), fontSize, Fit("TitleHeight", p.TitleHeight, 18, 64), Fit("TabHeight", p.TabHeight, 20, 64), Fit("ToolTabHeight", p.ToolTabHeight, 20, 64), Fit("RailThickness", p.RailThickness, 24, 72), N("ButtonCornerRadius", 0, 0, 12));
        Brush B(string key, Brush fallback) => DockThemeResources.Brush(manager, key, key switch
        {
            "PaneBrush" => "LayerFillColorDefaultBrush",
            "HeaderBrush" => "SolidBackgroundFillColorBaseBrush",
            "InactiveTabBrush" => "ControlFillColorSecondaryBrush",
            "BorderBrush" => "ControlStrokeColorDefaultBrush",
            "ForegroundBrush" => "TextFillColorPrimaryBrush",
            "HoverBrush" => "SubtleFillColorSecondaryBrush",
            "PressedBrush" => "SubtleFillColorTertiaryBrush",
            "AccentBrush" => "AccentFillColorDefaultBrush",
            "ActiveTitleBrush" => "ControlFillColorInputActiveBrush",
            _ => key
        }, fallback);
        double N(string key, double fallback, double min, double max)
        {
            object? value;
            if (DockThemeResources.UsesFluent(manager))
                value = DockThemeResources.Find(manager, "UnoDock." + key);
            else
                manager.Resources.TryGetValue("UnoDock." + key, out value);
            return value is double number && double.IsFinite(number) ? Math.Clamp(number, min, max) : fallback;
        }
    }

    internal static DockPalette Default(bool dark) => dark ? _dark ??= CreateDefault(true) : _light ??= CreateDefault(false);
    private static DockPalette CreateDefault(bool dark) => new(Color(dark ? 0x202020u : 0xffffffu), Color(dark ? 0x282828u : 0xffffffu), Color(dark ? 0x333333u : 0xe8e8e8u), Color(dark ? 0x686868u : 0xacacacu), Color(dark ? 0xf2f2f2u : 0x000000u), Color(dark ? 0x444444u : 0xdceaf4u), Color(dark ? 0x555555u : 0xb9d8edu), Color(dark ? 0x8bc4f5u : 0x0067b8u), Color(dark ? 0x333333u : 0xffffffu), 12, 18, 20, 23, 26);
    internal static SolidColorBrush Color(uint rgb) => new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    internal static DockChromeButton Button(string label, Action action, string? name = null)
    {
        var button = new DockChromeButton
        {
            Content = label
        };
        button.Click += (_, _) => action();
        DockVisuals.SetName(button, name ?? label);
        ToolTipService.SetToolTip(button, name ?? label);
        return button;
    }

    internal static DockChromeButton Icon(DockGlyph glyph, Action action, string name)
    {
        var button = Button("", action, name);
        button.Content = Glyph(glyph);
        button.Width = 16;
        button.Height = 16;
        button.Margin = new(0);
        return button;
    }

    internal static Path Glyph(DockGlyph glyph)
    {
        var geometry = new PathGeometry();
        void Line(params Point[] points)
        {
            var figure = new PathFigure
            {
                StartPoint = points[0],
                IsFilled = points.Length > 2,
                IsClosed = points.Length > 2 && points[0] == points[^1]
            };
            foreach (var point in points.Skip(1))
                figure.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(figure);
        }

        switch (glyph)
        {
            case DockGlyph.Close:
                Line(new(2, 2), new(8, 8));
                Line(new(8, 2), new(2, 8));
                break;
            case DockGlyph.Pin:
                Line(new(3, 1), new(7, 1), new(7, 5), new(8, 6), new(2, 6), new(3, 5), new(3, 1));
                Line(new(5, 6), new(5, 10));
                break;
            case DockGlyph.Documents:
                Line(new(1, 2), new(9, 2));
                goto case DockGlyph.Menu;
            case DockGlyph.Menu:
                Line(new(2, 4), new(5, 7), new(8, 4), new(2, 4));
                break;
        }

        return new()
        {
            Data = geometry,
            Width = 10,
            Height = 10,
            StrokeThickness = 1.2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Stretch = Stretch.None
        };
    }
}
