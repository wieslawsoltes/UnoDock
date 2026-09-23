using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Shell;
using Windows.Foundation;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

public static partial class ShellTests
{
    private static void RegisterNativeInput(TestRunner tests, DockingManager host)
    {
        if (!OperatingSystem.IsLinux()) return;
        foreach (var cancel in new[] { false, true }) tests.Test("X11 managed left resize, detach rollback=" + cancel, async () =>
        {
            var original = host.Layout; var mode = host.FloatingWindowMode;
            var content = new LayoutDocument { Title = "Managed chrome resize", Content = new Grid(), FloatingLeft = 50, FloatingTop = 90, FloatingWidth = 400, FloatingHeight = 320 };
            var display = XOpenDisplay(0); Check.True(display != 0, "Native input test requires an X11 display.");
            LayoutFloatingWindowControl? floating = null;
            try
            {
                host.FloatingWindowMode = FloatingWindowMode.InSurface;
                host.Layout = new() { RootPanel = new UnoDock.Layout.LayoutPanel(new LayoutDocumentPane(content)) };
                content.Float(); host.Refresh();
                await Until(() => host.FloatingWindows.Any(w => w.IsLoaded && w.ActualWidth > 0));
                floating = host.FloatingWindows.Single();
#if !WINDOWS
                // Previous lifecycle tests deliberately closed the active native
                // window. Restore focus before injecting a border gesture.
                Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, host.XamlRoot)).Activate();
#endif
                WindowChrome.SetWindowChrome(floating, new() { ResizeBorderThickness = new(12) }); await Tick();
                var converter = (IScreenWindowCoordinates)host.CrossWindowCoordinates!;
                var start = converter.ToScreen(floating, new(2, floating.ActualHeight / 2));
                var before = content.FloatingLeft;
                Motion(display, start); await Tick();
                Check.True(XTestFakeButtonEvent(display, 1, true, 0) != 0); XFlush(display); await Tick();
                Motion(display, new(start.X + 24, start.Y)); await Until(() => content.FloatingLeft > before + 1);
                Check.Near(before + 24, content.FloatingLeft, 1); Check.Near(376, content.FloatingWidth, 1);
                Check.Near(450, content.FloatingLeft + content.FloatingWidth, 1);
                if (cancel)
                {
                    WindowChrome.SetWindowChrome(floating, null); await Tick();
                    Check.Near(before, content.FloatingLeft); Check.Near(400, content.FloatingWidth);
                }
                Check.True(XTestFakeButtonEvent(display, 1, false, 0) != 0); XFlush(display); await Tick();
                Check.Near(cancel ? 400 : 376, content.FloatingWidth, 1);
                Check.True(ReferenceEquals(content.FindParent<LayoutFloatingWindow>(), floating.Model));
            }
            finally
            {
                if (display != 0) { XTestFakeButtonEvent(display, 1, false, 0); XFlush(display); XCloseDisplay(display); }
                if (floating != null) WindowChrome.SetWindowChrome(floating, null);
                host.FloatingWindowMode = mode; host.Layout = original; host.Refresh();
            }
        });
    }
    private static async Task Until(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Chrome test did not reach the expected state."); await Task.Delay(20); }
    }
    private static void Motion(nint display, Point point)
    {
        Check.True(XTestFakeMotionEvent(display, -1, checked((int)Math.Round(point.X)), checked((int)Math.Round(point.Y)), 0) != 0); XFlush(display);
    }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(nint name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XFlush(nint display);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeMotionEvent(nint display, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, [MarshalAs(UnmanagedType.Bool)] bool pressed, nuint delay);
}
