namespace Xceed.Wpf.AvalonDock.Internal;

internal static class DockCoordinates
{
    internal static Point Translate(FrameworkElement source, Point point, FrameworkElement destination, ICrossWindowCoordinates? coordinates)
    {
        if (source.XamlRoot == null || destination.XamlRoot == null) throw new InvalidOperationException("Visual root is detached.");
        var result = ReferenceEquals(source.XamlRoot, destination.XamlRoot)
            ? source.TransformToVisual(destination).TransformPoint(point)
            : (coordinates ?? throw new PlatformNotSupportedException("No cross-window coordinate provider.")).Translate(source, point, destination);
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y)) throw new InvalidOperationException("Coordinate provider returned a non-finite point.");
        return result;
    }
    internal static Rect Bounds(FrameworkElement source, Rect bounds, FrameworkElement destination, ICrossWindowCoordinates? coordinates)
    {
        // Two diagonal points are insufficient with rotation, skew, or reflection.
        Func<Point, Point> translate = coordinates is DesktopWindowCoordinates desktop ? desktop.CreateTranslator(source, destination)
            : point => Translate(source, point, destination, coordinates);
        var a = translate(new(bounds.Left, bounds.Top)); var b = translate(new(bounds.Right, bounds.Top));
        var c = translate(new(bounds.Left, bounds.Bottom)); var d = translate(new(bounds.Right, bounds.Bottom));
        if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y) ||
            !double.IsFinite(c.X) || !double.IsFinite(c.Y) || !double.IsFinite(d.X) || !double.IsFinite(d.Y))
            throw new InvalidOperationException("Coordinate provider returned non-finite bounds.");
        var x = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)); var y = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        var right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)); var bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        if (!double.IsFinite(right - x) || !double.IsFinite(bottom - y)) throw new InvalidOperationException("Coordinate bounds overflow.");
        return new(x, y, right - x, bottom - y);
    }
    internal static bool IsUnavailable(Exception e) => e is InvalidOperationException or ArgumentException or NotSupportedException;
}
