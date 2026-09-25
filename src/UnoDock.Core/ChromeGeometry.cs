namespace UnoDock.Core;
/// <summary>Pure geometry for custom window frames. Inputs are DIPs, never guessed frame offsets.</summary>
public static class ChromeGeometry
{
    public static ChromeHit HitTest(DockRect bounds, DockPoint point, double captionHeight, double left, double top, double right, double bottom, bool canResize, bool maximized, IReadOnlyList<DockRect>? interactive = null)
    {
        Validate(bounds, captionHeight);
        foreach (var value in new[]
        {
            left,
            top,
            right,
            bottom
        }

        )
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(left));
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        if (interactive != null)
            foreach (var rect in interactive)
                Validate(rect, 0);
        if (!Inside(bounds, point))
            return ChromeHit.Client;
        if (interactive != null && interactive.Any(r => Inside(r, point)))
            return ChromeHit.Client;
        if (canResize && !maximized)
        {
            // On undersized windows each opposing border receives at most half the dimension.
            var l = point.X < bounds.X + Math.Min(left, bounds.Width / 2);
            var r = point.X >= bounds.Right - Math.Min(right, bounds.Width / 2);
            var t = point.Y < bounds.Y + Math.Min(top, bounds.Height / 2);
            var b = point.Y >= bounds.Bottom - Math.Min(bottom, bounds.Height / 2);
            if (t && l)
                return ChromeHit.TopLeft;
            if (t && r)
                return ChromeHit.TopRight;
            if (b && l)
                return ChromeHit.BottomLeft;
            if (b && r)
                return ChromeHit.BottomRight;
            if (l)
                return ChromeHit.Left;
            if (r)
                return ChromeHit.Right;
            if (t)
                return ChromeHit.Top;
            if (b)
                return ChromeHit.Bottom;
        }

        return point.Y < bounds.Y + Math.Min(captionHeight, bounds.Height) ? ChromeHit.Caption : ChromeHit.Client;
    }

    /// <summary>Disjoint caption rectangles excluding interactive content. A sweep over vertical
    /// boundaries merges overlapping exclusion intervals, then coalesces equal adjacent bands.</summary>
    public static IReadOnlyList<DockRect> CaptionRegions(DockRect bounds, double captionHeight, IReadOnlyList<DockRect> interactive)
    {
        Validate(bounds, captionHeight);
        ArgumentNullException.ThrowIfNull(interactive);
        var height = Math.Min(captionHeight, bounds.Height);
        if (height == 0 || bounds.Width == 0)
            return Array.Empty<DockRect>();
        var caption = bounds with
        {
            Height = height
        };
        var holes = new List<DockRect>();
        var boundaries = new SortedSet<double>
        {
            caption.Y,
            caption.Bottom
        };
        foreach (var rect in interactive)
        {
            Validate(rect, 0);
            var x = Math.Max(rect.X, caption.X);
            var y = Math.Max(rect.Y, caption.Y);
            var right = Math.Min(rect.Right, caption.Right);
            var bottom = Math.Min(rect.Bottom, caption.Bottom);
            if (x >= right || y >= bottom)
                continue;
            holes.Add(new(x, y, right - x, bottom - y));
            boundaries.Add(y);
            boundaries.Add(bottom);
        }

        var ys = boundaries.ToArray();
        var result = new List<DockRect>();
        var previous = new Dictionary<(double X, double Width), int>();
        for (var i = 0; i < ys.Length - 1; i++)
        {
            var y = ys[i];
            var next = ys[i + 1];
            var cursor = caption.X;
            var current = new Dictionary<(double X, double Width), int>();
            foreach (var hole in holes.Where(h => h.Y < next && h.Bottom > y).OrderBy(h => h.X).ThenBy(h => h.Right))
            {
                if (hole.X > cursor)
                    Add(cursor, hole.X - cursor);
                cursor = Math.Max(cursor, hole.Right);
            }

            if (cursor < caption.Right)
                Add(cursor, caption.Right - cursor);
            previous = current;
            void Add(double x, double width)
            {
                var key = (x, width);
                if (previous.TryGetValue(key, out var index) && result[index].Bottom == y)
                    result[index] = result[index] with
                    {
                        Height = next - result[index].Y
                    };
                else
                {
                    index = result.Count;
                    result.Add(new(x, y, width, next - y));
                }

                current.Add(key, index);
            }
        }

        return result;
    }

    private static bool Inside(DockRect r, DockPoint p) => p.X >= r.X && p.Y >= r.Y && p.X < r.Right && p.Y < r.Bottom;
    private static void Validate(DockRect r, double caption)
    {
        if (!double.IsFinite(r.X) || !double.IsFinite(r.Y) || !double.IsFinite(r.Right) || !double.IsFinite(r.Bottom) || r.Width < 0 || r.Height < 0 || !double.IsFinite(caption) || caption < 0)
            throw new ArgumentOutOfRangeException(nameof(r));
    }
}
