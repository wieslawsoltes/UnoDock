namespace UnoDock.Core;

/// <summary>A frame drag is calculated from its starting rectangle, never from already updated
/// coordinates. Opposite resize edges remain stationary when a size constraint is reached.</summary>
public static class ChromeResize
{
    public static DockRect Apply(DockRect start, ChromeHit hit, double dx, double dy,
        double minWidth, double minHeight, double maxWidth = double.PositiveInfinity,
        double maxHeight = double.PositiveInfinity)
    {
        if (!Enum.IsDefined(hit)) throw new ArgumentOutOfRangeException(nameof(hit));
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(start.Width) ||
            !double.IsFinite(start.Height) || !double.IsFinite(start.Right) || !double.IsFinite(start.Bottom) || start.Width < 0 || start.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (!double.IsFinite(dx) || !double.IsFinite(dy)) throw new ArgumentOutOfRangeException(nameof(dx));
        Validate(minWidth, maxWidth); Validate(minHeight, maxHeight);
        if (hit == ChromeHit.Client) return start;
        DockRect result;
        if (hit == ChromeHit.Caption) result = start with { X = start.X + dx, Y = start.Y + dy };
        else
        {
            var left = hit is ChromeHit.Left or ChromeHit.TopLeft or ChromeHit.BottomLeft;
            var right = hit is ChromeHit.Right or ChromeHit.TopRight or ChromeHit.BottomRight;
            var top = hit is ChromeHit.Top or ChromeHit.TopLeft or ChromeHit.TopRight;
            var bottom = hit is ChromeHit.Bottom or ChromeHit.BottomLeft or ChromeHit.BottomRight;
            var width = left || right ? Math.Clamp(start.Width + (left ? -dx : dx), minWidth, maxWidth) : start.Width;
            var height = top || bottom ? Math.Clamp(start.Height + (top ? -dy : dy), minHeight, maxHeight) : start.Height;
            result = new(left ? start.Right - width : start.X, top ? start.Bottom - height : start.Y, width, height);
        }
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y) || !double.IsFinite(result.Right) || !double.IsFinite(result.Bottom))
            throw new ArgumentOutOfRangeException(nameof(dx), "The drag would produce non-finite frame coordinates.");
        return result;
    }
    private static void Validate(double min, double max)
    {
        if (!double.IsFinite(min) || min < 0 || double.IsNaN(max) || max < min)
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum and maximum sizes must be ordered, nonnegative numbers.");
    }
}
