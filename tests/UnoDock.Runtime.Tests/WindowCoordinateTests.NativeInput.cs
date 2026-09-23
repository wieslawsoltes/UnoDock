using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

public static partial class WindowCoordinateTests
{
    private static void RegisterNativeInput(TestRunner tests, DockingManager host)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1") return;
        tests.Test("X11 pointer drag docks between native windows with visible preview", async () =>
        {
            var original = host.Layout; var mode = host.FloatingWindowMode;
            var content = new LayoutAnchorable { Title = "Pointer source", Content = new TextBox { Text = "Pointer-retained editor" }, FloatingLeft = 50, FloatingTop = 90, FloatingWidth = 400, FloatingHeight = 320 };
            var target = new LayoutAnchorable { Title = "Pointer target", Content = new TextBox(), FloatingLeft = 650, FloatingTop = 90, FloatingWidth = 400, FloatingHeight = 320 };
            var display = XOpenDisplay(IntPtr.Zero);
            Check.True(display != IntPtr.Zero, "X11 input test requires DISPLAY.");
            try
            {
                host.FloatingWindowMode = FloatingWindowMode.Native;
                host.Layout = new();
                content.AddToLayout(host, AnchorableShowStrategy.Left); target.AddToLayout(host, AnchorableShowStrategy.Right);
                content.Float(); target.Float(); host.Refresh();
                await Until(() => host.FloatingWindows.Count() == 2 && host.FloatingWindows.All(w => w.NativeWindow != null && w.IsLoaded));
                await Task.Delay(80);
                var sourceHost = host.FloatingWindows.Single(w => ReferenceEquals(w.Model, content.FindParent<LayoutFloatingWindow>()));
                var targetHost = host.FloatingWindows.Single(w => ReferenceEquals(w.Model, target.FindParent<LayoutFloatingWindow>()));
                // A single-tool pane has no tab strip. Drag its real caption instead.
                var button = Visuals(sourceHost).OfType<ContentPresenter>().Single(p => p.Name == "PART_ToolCaption");
                sourceHost.NativeWindow!.Activate(); await Task.Delay(60);
                var area = host.GetDropAreas().OfType<DropArea<FrameworkElement>>().Single(a => a.AreaElement is ILayoutControl c && ReferenceEquals(c.Model, target.Parent));
                var converter = (IScreenWindowCoordinates)host.CrossWindowCoordinates!;
                var start = converter.ToScreen(button, new(button.ActualWidth / 2, button.ActualHeight / 2));
                var end = converter.ToScreen(area.AreaElement, new(area.AreaElement.ActualWidth / 2, area.AreaElement.ActualHeight / 2));
                var item = host.GetLayoutItemFromModel(content); var retained = item.View;
                Motion(display, start); await Task.Delay(40);
                Check.True(XTestFakeButtonEvent(display, 1, true, UIntPtr.Zero) != 0); XFlush(display);
                await Task.Delay(40);
                Motion(display, new(start.X + 20, start.Y + 8)); await Task.Delay(40);
                Motion(display, end); await Task.Delay(100);
                Check.True(Visuals(targetHost).OfType<OverlayWindow>().Any(w => w.IsOpen), "Target native window must paint the accepted drop preview.");
                Check.True(XTestFakeButtonEvent(display, 1, false, UIntPtr.Zero) != 0); XFlush(display);
                await Until(() => ReferenceEquals(content.Parent, target.Parent));
                host.Refresh();
                Check.Same(retained, host.GetLayoutItemFromModel(content).View);
                Check.Equal("Pointer-retained editor", ((TextBox)content.Content!).Text);
                Check.Equal(1, host.Layout.FloatingWindows.Count);
                Check.False(Visuals(targetHost).OfType<OverlayWindow>().Any(w => w.IsOpen));
            }
            finally
            {
                if (display != IntPtr.Zero) { XTestFakeButtonEvent(display, 1, false, UIntPtr.Zero); XFlush(display); XCloseDisplay(display); }
                host.FloatingWindowMode = mode; host.Layout = original; host.Refresh();
            }
        });
    }
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Motion(IntPtr display, Point point)
    {
        Check.True(XTestFakeMotionEvent(display, -1, checked((int)Math.Round(point.X)), checked((int)Math.Round(point.Y)), UIntPtr.Zero) != 0);
        XFlush(display);
    }
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeMotionEvent(IntPtr display, int screen, int x, int y, UIntPtr delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool pressed, UIntPtr delay);
}
