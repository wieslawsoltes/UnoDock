using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Xceed.Wpf.AvalonDock.Internal;

// Independent compact chrome. Geometry and layout follow public visual observations,
// not the original resource dictionaries, templates or vector assets.
internal readonly record struct DockPalette(Brush Surface, Brush Header, Brush Tab, Brush Border,
    Brush Foreground, Brush Hover, Brush Pressed, Brush Accent, Brush ActiveTitle,
    double FontSize, double TitleHeight, double TabHeight, double ToolTabHeight, double RailThickness);

internal enum DockGlyph { Close, Pin, Menu, Documents }

internal static class DockChrome
{
    [ThreadStatic] private static ControlTemplate? _buttonTemplate;
    [ThreadStatic] private static ControlTemplate? _thumbTemplate;
    internal static ControlTemplate ThumbTemplate => _thumbTemplate ??= (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='{TemplateBinding Background}'/></ControlTemplate>");
    [ThreadStatic] private static Brush? _transparent;
    internal static Brush Transparent => _transparent ??= new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    [ThreadStatic] private static DockPalette? _light, _dark;
    internal static ControlTemplate ButtonTemplate => _buttonTemplate ??= (ControlTemplate)XamlReader.Load("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}"
                Foreground="{TemplateBinding Foreground}" Padding="{TemplateBinding Padding}"
                HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
                VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />
          </Border>
        </ControlTemplate>
        """);
    internal static DockPalette Palette(DockingManager manager)
    {
        var dark = manager.ActualTheme == ElementTheme.Dark && manager.Theme is not Themes.GenericTheme;
        var p = Default(dark);
        return new(B("PaneBrush", p.Surface), B("HeaderBrush", p.Header), B("InactiveTabBrush", p.Tab),
            B("BorderBrush", p.Border), B("ForegroundBrush", p.Foreground), B("HoverBrush", p.Hover),
            B("PressedBrush", p.Pressed), B("AccentBrush", p.Accent), B("ActiveTitleBrush", p.ActiveTitle),
            N("FontSize", p.FontSize, 8, 32), N("TitleHeight", p.TitleHeight, 18, 64),
            N("TabHeight", p.TabHeight, 20, 64), N("ToolTabHeight", p.ToolTabHeight, 20, 64),
            N("RailThickness", p.RailThickness, 24, 72));
        Brush B(string key, Brush fallback) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is Brush b ? b : fallback;
        double N(string key, double fallback, double min, double max) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is double d && double.IsFinite(d) ? Math.Clamp(d, min, max) : fallback;
    }
    internal static DockPalette Default(bool dark) => dark ? _dark ??= CreateDefault(true) : _light ??= CreateDefault(false);
    private static DockPalette CreateDefault(bool dark) => new(
        Color(dark ? 0x202020u : 0xffffffu), Color(dark ? 0x282828u : 0xffffffu),
        Color(dark ? 0x333333u : 0xe8e8e8u), Color(dark ? 0x686868u : 0xacacacu),
        Color(dark ? 0xf2f2f2u : 0x000000u), Color(dark ? 0x444444u : 0xdceaf4u),
        Color(dark ? 0x555555u : 0xb9d8edu), Color(dark ? 0x8bc4f5u : 0x0067b8u),
        Color(dark ? 0x333333u : 0xffffffu), 12, 18, 20, 23, 26);
    internal static SolidColorBrush Color(uint rgb) => new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    internal static DockChromeButton Button(string label, Action action, string? name = null)
    {
        var button = new DockChromeButton { Content = label };
        button.Click += (_, _) => action();
        DockVisuals.SetName(button, name ?? label); ToolTipService.SetToolTip(button, name ?? label);
        return button;
    }
    internal static DockChromeButton Icon(DockGlyph glyph, Action action, string name)
    {
        var button = Button("", action, name);
        button.Content = Glyph(glyph); button.Width = 16; button.Height = 16; button.Margin = new(0);
        return button;
    }
    internal static Path Glyph(DockGlyph glyph)
    {
        var geometry = new PathGeometry();
        void Line(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsFilled = points.Length > 2, IsClosed = points.Length > 2 && points[0] == points[^1] };
            foreach (var point in points.Skip(1)) figure.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(figure);
        }
        switch (glyph)
        {
            case DockGlyph.Close: Line(new(2, 2), new(8, 8)); Line(new(8, 2), new(2, 8)); break;
            case DockGlyph.Pin:
                Line(new(3, 1), new(7, 1), new(7, 5), new(8, 6), new(2, 6), new(3, 5), new(3, 1));
                Line(new(5, 6), new(5, 10)); break;
            case DockGlyph.Documents: Line(new(1, 2), new(9, 2)); goto case DockGlyph.Menu;
            case DockGlyph.Menu: Line(new(2, 4), new(5, 7), new(8, 4), new(2, 4)); break;
        }
        return new() { Data = geometry, Width = 10, Height = 10, StrokeThickness = 1.2,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false, Stretch = Stretch.None };
    }
}

internal sealed class DockChromeButton : Button
{
    private DockPalette _palette;
    private bool _over, _pressed;
    internal DockChromeButton()
    {
        MinHeight = 0; MinWidth = 0; Padding = new(0); BorderThickness = new(1); CornerRadius = new(0);
        HorizontalContentAlignment = HorizontalAlignment.Center; VerticalContentAlignment = VerticalAlignment.Center;
        Template = DockChrome.ButtonTemplate; UseSystemFocusVisuals = true;
        Configure(DockChrome.Default(false));
        PointerEntered += (_, _) => { _over = true; Paint(); };
        PointerExited += (_, _) => { _over = false; Paint(); };
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => { _pressed = true; Paint(); }), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => { _pressed = false; Paint(); }), true);
        PointerCaptureLost += (_, _) => { _pressed = false; Paint(); };
        IsEnabledChanged += (_, _) => Paint();
        GotFocus += (_, _) => Paint(); LostFocus += (_, _) => Paint();
        Unloaded += (_, _) => { _over = _pressed = false; Paint(); };
    }
    internal void Configure(DockPalette palette)
    {
        _palette = palette; Foreground = palette.Foreground; FontSize = palette.FontSize;
        if (Content is Path path) { path.Stroke = palette.Foreground; path.Fill = palette.Foreground; }
        Paint();
    }
    private void Paint()
    {
        Background = IsEnabled && _over ? _pressed ? _palette.Pressed : _palette.Hover : DockChrome.Transparent;
        BorderBrush = FocusState == FocusState.Keyboard ? _palette.Accent : null;
        Opacity = IsEnabled ? 1 : .45;
    }
}

// Rotates one live element and exchanges its measure/arrange axes. Unlike a render
// transform on a normal StackPanel this reserves the correct vertical rail length.
internal sealed class DockRotatedLabel : Panel
{
    private bool _vertical;
    private readonly CompositeTransform _rotation = new() { Rotation = 90 };
    internal DockRotatedLabel(UIElement child) => Children.Add(child);
    internal bool Vertical
    {
        get => _vertical;
        set { if (_vertical == value) return; _vertical = value; InvalidateMeasure(); }
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Children[0]; child.Measure(_vertical ? new(availableSize.Height, availableSize.Width) : availableSize);
        return _vertical ? new(child.DesiredSize.Height, child.DesiredSize.Width) : child.DesiredSize;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Children[0];
        if (_vertical)
        {
            child.Arrange(new(0, 0, finalSize.Height, finalSize.Width));
            _rotation.TranslateX = finalSize.Width;
            if (!ReferenceEquals(child.RenderTransform, _rotation)) child.RenderTransform = _rotation;
        }
        else { child.RenderTransform = null; child.Arrange(new(0, 0, finalSize.Width, finalSize.Height)); }
        return finalSize;
    }
}
