using Xceed.Wpf.AvalonDock.Controls;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Xceed.Wpf.AvalonDock.Internal;

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

// Native WinUI seals Border. Both targets use the same composed visual tree so
// generic Uno runtime tests exercise the arrangement used by the native package.
internal abstract class DockGuideContainer : Panel
{
    protected Border Frame { get; } = new();
    protected DockGuideContainer() => Children.Add(Frame);
    protected override Size MeasureOverride(Size availableSize)
    {
        Frame.Measure(availableSize);
        return Frame.DesiredSize;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Frame.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}

internal sealed class DockGuideVisual : DockGuideContainer
{
    private readonly Border _pane, _selection, _title;
    private readonly Path _detail = new() { StrokeThickness = 1.1, Stretch = Stretch.None };
    internal DockGuideVisual(DropTargetType type, DockPosition position)
    {
        Name = "Guide_" + type; Frame.BorderThickness = new(1); Frame.CornerRadius = new(0); IsHitTestVisible = false;
        Canvas.SetZIndex(this, 2); DockVisuals.SetName(this, "Docking target: " + type);
        var drawing = new Canvas { Width = 30, Height = 30, IsHitTestVisible = false };
        _pane = new Border { Width = 22, Height = 22, BorderThickness = new(1.2), CornerRadius = new(.5) };
        Canvas.SetLeft(_pane, 4); Canvas.SetTop(_pane, 4); drawing.Children.Add(_pane);
        var r = DockSplitSolver.Preview(new(5.2, 5.2, 19.6, 19.6), position);
        _selection = new Border { Width = r.Width, Height = r.Height };
        Canvas.SetLeft(_selection, r.X); Canvas.SetTop(_selection, r.Y); drawing.Children.Add(_selection);
        _title = new Border { Width = 19.6, Height = 2.3 };
        Canvas.SetLeft(_title, 5.2); Canvas.SetTop(_title, 5.2); drawing.Children.Add(_title);
        var geometry = new PathGeometry();
        void Figure(bool filled, params Point[] points)
        {
            var f = new PathFigure { StartPoint = points[0], IsClosed = filled, IsFilled = filled };
            foreach (var point in points.Skip(1)) f.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(f);
        }
        var tool = type <= DropTargetType.DockingManagerDockBottom || type >= DropTargetType.DocumentPaneDockAsAnchorableLeft;
        if (tool)
        {
            switch (position)
            {
                case DockPosition.Left: Figure(true, new(17, 15), new(21, 11), new(21, 19)); break;
                case DockPosition.Right: Figure(true, new(13, 15), new(9, 11), new(9, 19)); break;
                case DockPosition.Top: Figure(true, new(15, 17), new(11, 21), new(19, 21)); break;
                case DockPosition.Bottom: Figure(true, new(15, 13), new(11, 9), new(19, 9)); break;
            }
        }
        else if (position == DockPosition.Inside)
        {
            // Two small tab corners distinguish "into group" from a side split.
            Figure(false, new(8, 9), new(8, 22), new(16, 22), new(16, 19), new(25, 19));
            Figure(false, new(16, 22), new(16, 26), new(20, 26), new(20, 22), new(25, 22));
        }
        else if (position is DockPosition.Left or DockPosition.Right)
            Figure(false, new(15, 8), new(15, 24));
        else Figure(false, new(6, 15), new(24, 15));
        _detail.Data = geometry; drawing.Children.Add(_detail);
        Frame.Child = new Viewbox { Child = drawing, Stretch = Stretch.Fill };
    }
    internal void Paint(DockGuidePalette p, bool selected, bool joined)
    {
        Frame.Background = selected ? p.Selection : joined ? DockChrome.Transparent : p.Background;
        Frame.BorderBrush = selected ? p.Ink : joined ? DockChrome.Transparent : p.Border;
        _pane.BorderBrush = p.Ink; _pane.Background = p.Window; _selection.Background = p.Fill;
        _title.Background = p.Title; _detail.Stroke = _detail.Fill = p.Ink;
    }
}

internal sealed class DockGuideBackplate : DockGuideContainer
{
    private readonly Path _shape;
    internal DockGuideBackplate()
    {
        Name = "DockGuideBackplate"; IsHitTestVisible = false; Canvas.SetZIndex(this, 1);
        var points = new Point[] { new(30, 0), new(60, 0), new(60, 25), new(65, 30), new(90, 30), new(90, 60),
            new(65, 60), new(60, 65), new(60, 90), new(30, 90), new(30, 65), new(25, 60), new(0, 60), new(0, 30), new(25, 30), new(30, 25) };
        var f = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        foreach (var point in points.Skip(1)) f.Segments.Add(new LineSegment { Point = point });
        _shape = new Path { Data = new PathGeometry { Figures = { f } }, StrokeThickness = .8, Stretch = Stretch.Fill };
        Frame.Child = _shape;
    }
    internal void Paint(DockGuidePalette p) { _shape.Fill = p.Background; _shape.Stroke = p.Border; }
}
