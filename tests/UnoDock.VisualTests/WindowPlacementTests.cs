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
        }

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

    private static bool Near(Point a, Point b, double tolerance) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 120 && !ready(); i++)
            await Task.Delay(25);
        Check.True(ready(), "The window placement did not settle.");
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
        private readonly IDisposable _registration;
        internal Fixture(FloatingWindowMode mode)
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
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(Window);
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
            _registration.Dispose();
            Window.Close();
            Coordinates.Dispose();
        }
    }
}
