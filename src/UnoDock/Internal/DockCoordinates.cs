namespace UnoDock.Internal;
internal static class DockCoordinates
{
    internal static Point Translate(FrameworkElement source, Point point, FrameworkElement destination, ICrossWindowCoordinates? coordinates)
    {
        if (source.XamlRoot == null || destination.XamlRoot == null)
            throw new InvalidOperationException("Visual root is detached.");
        var result = ReferenceEquals(source.XamlRoot, destination.XamlRoot) ? source.TransformToVisual(destination).TransformPoint(point) : (coordinates ?? throw new PlatformNotSupportedException("No cross-window coordinate provider.")).Translate(source, point, destination);
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y))
            throw new InvalidOperationException("Coordinate provider returned a non-finite point.");
        return result;
    }

    internal static Rect Bounds(FrameworkElement source, Rect bounds, FrameworkElement destination, ICrossWindowCoordinates? coordinates)
    {
        if (source.XamlRoot == null || destination.XamlRoot == null)
            throw new InvalidOperationException("Visual root is detached.");
        // Capture one transform/host snapshot for the whole rectangle. Two diagonal
        // points are insufficient with rotation, skew or reflection.
        Func<Point, Point> translate;
        if (ReferenceEquals(source.XamlRoot, destination.XamlRoot))
        {
            var transform = source.TransformToVisual(destination);
            translate = transform.TransformPoint;
        }
        else
        {
            translate = coordinates is DesktopWindowCoordinates desktop ? desktop.CreateTranslator(source, destination) : point => Translate(source, point, destination, coordinates);
        }

        return Enclose(translate(new(bounds.Left, bounds.Top)), translate(new(bounds.Right, bounds.Top)), translate(new(bounds.Left, bounds.Bottom)), translate(new(bounds.Right, bounds.Bottom)));
    }

    internal static Rect Enclose(Point a, Point b, Point c, Point d)
    {
        if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(c.X) || !double.IsFinite(c.Y) || !double.IsFinite(d.X) || !double.IsFinite(d.Y))
            throw new InvalidOperationException("Coordinate provider returned non-finite bounds.");
        var x = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        var y = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        var right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        var bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        if (!double.IsFinite(right - x) || !double.IsFinite(bottom - y))
            throw new InvalidOperationException("Coordinate bounds overflow.");
        var result = new Rect(x, y, right - x, bottom - y);
        if (ContainsExtents(result, x, y, right, bottom))
            return result;
        // Rect exposes doubles but its WinRT storage is Single. Rounding origin and
        // width independently can exclude a transformed corner by a few ULPs. Round
        // the endpoints outwards, then round the required span upwards. This is not
        // a pixel/tolerance inflation: representable enclosing rectangles stay exact.
        var leftEdge = RoundDown(x);
        var topEdge = RoundDown(y);
        var rightEdge = RoundUp(right);
        var bottomEdge = RoundUp(bottom);
        result = new Rect(leftEdge, topEdge, RoundUp((double)rightEdge - leftEdge), RoundUp((double)bottomEdge - topEdge));
        if (!ContainsExtents(result, x, y, right, bottom))
            throw new InvalidOperationException("Coordinate bounds cannot be represented by a finite WinRT rectangle.");
        return result;
    }

    private static bool ContainsExtents(Rect value, double left, double top, double right, double bottom) => double.IsFinite(value.Left) && double.IsFinite(value.Top) && double.IsFinite(value.Right) && double.IsFinite(value.Bottom) && double.IsFinite(value.Width) && double.IsFinite(value.Height) && value.Left <= left && value.Top <= top && value.Right >= right && value.Bottom >= bottom;
    private static float RoundDown(double value)
    {
        var result = (float)value;
        if (result > value)
            result = MathF.BitDecrement(result);
        if (!float.IsFinite(result))
            throw new InvalidOperationException("Coordinate bounds exceed WinRT range.");
        return result;
    }

    private static float RoundUp(double value)
    {
        var result = (float)value;
        if (result < value)
            result = MathF.BitIncrement(result);
        if (!float.IsFinite(result))
            throw new InvalidOperationException("Coordinate bounds exceed WinRT range.");
        return result;
    }

    internal static bool IsUnavailable(Exception e) => e is InvalidOperationException or ArgumentException or NotSupportedException;
}
