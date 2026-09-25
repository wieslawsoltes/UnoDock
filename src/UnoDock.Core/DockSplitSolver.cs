namespace UnoDock.Core;

/// <summary>Constrained water-filling; deterministic and bounded, with no dependency on visual trees.</summary>
public static class DockSplitSolver
{
    public static double[] Allocate(double available, double separator, ReadOnlySpan<DockMeasure> items)
    {
        if (!double.IsFinite(available) || available < 0 || !double.IsFinite(separator) || separator < 0)
            throw new ArgumentOutOfRangeException(nameof(available));
        var count = items.Length;
        if (count == 0) return [];
        var space = Math.Max(0, available - separator * (count - 1));
        var result = new double[count];
        double fixedTotal = 0, minimumTotal = 0, weights = 0;
        var active = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var item = items[i];
            if (!double.IsFinite(item.Value) || !double.IsFinite(item.Minimum) || !double.IsFinite(item.Desired) || item.Value < 0 || item.Minimum < 0 || item.Desired < 0 || !Enum.IsDefined(item.Unit))
                throw new ArgumentOutOfRangeException(nameof(items));
            minimumTotal += item.Minimum;
            active[i] = item.Unit == DockLengthUnit.Star;
            if (active[i]) weights += Math.Max(item.Value, 1e-9);
            else fixedTotal += result[i] = Math.Max(item.Minimum, item.Unit == DockLengthUnit.Auto ? item.Desired : item.Value);
        }
        if (!double.IsFinite(fixedTotal + minimumTotal + weights)) throw new ArgumentOutOfRangeException(nameof(items), "Aggregate layout dimensions overflow.");
        // Under impossible constraints, scale all minima proportionally instead of producing negatives.
        if (minimumTotal >= space)
        {
            for (var i = 0; i < count; i++) result[i] = minimumTotal > 0 ? space * (items[i].Minimum / minimumTotal) : space / count;
            return result;
        }
        var starMinimum = items.ToArray().Where((_, i) => active[i]).Sum(x => x.Minimum);
        var remaining = Math.Max(0, space - fixedTotal);
        if (fixedTotal + starMinimum > space)
        {
            var discretionary = fixedTotal - items.ToArray().Where((_, i) => !active[i]).Sum(x => x.Minimum);
            var budget = space - minimumTotal;
            for (var i = 0; i < count; i++)
                result[i] = items[i].Minimum + (!active[i] && discretionary > 0 ? (result[i] - items[i].Minimum) * budget / discretionary : 0);
            return result;
        }
        for (var pass = 0; pass < count && weights > 0; pass++)
        {
            var clamped = false;
            for (var i = 0; i < count; i++)
            {
                if (!active[i]) continue;
                var size = remaining * (Math.Max(items[i].Value, 1e-9) / weights);
                if (size + 1e-9 >= items[i].Minimum) continue;
                result[i] = items[i].Minimum;
                remaining -= result[i]; weights -= Math.Max(items[i].Value, 1e-9);
                active[i] = false; clamped = true;
            }
            if (!clamped) break;
        }
        for (var i = 0; i < count; i++) if (active[i]) result[i] = Math.Max(0, remaining * (Math.Max(items[i].Value, 1e-9) / weights));
        // Pixel/auto-only groups may intentionally leave unoccupied space.
        return result;
    }

    public static (double Before, double After) ResizePair(double before, double after, double delta, double minimumBefore, double minimumAfter)
    {
        if (!double.IsFinite(before + after + delta + minimumBefore + minimumAfter) || before < 0 || after < 0 || minimumBefore < 0 || minimumAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(delta));
        var total = before + after;
        if (minimumBefore + minimumAfter > total)
        {
            var ratio = minimumBefore / (minimumBefore + minimumAfter);
            return (total * ratio, total * (1 - ratio));
        }
        var first = Math.Clamp(before + delta, minimumBefore, total - minimumAfter);
        return (first, total - first);
    }

    public static DockPosition HitTest(DockRect area, DockPoint point, double edgeRatio = .25)
    {
        if (!double.IsFinite(edgeRatio) || edgeRatio < 0 || edgeRatio > .5)
            throw new ArgumentOutOfRangeException(nameof(edgeRatio));
        if (!area.Contains(point) || area.Width == 0 || area.Height == 0) return DockPosition.Inside;
        var x = (point.X - area.X) / area.Width;
        var y = (point.Y - area.Y) / area.Height;
        var edge = Math.Min(Math.Min(x, 1 - x), Math.Min(y, 1 - y));
        if (edge > edgeRatio) return DockPosition.Inside;
        // Corner ties are resolved consistently, independently of target enumeration order.
        if (edge == x) return DockPosition.Left;
        if (edge == 1 - x) return DockPosition.Right;
        return edge == y ? DockPosition.Top : DockPosition.Bottom;
    }
    public static DockRect Preview(DockRect area, DockPosition position) => position switch
    {
        DockPosition.Left => area with { Width = area.Width / 2 },
        DockPosition.Right => new(area.X + area.Width / 2, area.Y, area.Width / 2, area.Height),
        DockPosition.Top => area with { Height = area.Height / 2 },
        DockPosition.Bottom => new(area.X, area.Y + area.Height / 2, area.Width, area.Height / 2),
        DockPosition.Inside => area,
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };
}
