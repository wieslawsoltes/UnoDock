using UnoDock.Core;
using Windows.Foundation;

namespace UnoDock.Testing;

internal static partial class FloatingChromeTests
{
    private static void RegisterPhysical(TestRunner tests, bool tools)
    {
        var kind = tools ? "tools" : "document";
        foreach (var edge in Edges)
            tests.Test($"chrome/{kind}: physical pointer resizes {edge}", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                using var input = new PointerInput();
                f.Native.Activate();
                var before = FloatingChromeProbe.Bounds(f.Native);
                var grip = f.Grip(edge);
                var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
                input.MoveTo(grip, down);
                await Task.Delay(70);
                input.Press();
                await Task.Delay(70);
                Check.True(f.Control.IsResizing, "The native press did not capture a resize grip.");
                // The owner does not move; do not resolve the finish against a grip
                // which itself changes screen position as its frame is resized.
                var startInOwner = input.OwnerPoint(grip, down, f.Manager);
                input.MoveTo(f.Manager, new(startInOwner.X + 22, startInOwner.Y + 16));
                await Task.Delay(100);
                input.Release();
                await Wait(() => !f.Control.IsResizing);
                var scale = f.Control.XamlRoot!.RasterizationScale;
                Near(ChromeResize.Apply(before, edge, 22 * scale, 16 * scale, 0, 0), FloatingChromeProbe.Bounds(f.Native), 2);
                Check.False(f.Control.IsDragging);
                f.AssertEditors();
            });
        tests.Test($"chrome/{kind}: physical Escape cancels resize without a late-release commit", async () =>
        {
            using var f = new Fixture(tools);
            await f.Show();
            using var input = new PointerInput();
            f.Native.Activate();
            var before = FloatingChromeProbe.Bounds(f.Native);
            var grip = f.Grip(ChromeHit.BottomRight);
            var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
            var ownerPoint = input.OwnerPoint(grip, down, f.Manager);
            input.MoveTo(grip, down);
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            Check.True(f.Control.IsResizing);
            input.MoveTo(f.Manager, new(ownerPoint.X + 45, ownerPoint.Y + 30));
            await Task.Delay(100);
            input.EscapeDown();
            await Wait(() => !f.Control.IsResizing);
            input.EscapeUp();
            input.Release();
            await Task.Delay(100);
            Near(before, FloatingChromeProbe.Bounds(f.Native));
            f.AssertEditors();
        });
        // Never count an unavailable EWMH operation as a passing physical test.
        // Dedicated Openbox acceptance requires this case by name on Linux.
        if (OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("UNODOCK_REQUIRE_WM") == "1")
            tests.Test($"chrome/{kind}: physical custom maximize and restore buttons retain the native host", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                using var input = new PointerInput();
                var native = f.Native;
                var before = FloatingChromeProbe.Bounds(native);
                await input.Click(f.Button("Maximize"));
                await Wait(() => f.Control.IsMaximized);
                Check.True(f.Control.IsCustomTitleBar);
                await input.Click(f.Button("Maximize"));
                await Wait(() => !f.Control.IsMaximized);
                await Task.Delay(100);
                Near(before, FloatingChromeProbe.Bounds(native), 2);
                Check.Same(native, f.Native);
                f.AssertEditors();
            });
        tests.Test($"chrome/{kind}: physical custom close honors veto and then closes once", async () =>
        {
            using var f = new Fixture(tools);
            await f.Show();
            using var input = new PointerInput();
            var veto = true;
            var calls = 0;
            f.Control.Closing += (_, e) =>
            {
                calls++;
                e.Cancel = veto;
            };
            await input.Click(f.Button("Close"));
            await Wait(() => calls == 1);
            Check.True(f.Control.NativeWindow != null && f.Control.IsCustomTitleBar);
            f.AssertEditors();
            veto = false;
            await input.Click(f.Button("Close"));
            await Wait(() => f.Control.NativeWindow == null);
            Check.Equal(2, calls);
            Check.False(f.Control.IsResizing);
            Check.False(f.Control.IsDragging);
        });
    }
}
