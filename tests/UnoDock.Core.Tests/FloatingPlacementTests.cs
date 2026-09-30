using UnoDock.Core;
using UnoDock.Testing;

internal static class FloatingPlacementTests
{
    internal static void Register(TestRunner tests)
    {
        DockRect[] monitors = [new(0, 0, 1920, 1040), new(1920, -200, 1280, 984)];
        tests.Test("placement: a visible window is unchanged", () => Check.Equal(new DockRect(100, 100, 640, 480), FloatingPlacement.Fit(new(100, 100, 640, 480), monitors)));
        tests.Test("placement: a window hanging off the edge moves inside", () => Check.Equal(new DockRect(1280, 560, 640, 480), FloatingPlacement.Fit(new(1500, 900, 640, 480), [monitors[0]])));
        tests.Test("placement: the monitor with the largest overlap wins", () =>
        {
            var fitted = FloatingPlacement.Fit(new(1800, 0, 400, 300), monitors);
            Check.Equal(1920, fitted.X);
            Check.Equal(0, fitted.Y);
        });
        tests.Test("placement: an off-screen window goes to the nearest monitor", () =>
        {
            var fitted = FloatingPlacement.Fit(new(5000, 100, 400, 300), monitors);
            Check.Equal(new DockRect(2800, 100, 400, 300), fitted);
            Check.Equal(new DockRect(0, 0, 400, 300), FloatingPlacement.Fit(new(-3000, -3000, 400, 300), monitors));
        });
        tests.Test("placement: an oversized window shrinks to the work area", () => Check.Equal(new DockRect(0, 0, 1920, 1040), FloatingPlacement.Fit(new(-50, -50, 4000, 3000), [monitors[0]])));
        tests.Test("placement: no work areas leaves bounds untouched", () => Check.Equal(new DockRect(-9, -9, 10, 10), FloatingPlacement.Fit(new(-9, -9, 10, 10), [])));
        tests.Test("placement: degenerate work areas are ignored", () => Check.Equal(new DockRect(0, 0, 100, 100), FloatingPlacement.Fit(new(-10, -10, 100, 100), [new(5, 5, 0, 0), monitors[0]])));
        tests.Test("placement: a floated pane keeps its client area in place", () =>
        {
            var bounds = FloatingPlacement.FromPane(new(300, 200, 250, 400), 24, new DockRect(0, 0, 1920, 1040));
            Check.Equal(new DockRect(300, 176, 250, 424), bounds);
        });
        tests.Test("placement: a floated document pane is limited to a share of the work area", () =>
        {
            var bounds = FloatingPlacement.FromPane(new(0, 0, 1900, 1000), 24, new DockRect(0, 0, 1920, 1040));
            Check.Equal(1536, bounds.Width);
            Check.Equal(832, bounds.Height);
            Check.Equal(160, FloatingPlacement.FromPane(new(0, 0, 10, 10), 0, null).Width);
        });
    }
}
