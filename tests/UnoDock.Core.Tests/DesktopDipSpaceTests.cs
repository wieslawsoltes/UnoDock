using UnoDock.Core;
using UnoDock.Testing;

internal static class DesktopDipSpaceTests
{
    internal static void Register(TestRunner tests)
    {
        // Primary 100% at the origin, a 150% monitor to the right, a 125% monitor
        // to the left and a 200% monitor below the primary.
        DesktopMonitor[] monitors = [new(new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1), new(new(1920, -200, 2880, 1620), new(1920, -200, 2880, 1560), 1.5), new(new(-2400, 0, 2400, 1350), new(-2400, 0, 2400, 1300), 1.25), new(new(0, 1080, 3840, 2160), new(0, 1080, 3840, 2100), 2)];
        tests.Test("dip space: a single monitor at the origin maps physical / scale", () =>
        {
            DesktopMonitor[] single = [new(new(0, 0, 3840, 2160), new(0, 0, 3840, 2100), 2)];
            Check.Equal(new DockPoint(640, 360), DesktopDipSpace.ToDip(new DockPoint(1280, 720), single, 1));
            Check.Equal(new DockRect(100, 50, 400, 300), DesktopDipSpace.ToDip(new DockRect(200, 100, 800, 600), single, 1));
        });
        tests.Test("dip space: points round-trip on every monitor", () =>
        {
            foreach (var monitor in monitors)
                foreach (var (fx, fy) in new[]
                {
                    (0.0, 0.0),
                    (0.25, 0.5),
                    (0.9, 0.9)
                }

                )
                {
                    var physical = new DockPoint(monitor.Bounds.X + monitor.Bounds.Width * fx, monitor.Bounds.Y + monitor.Bounds.Height * fy);
                    var dip = DesktopDipSpace.ToDip(physical, monitors, 1);
                    var back = DesktopDipSpace.ToPhysical(dip, monitors, 1);
                    Check.Near(physical.X, back.X, 1e-6);
                    Check.Near(physical.Y, back.Y, 1e-6);
                }
        });
        tests.Test("dip space: window bounds keep their monitor and physical size", () =>
        {
            var physical = new DockRect(2400, 100, 900, 600);
            var dip = DesktopDipSpace.ToDip(physical, monitors, 1);
            Check.Equal(new DockRect(1920 + 480 / 1.5, -200 + 300 / 1.5, 600, 400), dip);
            Check.Equal(physical, DesktopDipSpace.ToPhysical(dip, monitors, 1));
        });
        tests.Test("dip space: monitors with scales of at least one never overlap", () =>
        {
            var rects = monitors.Select(DesktopDipSpace.Bounds).ToArray();
            for (var i = 0; i < rects.Length; i++)
                for (var j = i + 1; j < rects.Length; j++)
                {
                    var a = rects[i];
                    var b = rects[j];
                    Check.True(!(a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom), $"{a} overlaps {b}");
                }
        });
        tests.Test("dip space: work areas use their monitor's scale", () =>
        {
            Check.Equal(new DockRect(1920, -200, 1920, 1040), DesktopDipSpace.WorkArea(monitors[1]));
            Check.Equal(new DockRect(0, 1080, 1920, 1050), DesktopDipSpace.WorkArea(monitors[3]));
        });
        tests.Test("dip space: off-monitor points use the nearest monitor; no monitors use the fallback", () =>
        {
            Check.Equal(1.5, DesktopDipSpace.ScaleAtPhysical(new DockPoint(9000, 0), monitors, 1));
            Check.Equal(new DockPoint(50, 25), DesktopDipSpace.ToDip(new DockPoint(100, 50), [], 2));
            Check.Equal(new DockPoint(200, 100), DesktopDipSpace.ToPhysical(new DockPoint(100, 50), [], 2));
        });
    }
}
