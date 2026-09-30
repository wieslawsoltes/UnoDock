namespace UnoDock.Core;
/// <summary>Placement policy for floating windows in top-left screen DIPs.</summary>
public static class FloatingPlacement
{
    /// <summary>Fit a window onto the monitor work area it overlaps most (or the
        /// nearest one when it is off every monitor): shrink it to the work area and
        /// move it so it is entirely visible. With no work areas the bounds are
        /// returned unchanged.</summary>
        public static DockRect Fit(DockRect window, IReadOnlyList<DockRect> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (!double.IsFinite(window.X + window.Y + window.Width + window.Height))
            throw new ArgumentOutOfRangeException(nameof(window));
        var area = Choose(window, workAreas);
        if (area is not { } target)
            return window;
        var width = Math.Min(Math.Max(0, window.Width), target.Width);
        var height = Math.Min(Math.Max(0, window.Height), target.Height);
        var x = Math.Clamp(window.X, target.X, target.Right - width);
        var y = Math.Clamp(window.Y, target.Y, target.Bottom - height);
        return new(x, y, width, height);
    }

    /// <summary>The work area overlapping <paramref name = "window"/> most, else the
        /// one whose nearest point is closest to the window's center.</summary>
        public static DockRect? Choose(DockRect window, IReadOnlyList<DockRect> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        DockRect? best = null;
        var bestOverlap = 0d;
        foreach (var area in workAreas)
        {
            if (!(area.Width > 0 && area.Height > 0) || !double.IsFinite(area.X + area.Y + area.Width + area.Height))
                continue;
            var overlap = Math.Max(0, Math.Min(window.Right, area.Right) - Math.Max(window.X, area.X)) * Math.Max(0, Math.Min(window.Bottom, area.Bottom) - Math.Max(window.Y, area.Y));
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = area;
            }
        }

        if (best != null)
            return best;
        var cx = window.X + window.Width / 2;
        var cy = window.Y + window.Height / 2;
        var bestDistance = double.PositiveInfinity;
        foreach (var area in workAreas)
        {
            if (!(area.Width > 0 && area.Height > 0) || !double.IsFinite(area.X + area.Y + area.Width + area.Height))
                continue;
            var dx = cx - Math.Clamp(cx, area.X, area.Right);
            var dy = cy - Math.Clamp(cy, area.Y, area.Bottom);
            var distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = area;
            }
        }

        return best;
    }

    /// <summary>Initial floating bounds for content leaving a docked pane: the
        /// window's client area covers the pane's former screen rectangle (so the
        /// content does not jump), limited to a share of the work area.</summary>
        public static DockRect FromPane(DockRect paneScreenBounds, double captionHeight, DockRect? workArea, double maxShare = .8, double minWidth = 160, double minHeight = 100)
    {
        if (!double.IsFinite(paneScreenBounds.X + paneScreenBounds.Y + paneScreenBounds.Width + paneScreenBounds.Height + captionHeight + maxShare) || maxShare <= 0 || maxShare > 1)
            throw new ArgumentOutOfRangeException(nameof(paneScreenBounds));
        var width = Math.Max(minWidth, paneScreenBounds.Width);
        var height = Math.Max(minHeight, paneScreenBounds.Height + Math.Max(0, captionHeight));
        if (workArea is { Width: > 0, Height: > 0 } area)
        {
            width = Math.Min(width, Math.Max(minWidth, area.Width * maxShare));
            height = Math.Min(height, Math.Max(minHeight, area.Height * maxShare));
        }

        return new(paneScreenBounds.X, paneScreenBounds.Y - Math.Max(0, captionHeight), width, height);
    }
}
