using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

/// <summary>Real desktop windows; no stub UI types and no guessed non-client geometry.</summary>
public static partial class WindowCoordinateTests
{
    public static async Task<int> Run(string output, DockingManager? host = null)
    {
        var tests = new TestRunner();
        tests.Test("coordinate adapter rejects detached visuals", () =>
        {
            using var adapter = new DesktopWindowCoordinates();
            Check.Throws<InvalidOperationException>(() => adapter.Translate(new Grid(), default, new Grid()));
        });
        tests.Test("coordinate adapter rejects non-finite coordinates", () =>
        {
            using var adapter = new DesktopWindowCoordinates();
            Check.Throws<ArgumentOutOfRangeException>(() => adapter.ToScreen(new Grid(), new(double.NaN, 0)));
        });
        tests.Test("coordinate adapter validates visual arguments", () =>
        {
            using var adapter = new DesktopWindowCoordinates();
            Check.Throws<ArgumentNullException>(() => adapter.Translate(null!, default, new Grid()));
        });
        tests.Test("coordinate adapter is unusable after disposal", () =>
        {
            var adapter = new DesktopWindowCoordinates(); adapter.Dispose(); adapter.Dispose();
            Check.Throws<ObjectDisposedException>(() => adapter.ToScreen(new Grid(), default));
        });
        tests.Test("manager owns only its default coordinate adapter", () =>
        {
            var manager = new DockingManager(); var owned = (DesktopWindowCoordinates)manager.CrossWindowCoordinates!;
            var external = new ExternalCoordinates(); manager.CrossWindowCoordinates = external;
            manager.Dispose(); Check.False(external.Disposed);
            Check.Throws<ObjectDisposedException>(() => owned.ToScreen(new Grid(), default));
        });
        tests.Test("floating single-content property retains public setter", () =>
        {
            using var manager = new DockingManager(); var model = new LayoutAnchorable();
            manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(model));
            var item = manager.GetLayoutItemFromModel(model);
            var control = new LayoutAnchorableFloatingWindowControl(new());
            control.SingleContentLayoutItem = item; Check.Same(item, control.SingleContentLayoutItem);
            control.SingleContentLayoutItem = null; Check.True(control.SingleContentLayoutItem == null);
        });
        // Mobile/browser heads compile this suite but cannot create native windows.
        // A CI job must report its host separately; skipped host coverage is not a pass.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            var a = new Window { Title = "UnoDock coordinate test A" };
            var b = new Window { Title = "UnoDock coordinate test B" };
            var source = new Grid { Width = 100, Height = 70, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(13, 19, 0, 0) };
            var destination = new Grid { Width = 120, Height = 80, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(29, 31, 0, 0) };
            var ar = new Grid(); ar.Children.Add(source); a.Content = ar;
            var br = new Grid(); br.Children.Add(destination); b.Content = br;
            using var adapter = new DesktopWindowCoordinates();
            var closed = false;
            try
            {
                a.AppWindow.Move(new() { X = 73, Y = 97 }); a.AppWindow.Resize(new() { Width = 420, Height = 340 });
                b.AppWindow.Move(new() { X = 581, Y = 173 }); b.AppWindow.Resize(new() { Width = 360, Height = 280 });
                a.Activate(); b.Activate();
                await Until(() => source.IsLoaded && destination.IsLoaded && source.ActualWidth > 0 && destination.ActualWidth > 0);
                tests.Test("same-root conversion equals framework transform", () =>
                {
                    var expected = source.TransformToVisual(ar).TransformPoint(new(7.25, -2.75));
                    Near(expected, adapter.Translate(source, new(7.25, -2.75), ar));
                });
                tests.Test("native client-to-client roundtrip retains fractional coordinates", () =>
                {
                    var point = new Point(9.125, -6.75);
                    var translated = adapter.Translate(source, point, destination);
                    Near(point, adapter.Translate(destination, translated, source), OperatingSystem.IsWindows() ? 1 : 1e-6);
                    Check.True(Math.Abs(translated.X - point.X) > 100, "Different window positions must affect translated coordinates.");
                });
                tests.Test("cross-window conversion agrees with physical screen projection", () =>
                {
                    var point = new Point(23.5, 40.125);
                    var screen = adapter.ToScreen(source, point);
                    Near(adapter.FromScreen(screen, destination), adapter.Translate(source, point, destination), OperatingSystem.IsWindows() ? 1 : 1e-6);
                    Near(point, adapter.FromScreen(screen, source), OperatingSystem.IsWindows() ? 1 : 1e-6);
                });
                tests.Test("cross-window projection includes local visual transforms", () =>
                {
                    source.RenderTransform = new ScaleTransform { ScaleX = 1.5, ScaleY = .75 };
                    destination.RenderTransform = new ScaleTransform { ScaleX = .5, ScaleY = 1.25 };
                    try
                    {
                        var p = new Point(18.5, 22.25);
                        Near(p, adapter.Translate(destination, adapter.Translate(source, p, destination), source), OperatingSystem.IsWindows() ? 2 : 1e-5);
                    }
                    finally { source.RenderTransform = null; destination.RenderTransform = null; }
                });
                tests.Test("moving a native window invalidates no cached origin", async () =>
                {
                    var previous = adapter.ToScreen(source, default);
                    var oldPosition = a.AppWindow.Position;
                    a.AppWindow.Move(new() { X = oldPosition.X + 31, Y = oldPosition.Y + 17 });
                    await Until(() => Math.Abs(adapter.ToScreen(source, default).X - previous.X) >= 1);
                    var current = adapter.ToScreen(source, default);
                    Check.Near(31, current.X - previous.X, 1); Check.Near(17, current.Y - previous.Y, 1);
                });
                tests.Test("coordinate ownership rejects background-thread access", async () =>
                {
                    await Task.Run(() => Check.Throws<InvalidOperationException>(() => adapter.Translate(source, default, destination)));
                });
                tests.Test("closed destination rejects coordinate transfer", async () =>
                {
                    CloseTestWindow(b); closed = true;
                    await Task.Delay(25);
                    Check.Throws<InvalidOperationException>(() => adapter.Translate(source, default, destination));
                });
                if (host != null)
                {
                    RegisterNativeInput(tests, host);
                    tests.Test("native tool-window drop plan transfers between XamlRoots", async () =>
                    {
                        var original = host.Layout; var mode = host.FloatingWindowMode;
                        var toolA = new LayoutAnchorable { Title = "A", Content = new TextBox { Text = "retained editor" }, FloatingLeft = 70, FloatingTop = 110 };
                        var toolB = new LayoutAnchorable { Title = "B", Content = new TextBox { Text = "target editor" }, FloatingLeft = 780, FloatingTop = 110 };
                        var document = new LayoutDocument { Title = "Main" };
                        try
                        {
                            host.FloatingWindowMode = FloatingWindowMode.Native;
                            host.Layout = new() { RootPanel = new UnoDock.Layout.LayoutPanel(new LayoutDocumentPane(document)) };
                            toolA.AddToLayout(host, AnchorableShowStrategy.Left); toolB.AddToLayout(host, AnchorableShowStrategy.Right);
                            toolA.Float(); toolB.Float(); host.Refresh();
                            await Until(() => host.FloatingWindows.Count() == 2 && host.FloatingWindows.All(w => w.NativeWindow != null && w.IsLoaded));
                            await Task.Delay(50);
                            var target = toolB.Parent;
                            var area = host.GetDropAreas().OfType<DropArea<FrameworkElement>>()
                                .Single(a => a.AreaElement is ILayoutControl c && ReferenceEquals(c.Model, target));
                            Check.True(area.DetectionRect.Width > 0 && area.DetectionRect.Height > 0);
                            var rect = area.DetectionRect;
                            var plan = host.GetDropPlan(toolA, new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
                            Check.True(plan != null); Check.Equal(DropTargetType.AnchorablePaneDockInside, plan!.Type);
                            var view = host.GetLayoutItemFromModel(toolA).View;
                            Check.True(plan.Execute()); host.Refresh();
                            Check.Same(target, toolA.Parent); Check.Same(view, host.GetLayoutItemFromModel(toolA).View);
                            Check.Equal("retained editor", ((TextBox)toolA.Content!).Text);
                            Check.Equal(1, host.Layout.FloatingWindows.Count);
                        }
                        finally { host.FloatingWindowMode = mode; host.Layout = original; host.Refresh(); }
                    });
                    tests.Test("closed native target invalidates a previously accepted plan", async () =>
                    {
                        var original = host.Layout; var mode = host.FloatingWindowMode;
                        var source = new LayoutAnchorable { Title = "Source" }; var target = new LayoutAnchorable { Title = "Target", CanClose = true };
                        try
                        {
                            host.FloatingWindowMode = FloatingWindowMode.Native; host.Layout = new();
                            source.AddToLayout(host, AnchorableShowStrategy.Left); target.AddToLayout(host, AnchorableShowStrategy.Right);
                            target.Float(); host.Refresh();
                            await Until(() => host.FloatingWindows.Any(w => w.NativeWindow != null && w.IsLoaded));
                            var pane = (ILayoutGroup)target.Parent!;
                            var plan = DockDropPlan.Create(source, pane, DropTargetType.AnchorablePaneDockInside, new(0, 0, 400, 300))!;
                            Check.True(plan.CanExecute); target.Close(); host.Refresh();
                            Check.False(plan.CanExecute); Check.False(plan.Execute()); Check.False(source.IsFloating);
                        }
                        finally { host.FloatingWindowMode = mode; host.Layout = original; host.Refresh(); }
                    });
                }
                return await tests.Run(output, "window-coordinates");
            }
            finally { if (!closed) { CloseTestWindow(b); } CloseTestWindow(a); }
        }
        Console.WriteLine("Native coordinate runtime cases are not executed on this host.");
        return await tests.Run(output, "window-coordinates");
    }
    private static void CloseTestWindow(Window window)
    {
        // These auxiliary windows are not owned by DockingManager. Invoke the
        // same unmap-before-teardown path to isolate subsequent native tests.
        typeof(DesktopWindowCoordinates).GetMethod("HideNativeClientBeforeClose",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [window]);
        window.Content = null; window.Close();
    }
    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        { if (DateTime.UtcNow >= deadline) throw new TimeoutException("Desktop window did not reach the requested state."); await Task.Delay(20); }
    }
    private static void Near(Point a, Point b, double tolerance = 1e-5)
    { Check.Near(a.X, b.X, tolerance); Check.Near(a.Y, b.Y, tolerance); }
    private sealed class ExternalCoordinates : ICrossWindowCoordinates, IDisposable
    {
        public bool Disposed { get; private set; }
        public Point Translate(FrameworkElement source, Point point, FrameworkElement destination) => point;
        public void Dispose() => Disposed = true;
    }
}
