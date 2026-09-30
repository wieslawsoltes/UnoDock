using System.Reflection;
using Microsoft.Windows.Shell;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Floating window sizing and placement, application window placement
/// helpers and drop-down menu positioning on the desktop hosts.</summary>
internal static class WindowPlacementTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            tests.Test("placement: work areas are reported and SetWindowBounds places a window in DIPs", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var areas = DesktopWindowCoordinates.GetWorkAreas(f.Manager);
                Check.True(areas.Count >= 1, "No monitor work area was reported.");
                var area = areas[0];
                var target = new Rect(area.X + 60, area.Y + 50, 700, 480);
                DesktopWindowCoordinates.SetWindowBounds(f.Window, target, f.Manager.XamlRoot!.RasterizationScale);
                await Wait(() => Near(f.ClientOrigin(), new(target.X, target.Y), 48) && Math.Abs(f.Manager.ActualWidth - 700) <= 2);
            });
            tests.Test("placement: floating a docked tool opens its window over the pane", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var pane = f.PaneView(f.Tool);
                var paneOrigin = f.Coordinates.ToDesktopPoint(pane, default);
                var size = new Size(pane.ActualWidth, pane.ActualHeight);
                f.Tool.Float();
                var control = await f.Floating(f.Tool);
                Check.True(Math.Abs(f.Tool.FloatingWidth - size.Width) <= 1, $"Width {f.Tool.FloatingWidth} vs pane {size.Width}.");
                Check.True(Math.Abs(f.Tool.FloatingLeft - paneOrigin.X) <= 2, $"Left {f.Tool.FloatingLeft} vs pane {paneOrigin.X}.");
                Check.True(f.Tool.FloatingTop <= paneOrigin.Y, "The caption sits above (or, at the screen top, level with) the pane's former client area.");
                await Wait(() => Near(f.Coordinates.ToDesktopPoint(control, default), new(f.Tool.FloatingLeft, f.Tool.FloatingTop), 40));
            });
            tests.Test("placement: remembered floating bounds are reused", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var area = DesktopWindowCoordinates.GetWorkAreas(f.Manager)[0];
                f.Tool.FloatingLeft = area.X + 120;
                f.Tool.FloatingTop = area.Y + 90;
                f.Tool.FloatingWidth = 333;
                f.Tool.FloatingHeight = 222;
                f.Tool.Float();
                await f.Floating(f.Tool);
                Check.Equal(333d, f.Tool.FloatingWidth);
                Check.Equal(area.X + 120, f.Tool.FloatingLeft);
            });
            tests.Test("placement: off-screen floating bounds move onto a monitor", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                f.Document.FloatingLeft = -20000;
                f.Document.FloatingTop = -20000;
                f.Document.FloatingWidth = 400;
                f.Document.FloatingHeight = 300;
                f.Document.Float();
                await f.Floating(f.Document);
                var bounds = new Rect(f.Document.FloatingLeft, f.Document.FloatingTop, f.Document.FloatingWidth, f.Document.FloatingHeight);
                Check.True(DesktopWindowCoordinates.GetWorkAreas(f.Manager).Any(a => a.X <= bounds.X + 1 && a.Y <= bounds.Y + 1 && bounds.X + bounds.Width <= a.X + a.Width + 1 && bounds.Y + bounds.Height <= a.Y + a.Height + 1), $"Window {bounds} is not on a work area.");
            });
            tests.Test("placement: oversized floating bounds shrink to the work area", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                f.Document.FloatingLeft = 0;
                f.Document.FloatingTop = 0;
                f.Document.FloatingWidth = 20000;
                f.Document.FloatingHeight = 20000;
                f.Document.Float();
                await f.Floating(f.Document);
                Check.True(DesktopWindowCoordinates.GetWorkAreas(f.Manager).Any(a => f.Document.FloatingWidth <= a.Width + 1 && f.Document.FloatingHeight <= a.Height + 1));
            });
            tests.Test("floating: hiding the last tool hides its native host and Show restores it in place", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var control = await f.FloatTool(160, 120);
                var model = f.Tool.FindParent<LayoutFloatingWindow>()!;
                var before = Bounds(f.Tool);
                f.Tool.Hide();
                await Wait(() => !NativeVisible(control), "The empty native floating window stayed on screen.");
                Check.True(f.Manager.Layout.FloatingWindows.Contains(model), "The floating model was not retained for Show().");
                f.Tool.Show();
                await Wait(() => NativeVisible(control) && control.IsLoaded, "Show() did not present the floating window again.");
                Check.Same(control, f.Manager.FloatingWindows.Single());
                Check.Same(model, f.Tool.FindParent<LayoutFloatingWindow>());
                CheckBounds(before, Bounds(f.Tool), 2);
                await Wait(() => Near(f.Coordinates.ToDesktopPoint(control, default), new(f.Tool.FloatingLeft, f.Tool.FloatingTop), 40), "The floating window reappeared elsewhere.");
            });
            tests.Test("floating: HideWindowCommand hides the native host at once and IsVisible restores it", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var control = (LayoutAnchorableFloatingWindowControl)await f.FloatTool(180, 140);
                var before = Bounds(f.Tool);
                Check.True(control.HideWindowCommand.CanExecute(null));
                control.HideWindowCommand.Execute(null);
                Check.False(f.Tool.IsVisible);
                Check.False(NativeVisible(control));
                await Task.Delay(150);
                Check.False(NativeVisible(control));
                f.Tool.IsVisible = true;
                await Wait(() => NativeVisible(control) && control.IsLoaded, "IsVisible=true did not present the floating window again.");
                CheckBounds(before, Bounds(f.Tool), 2);
            });
            tests.Test("floating: an externally closed native host is recreated for its floating model", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                f.Document.FloatingWidth = 360;
                f.Document.FloatingHeight = 260;
                f.Document.Float();
                var control = await f.Floating(f.Document);
                control.NativeWindow!.Close();
                await Wait(() => control.NativeWindow == null, "The native window did not close.");
                Check.True(f.Document.FindParent<LayoutFloatingWindow>() != null, "An external close must not close model content.");
                f.Manager.Refresh();
                LayoutFloatingWindowControl? replacement = null;
                await Wait(() => (replacement = f.Manager.FloatingWindows.SingleOrDefault()) is { IsLoaded: true } && NativeVisible(replacement), "No live host replaced the closed one.");
                Check.False(ReferenceEquals(control, replacement));
                await Wait(() => ((FrameworkElement)f.Document.Content!).IsLoaded, "The document content is not presented by the replacement host.");
            });
            tests.Test("floating: switching native windows to in-surface windows keeps them in place", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var client = f.ClientOrigin();
                f.Tool.FloatingLeft = client.X + 220;
                f.Tool.FloatingTop = client.Y + 160;
                f.Tool.FloatingWidth = 300;
                f.Tool.FloatingHeight = 220;
                f.Tool.Float();
                var control = await f.Floating(f.Tool);
                await Wait(() => NativeVisible(control));
                var desktop = new Point(f.Tool.FloatingLeft, f.Tool.FloatingTop);
                var native = control.NativeWindow!;
                var nativeClosed = false;
                native.Closed += (_, _) => nativeClosed = true;
                f.Manager.FloatingWindowMode = FloatingWindowMode.InSurface;
                LayoutFloatingWindowControl? surfaceControl = null;
                await Wait(() => nativeClosed && (surfaceControl = f.Manager.FloatingWindows.SingleOrDefault()) is { NativeWindow: null, IsLoaded: true, Visibility: Visibility.Visible } && ReferenceEquals(surfaceControl.XamlRoot, f.Manager.XamlRoot), "The native host was not released into the surface.");
                Check.True(control.NativeWindow == null, "The native window survived the switch.");
                Check.True(Near(f.Coordinates.ToDesktopPoint(surfaceControl!, default), desktop, 4), $"In-surface window at {f.Coordinates.ToDesktopPoint(surfaceControl!, default)}, native frame was at {desktop}.");
                f.Manager.FloatingWindowMode = FloatingWindowMode.Native;
                LayoutFloatingWindowControl? nativeControl = null;
                await Wait(() => (nativeControl = f.Manager.FloatingWindows.SingleOrDefault()) is { IsLoaded: true } && NativeVisible(nativeControl), "The window did not return to a native host.");
                Check.True(Near(new(f.Tool.FloatingLeft, f.Tool.FloatingTop), desktop, 4), $"Native bounds {f.Tool.FloatingLeft},{f.Tool.FloatingTop} drifted from {desktop}.");
                await Wait(() => Near(f.Coordinates.ToDesktopPoint(nativeControl!, default), desktop, 40), "The native window does not present its content at its bounds.");
            });
            tests.Test("floating: an unregistered host window is resolved and closes its floating windows", async () =>
            {
                var f = new Fixture(FloatingWindowMode.Native, register: false);
                try
                {
                    await f.Show();
                    Check.Same(f.Window, typeof(DockingManager).GetProperty("HostWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Manager));
                    var control = await f.FloatTool(200, 160);
                    var floater = control.NativeWindow!;
                    var closed = false;
                    floater.Closed += (_, _) => closed = true;
                    f.CloseWindow();
                    await Wait(() => closed && control.NativeWindow == null && !f.Manager.FloatingWindows.Any(), "A floating window outlived its owner.");
                    Check.True(f.Tool.FindParent<LayoutFloatingWindow>() != null, "Closing the owner must not rewrite the layout model.");
                }
                finally
                {
                    f.Dispose();
                }
            });
            tests.Test("floating: closing a tool window leaves no host and Show re-floats it at the same bounds", () => CloseAndShow(FloatingWindowMode.Native));
            tests.Test("floating: keyboard moves follow the native origin convention", async () =>
            {
                using var f = new Fixture(FloatingWindowMode.Native);
                await f.Show();
                var control = await f.FloatTool(240, 200);
                var before = new Point(f.Tool.FloatingLeft, f.Tool.FloatingTop);
                typeof(LayoutFloatingWindowControl).GetMethod("MoveBy", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [10d, 20d]);
                await Wait(() => Near(new(f.Tool.FloatingLeft, f.Tool.FloatingTop), new(before.X + 10, before.Y + 20), 2), "The keyboard move did not move right and down by the requested DIPs.");
            });
        }

        tests.Test("floating: an in-surface tool window hides with its last tool", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            var control = await f.FloatTool(120, 100);
            f.Tool.Hide();
            await Wait(() => control.Visibility == Visibility.Collapsed, "The empty in-surface floating window stayed visible.");
            f.Tool.Show();
            await Wait(() => control.Visibility == Visibility.Visible && control.IsLoaded, "Show() did not present the in-surface window again.");
            Check.Same(control, f.Manager.FloatingWindows.Single());
        });
        tests.Test("floating: closing an in-surface tool window and Show re-floats it at the same bounds", () => CloseAndShow(FloatingWindowMode.InSurface));
        tests.Test("floating: an in-surface caption menu does not offer minimize", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            var control = await f.FloatTool(120, 100);
            var minimize = SystemCommands.CreateSystemMenu(control).Items.OfType<MenuFlyoutItem>().Single(i => i.Text == Properties.Resources.Window_Minimize);
            Check.False(minimize.IsEnabled);
            Check.True(minimize.Command == null, "A disabled item must not be re-enabled by its command.");
        });
        tests.Test("floating: an in-surface drag observes Escape at the window root and reuses resting guides", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            var control = await f.FloatTool(120, 100);
            var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Manager)!;
            var generation = (long)Invoke(surface, "BeginFloatingDrag", control)!;
            Check.True(generation != 0, "The floating drag did not begin.");
            try
            {
                Check.True((bool)Invoke(surface, "UpdateFloatingDrag", control, generation, new Point(420, 320), false)!);
                Check.True(Field(surface, "_escapeRoot") is UIElement, "Escape is not observed at the window root during a drag.");
                var tick = (long)Field(surface, "_adornerTick")!;
                Invoke(surface, "OnDragScroll", null, null);
                Check.Equal(tick, (long)Field(surface, "_adornerTick")!);
                await Task.Delay(320);
                Invoke(surface, "OnDragScroll", null, null);
                Check.True((long)Field(surface, "_adornerTick")! != tick, "Resting guides were never refreshed.");
            }
            finally
            {
                Invoke(surface, "CancelDrag");
            }

            Check.True(Field(surface, "_escapeRoot") == null, "The root Escape observer outlived the drag.");
            Check.False(control.IsDragging);
        });
        tests.Test("placement: in-surface floating windows stay inside the workspace", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            f.Tool.FloatingLeft = 5000;
            f.Tool.FloatingTop = 5000;
            f.Tool.FloatingWidth = 300;
            f.Tool.FloatingHeight = 200;
            f.Tool.Float();
            var control = await f.Floating(f.Tool);
            await Wait(() => Canvas.GetLeft(control) + control.Width <= f.Manager.ActualWidth + 1 && Canvas.GetTop(control) + control.Height <= f.Manager.ActualHeight + 1);
            Check.Equal(5000d, f.Tool.FloatingLeft);
        });
        tests.Test("placement: an in-surface float from a pane opens near that pane", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            var pane = f.PaneView(f.Tool);
            var origin = pane.TransformToVisual(f.Manager).TransformPoint(default);
            f.Tool.Float();
            await f.Floating(f.Tool);
            Check.True(Math.Abs(f.Tool.FloatingLeft - origin.X) <= 40, $"Left {f.Tool.FloatingLeft} vs pane {origin.X}.");
            Check.True(f.Tool.FloatingWidth >= 160 && f.Tool.FloatingWidth <= f.Manager.ActualWidth);
        });
        tests.Test("placement: drop-down chrome menus open below their button", async () =>
        {
            using var f = new Fixture(FloatingWindowMode.InSurface);
            await f.Show();
            var documents = f.Manager.FindVisualChildren<Button>().First(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(b) == Properties.Resources.Pane_OpenDocuments);
            var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(documents);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer).Invoke();
            await Wait(() => VisualTreeHelper.GetOpenPopupsForXamlRoot(f.Manager.XamlRoot).Any(p => p.Child != null));
            var popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(f.Manager.XamlRoot).First(p => p.Child != null);
            await Task.Delay(100);
            var menuTop = popup.Child.TransformToVisual(documents).TransformPoint(default).Y;
            Check.True(menuTop >= documents.ActualHeight - 2, $"Menu top {menuTop} is not below the {documents.ActualHeight}-high button.");
            popup.IsOpen = false;
        });
        return await tests.Run(output, "window-placement");
    }

    /// <summary>The close button hides the tools of a tool window. The floating model
        /// stays in the layout (the tools' PreviousContainer keeps its panes) and Show()
        /// re-inserts a tool there, presented by a live host at the remembered bounds.</summary>
        private static async Task CloseAndShow(FloatingWindowMode mode)
    {
        using var f = new Fixture(mode);
        await f.Show();
        var control = await f.FloatTool(170, 130);
        var model = f.Tool.FindParent<LayoutFloatingWindow>()!;
        var before = Bounds(f.Tool);
        control.Close();
        Check.False(f.Tool.IsVisible);
        await Wait(() => !f.Manager.FloatingWindows.Any(w => ReferenceEquals(w.Model, model) && (NativeVisible(w) || w.NativeWindow == null && w.Visibility == Visibility.Visible && w.IsLoaded)), "A closed tool window left a visible host.");
        Check.True(f.Manager.Layout.FloatingWindows.Contains(model), "Closing hid the tools; the floating window stays in the layout.");
        f.Tool.Show();
        Check.True(f.Tool.IsFloating, "Show() did not return the tool to its floating window.");
        Check.Same(model, f.Tool.FindParent<LayoutFloatingWindow>());
        LayoutFloatingWindowControl? host = null;
        await Wait(() => (host = f.Manager.FloatingWindows.SingleOrDefault(w => ReferenceEquals(w.Model, model))) is { IsLoaded: true } && (mode == FloatingWindowMode.Native ? NativeVisible(host) : host.Visibility == Visibility.Visible), "Show() did not present the floating window again.");
        CheckBounds(before, Bounds(f.Tool), 2);
        if (mode == FloatingWindowMode.Native)
            await Wait(() => Near(f.Coordinates.ToDesktopPoint(host!, default), new(f.Tool.FloatingLeft, f.Tool.FloatingTop), 40), "The floating window reappeared elsewhere.");
    }

    private static bool Near(Point a, Point b, double tolerance) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
    private static bool NativeVisible(LayoutFloatingWindowControl control) => control.NativeWindow is { } window && window.AppWindow.IsVisible;
    private static Rect Bounds(LayoutContent content) => new(content.FloatingLeft, content.FloatingTop, content.FloatingWidth, content.FloatingHeight);
    private static void CheckBounds(Rect expected, Rect actual, double tolerance) => Check.True(Math.Abs(expected.X - actual.X) <= tolerance && Math.Abs(expected.Y - actual.Y) <= tolerance && Math.Abs(expected.Width - actual.Width) <= tolerance && Math.Abs(expected.Height - actual.Height) <= tolerance, $"Bounds {actual} differ from {expected}.");
    private static object? Invoke(object target, string name, params object?[] arguments) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.Invoke(target, arguments);
    private static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);
    private static Task Wait(Func<bool> ready) => Wait(ready, "The window placement did not settle.");
    private static async Task Wait(Func<bool> ready, string message)
    {
        for (var i = 0; i < 120 && !ready(); i++)
            await Task.Delay(25);
        Check.True(ready(), message);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager;
        internal readonly LayoutAnchorable Tool = new()
        {
            Title = "Placement tool",
            ContentId = "placement-tool",
            Content = new TextBlock
            {
                Text = "Tool"
            }
        };
        internal readonly LayoutDocument Document = new()
        {
            Title = "Placement document",
            ContentId = "placement-document",
            Content = new TextBox
            {
                Text = "Document"
            }
        };
        internal readonly LayoutDocument Other = new()
        {
            Title = "Other document",
            ContentId = "placement-other",
            Content = new TextBlock
            {
                Text = "Other"
            }
        };
        internal readonly Window Window;
        internal readonly DesktopWindowCoordinates Coordinates = new();
        private readonly IDisposable? _registration;
        private bool _windowClosed;
        internal Fixture(FloatingWindowMode mode, bool register = true)
        {
            Manager = new()
            {
                FloatingWindowMode = mode,
                Theme = new GenericTheme(),
                CrossWindowCoordinates = Coordinates
            };
            var tools = new LayoutAnchorablePane(Tool)
            {
                DockWidth = new(260)
            };
            var documents = new LayoutDocumentPane(Document);
            documents.Children.Add(Other);
            var panel = new LayoutPanel(tools);
            panel.Children.Add(documents);
            Manager.Layout = new()
            {
                RootPanel = panel
            };
            Window = new Window
            {
                Content = Manager,
                Title = "UnoDock placement acceptance"
            };
            _registration = register ? Microsoft.Windows.Shell.SystemCommands.RegisterWindow(Window) : null;
            Window.AppWindow.Resize(new()
            {
                Width = 1100,
                Height = 720
            });
        }

        internal async Task Show()
        {
            Window.Activate();
            for (var i = 0; i < 80 && (!Manager.IsLoaded || Manager.ActualWidth <= 0); i++)
                await Task.Delay(25);
            await Task.Delay(250);
        }

        internal FrameworkElement PaneView(LayoutContent content) => Manager.FindVisualChildren<LayoutCachePaneControl>().First(p => ReferenceEquals(((ILayoutControl)p).Model, content.Parent));
        internal Point ClientOrigin() => Coordinates.ToDesktopPoint(Manager, default);
        /// <summary>Floats the tool at an offset from the first work area (native)
                /// or the surface origin (in-surface) and waits for its host.</summary>
                internal async Task<LayoutFloatingWindowControl> FloatTool(double left, double top)
        {
            var origin = Manager.FloatingWindowMode == FloatingWindowMode.Native ? DesktopWindowCoordinates.GetWorkAreas(Manager)[0] : new Rect(0, 0, 0, 0);
            Tool.FloatingLeft = origin.X + left;
            Tool.FloatingTop = origin.Y + top;
            Tool.FloatingWidth = 320;
            Tool.FloatingHeight = 240;
            Tool.Float();
            var control = await Floating(Tool);
            if (control.NativeWindow != null)
                await Wait(() => NativeVisible(control));
            return control;
        }

        internal void CloseWindow()
        {
            _windowClosed = true;
            Window.Close();
        }

        internal async Task<LayoutFloatingWindowControl> Floating(LayoutContent content)
        {
            LayoutFloatingWindowControl? control = null;
            await Wait(() => (control = content.FindParent<LayoutFloatingWindow>() is { } model ? Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, model)) : null) is { IsLoaded: true });
            await Task.Delay(150);
            return control!;
        }

        public void Dispose()
        {
            foreach (var control in Manager.FloatingWindows.ToArray())
                control.NativeWindow?.Close();
            _registration?.Dispose();
            if (!_windowClosed)
                Window.Close();
            Coordinates.Dispose();
        }
    }
}
