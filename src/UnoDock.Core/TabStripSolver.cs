namespace UnoDock.Core;
/// <summary>Natural-width tabs compressed by a shared upper bound. Small tabs keep their
/// desired size; only wider tabs shrink. O(n log n) time, deterministic input ordering.</summary>
public static class TabStripSolver
{
    public static double[] Allocate(double available, ReadOnlySpan<double> desired)
    {
        if (double.IsNaN(available) || available < 0)
            throw new ArgumentOutOfRangeException(nameof(available));
        var result = desired.ToArray();
        double sum = 0;
        foreach (var width in result)
        {
            if (!double.IsFinite(width) || width < 0)
                throw new ArgumentOutOfRangeException(nameof(desired));
            sum += width;
        }

        if (!double.IsFinite(sum))
            throw new ArgumentOutOfRangeException(nameof(desired));
        if (sum <= available || result.Length == 0)
            return result;
        var sorted = (double[])result.Clone();
        Array.Sort(sorted);
        var remaining = available;
        var cap = 0d;
        for (var i = 0; i < sorted.Length; i++)
        {
            cap = remaining / (sorted.Length - i);
            if (sorted[i] >= cap)
                break;
            remaining -= sorted[i];
        }

        for (var i = 0; i < result.Length; i++)
            result[i] = Math.Min(result[i], cap);
        return result;
    }

    /// <summary>Returns the boundary before/after tabs, including the trailing boundary.
    /// The origin is the logical leading edge; RTL callers transform their local x first.</summary>
    public static int InsertionIndex(double position, ReadOnlySpan<double> widths)
    {
        if (!double.IsFinite(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        double total = 0;
        foreach (var width in widths)
        {
            if (!double.IsFinite(width) || width < 0)
                throw new ArgumentOutOfRangeException(nameof(widths));
            total += width;
        }

        if (!double.IsFinite(total))
            throw new ArgumentOutOfRangeException(nameof(widths));
        double x = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            var width = widths[i];
            if (!double.IsFinite(width) || width < 0)
                throw new ArgumentOutOfRangeException(nameof(widths));
            if (position < x + width / 2)
                return i;
            x += width;
        }

        return widths.Length;
    }
}
