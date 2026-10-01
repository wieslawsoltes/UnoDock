namespace UnoDock.Core;
/// <summary>One desktop DIP space over monitors with different scales. Each
/// monitor's DIP rectangle is anchored at its physical origin and sized by its
/// own scale, so points and bounds round-trip exactly on every monitor, a single
/// monitor at the desktop origin maps as <c>physical / scale</c>, and monitors
/// with scales of at least 1 never overlap in DIPs.</summary>
public static class DesktopDipSpace
{
    /// <summary>The DIP rectangle a monitor occupies.</summary>
    public static DockRect Bounds(DesktopMonitor monitor) => ToDip(monitor, monitor.Bounds);
    /// <summary>A monitor's work area in DIPs.</summary>
    public static DockRect WorkArea(DesktopMonitor monitor) => ToDip(monitor, monitor.WorkArea);
    /// <summary>Converts a physical point through the monitor that contains it (or
        /// the nearest one). Without monitors the fallback scale divides the point.</summary>
        public static DockPoint ToDip(DockPoint physical, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (Nearest(physical, monitors, m => m.Bounds) is not { } monitor)
            return new(physical.X / Valid(fallbackScale), physical.Y / Valid(fallbackScale));
        var scale = Valid(monitor.Scale);
        return new(monitor.Bounds.X + (physical.X - monitor.Bounds.X) / scale, monitor.Bounds.Y + (physical.Y - monitor.Bounds.Y) / scale);
    }

    /// <summary>Converts a DIP point through the monitor whose DIP rectangle
        /// contains it (or the nearest one).</summary>
        public static DockPoint ToPhysical(DockPoint dip, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (Nearest(dip, monitors, Bounds) is not { } monitor)
            return new(dip.X * Valid(fallbackScale), dip.Y * Valid(fallbackScale));
        var scale = Valid(monitor.Scale);
        return new(monitor.Bounds.X + (dip.X - monitor.Bounds.X) * scale, monitor.Bounds.Y + (dip.Y - monitor.Bounds.Y) * scale);
    }

    /// <summary>The scale of the monitor a DIP point belongs to.</summary>
    public static double ScaleAtDip(DockPoint dip, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        return Nearest(dip, monitors, Bounds) is { } monitor ? Valid(monitor.Scale) : Valid(fallbackScale);
    }

    /// <summary>The scale of the monitor a physical point belongs to.</summary>
    public static double ScaleAtPhysical(DockPoint physical, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        return Nearest(physical, monitors, m => m.Bounds) is { } monitor ? Valid(monitor.Scale) : Valid(fallbackScale);
    }

    /// <summary>Physical window bounds in DIPs: the origin through its monitor and
        /// the size by that monitor's scale.</summary>
        public static DockRect ToDip(DockRect physical, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        var origin = ToDip(new DockPoint(physical.X, physical.Y), monitors, fallbackScale);
        var scale = ScaleAtPhysical(new(physical.X, physical.Y), monitors, fallbackScale);
        return new(origin.X, origin.Y, physical.Width / scale, physical.Height / scale);
    }

    /// <summary>DIP window bounds in physical pixels, sized by the scale of the
        /// monitor that receives the window's origin.</summary>
        public static DockRect ToPhysical(DockRect dip, IReadOnlyList<DesktopMonitor> monitors, double fallbackScale)
    {
        var origin = ToPhysical(new DockPoint(dip.X, dip.Y), monitors, fallbackScale);
        var scale = ScaleAtDip(new(dip.X, dip.Y), monitors, fallbackScale);
        return new(origin.X, origin.Y, dip.Width * scale, dip.Height * scale);
    }

    private static DockRect ToDip(DesktopMonitor monitor, DockRect rect)
    {
        var scale = Valid(monitor.Scale);
        return new(monitor.Bounds.X + (rect.X - monitor.Bounds.X) / scale, monitor.Bounds.Y + (rect.Y - monitor.Bounds.Y) / scale, rect.Width / scale, rect.Height / scale);
    }

    private static DesktopMonitor? Nearest(DockPoint point, IReadOnlyList<DesktopMonitor> monitors, Func<DesktopMonitor, DockRect> area)
    {
        DesktopMonitor? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var monitor in monitors)
        {
            var rect = area(monitor);
            if (!(rect.Width > 0 && rect.Height > 0))
                continue;
            // Half-open containment: a shared edge belongs to the monitor after it.
            var dx = point.X < rect.X ? rect.X - point.X : point.X >= rect.Right ? point.X - rect.Right + 1e-9 : 0;
            var dy = point.Y < rect.Y ? rect.Y - point.Y : point.Y >= rect.Bottom ? point.Y - rect.Bottom + 1e-9 : 0;
            var distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monitor;
            }
        }

        return best;
    }

    private static double Valid(double scale) => double.IsFinite(scale) && scale > 0 ? scale : 1;
}
