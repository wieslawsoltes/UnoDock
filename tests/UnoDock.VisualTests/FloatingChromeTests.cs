using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Core;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Real-host chrome acceptance. Native decoration/geometry probes do not
/// call production adapters. Deterministic resize entry tests and actual XTEST /
/// SendInput tests are labeled separately and do not stand in for each other.</summary>
internal static class FloatingChromeTests
{
    private static readonly ChromeHit[] Edges = [ChromeHit.Left, ChromeHit.Top, ChromeHit.Right, ChromeHit.Bottom,
        ChromeHit.TopLeft, ChromeHit.TopRight, ChromeHit.BottomLeft, ChromeHit.BottomRight];
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        tests.Test("chrome: invalid direct mode value restores the previous setting", () =>
        {
            using var manager = new DockingManager();
            Check.Equal(FloatingWindowTitleBarMode.Custom, manager.FloatingWindowTitleBarMode);
            var failed = false;
            try { manager.SetValue(DockingManager.FloatingWindowTitleBarModeProperty, (FloatingWindowTitleBarMode)999); }
            catch (ArgumentOutOfRangeException) { failed = true; }
            Check.True(failed); Check.Equal(FloatingWindowTitleBarMode.Custom, manager.FloatingWindowTitleBarMode);
            return Task.CompletedTask;
        });
        foreach (var tools in new[] { false, true })
        {
            var kind = tools ? "tools" : "document";
            tests.Test($"chrome/{kind}: native frame is replaced and caption controls remain real", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                Check.True(f.Control.IsCustomTitleBar); FloatingChromeProbe.AssertCustom(f.Native, f.Owner);
                foreach (var id in new[] { "Dock", "Minimize", "Maximize", "Close" })
                { var button = f.Button(id); Check.True(button.IsLoaded && button.Visibility == Visibility.Visible && button.ActualWidth > 0); }
                foreach (var edge in Edges)
                { var grip = f.Grip(edge); Check.True(grip.IsLoaded && grip.ActualWidth > 0 && grip.ActualHeight > 0); }
                f.AssertEditors();
            });
            tests.Test($"chrome/{kind}: system/custom switching retains window, caption and editors", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var native = f.Native; var caption = f.Caption;
                for (var i = 0; i < 2; i++)
                {
                    f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.System;
                    await Task.Delay(120); Check.False(f.Control.IsCustomTitleBar); FloatingChromeProbe.AssertSystem(native);
                    f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.Custom;
                    await Task.Delay(120); Check.True(f.Control.IsCustomTitleBar); FloatingChromeProbe.AssertCustom(native, f.Owner);
                    Check.Same(native, f.Native); Check.Same(caption, f.Caption); f.AssertEditors();
                }
            });
            foreach (var edge in Edges)
            foreach (var cancel in new[] { false, true })
                tests.Test($"chrome/{kind}: actual native resize {edge}, cancel={cancel}", async () =>
                {
                    using var f = new Fixture(tools); await f.Show();
                    var before = FloatingChromeProbe.Bounds(f.Native);
                    var resize = Call(f.Control, "BeginFrameResize", edge, new Point(0, 0), null, null)!;
                    Check.True(resize != null && f.Control.IsResizing);
                    Call(f.Control, "MoveFrameResize", resize!, new Point(24, 18));
                    await Task.Delay(80);
                    var expected = ChromeResize.Apply(before, edge, 24, 18, 0, 0);
                    Near(expected, FloatingChromeProbe.Bounds(f.Native));
                    Call(f.Control, "EndFrameResize", resize!, cancel);
                    await Task.Delay(80); Check.False(f.Control.IsResizing); Check.False(f.Control.IsDragging);
                    Near(cancel ? before : expected, FloatingChromeProbe.Bounds(f.Native));
                    var final = FloatingChromeProbe.Bounds(f.Native);
                    Call(f.Control, "MoveFrameResize", resize!, new Point(200, 200));
                    Call(f.Control, "EndFrameResize", resize!, false);
                    Near(final, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
                });
            tests.Test($"chrome/{kind}: minimum clamp holds the opposite edge and Escape restores", async () =>
            {
                using var f = new Fixture(tools); await f.Show(); f.Control.MinWidth = 230; f.Control.MinHeight = 180;
                var before = FloatingChromeProbe.Bounds(f.Native);
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.TopLeft, new Point(0, 0), null, null)!;
                Call(f.Control, "MoveFrameResize", resize, new Point(10000, 10000)); await Task.Delay(80);
                var after = FloatingChromeProbe.Bounds(f.Native); var scale = OperatingSystem.IsMacOS() ? 1 : f.Control.XamlRoot!.RasterizationScale;
                Near(230 * scale, after.Width); Near(180 * scale, after.Height); Near(before.Right, after.Right); Near(before.Bottom, after.Bottom);
                Call(f.Control, "CancelFrameResize", true); await Task.Delay(80);
                Near(before, FloatingChromeProbe.Bounds(f.Native)); Check.False(f.Control.IsResizing);
            });
            tests.Test($"chrome/{kind}: non-resizable presenter cannot begin custom resize", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var presenter = (OverlappedPresenter)f.Native.AppWindow.Presenter;
                presenter.IsResizable = false; f.Manager.Refresh(); await Task.Delay(50);
                Check.True(Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null) == null);
                Check.False(f.Control.IsResizing);
                presenter.IsResizable = true; f.Manager.Refresh();
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null)!;
                Check.True(resize != null); Call(f.Control, "EndFrameResize", resize!, true);
            });
            tests.Test($"chrome/{kind}: mode switch cancels a pending resize before restoring decorations", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var native = f.Native;
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.BottomRight, new Point(0, 0), null, null)!;
                Call(f.Control, "MoveFrameResize", resize, new Point(60, 40));
                f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.System;
                await Task.Delay(100); Check.False(f.Control.IsResizing); Check.False(f.Control.IsCustomTitleBar);
                var beforeLate = FloatingChromeProbe.Bounds(native);
                Call(f.Control, "MoveFrameResize", resize, new Point(300, 300));
                Near(beforeLate, FloatingChromeProbe.Bounds(native)); f.AssertEditors();
            });
            tests.Test($"chrome/{kind}: light/dark switching retains focused editor and chrome controls", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var caption = f.Caption; var native = f.Native; var grips = Edges.Select(f.Grip).ToArray();
                var editor = (TextBox)f.Source[0].Content!;
                native.Activate(); Check.True(editor.Focus(FocusState.Keyboard)); await Task.Delay(60);
                Check.True(editor.FocusState != FocusState.Unfocused);
                f.Manager.Theme = new FluentTheme(ElementTheme.Dark); f.Manager.Refresh(); await Task.Delay(60);
                FloatingChromeProbe.AssertCustom(native, f.Owner);
                Check.Same(native, f.Native); Check.Same(caption, f.Caption);
                for (var i = 0; i < grips.Length; i++) Check.Same(grips[i], f.Grip(Edges[i]));
                f.Manager.Theme = new FluentTheme(ElementTheme.Light); f.Manager.Refresh(); f.AssertEditors();
            });
            if ((OperatingSystem.IsLinux() || OperatingSystem.IsWindows()) && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                foreach (var edge in Edges)
                    tests.Test($"chrome/{kind}: physical pointer resizes {edge}", async () =>
                    {
                        using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                        f.Native.Activate(); var before = FloatingChromeProbe.Bounds(f.Native); var grip = f.Grip(edge);
                        var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
                        input.MoveTo(grip, down); await Task.Delay(70); input.Press(); await Task.Delay(70);
                        Check.True(f.Control.IsResizing, "The native press did not capture a resize grip.");
                        // Resolve the finish against the original screen position via
                        // an unmoved owner, not against the now-moving grip itself.
                        var startInOwner = input.OwnerPoint(grip, down, f.Manager);
                        input.MoveTo(f.Manager, new(startInOwner.X + 22, startInOwner.Y + 16));
                        await Task.Delay(100); input.Release(); await Wait(() => !f.Control.IsResizing);
                        var scale = f.Control.XamlRoot!.RasterizationScale;
                        Near(ChromeResize.Apply(before, edge, 22 * scale, 16 * scale, 0, 0), FloatingChromeProbe.Bounds(f.Native), 2);
                        Check.False(f.Control.IsDragging); f.AssertEditors();
                    });
                tests.Test($"chrome/{kind}: physical Escape cancels resize without a late-release commit", async () =>
                {
                    using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                    f.Native.Activate(); var before = FloatingChromeProbe.Bounds(f.Native); var grip = f.Grip(ChromeHit.BottomRight);
                    var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
                    var ownerPoint = input.OwnerPoint(grip, down, f.Manager);
                    input.MoveTo(grip, down); await Task.Delay(60); input.Press(); await Task.Delay(60);
                    Check.True(f.Control.IsResizing);
                    input.MoveTo(f.Manager, new(ownerPoint.X + 45, ownerPoint.Y + 30)); await Task.Delay(100);
                    input.EscapeDown(); await Wait(() => !f.Control.IsResizing); input.EscapeUp(); input.Release(); await Task.Delay(100);
                    Near(before, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
                });
                tests.Test($"chrome/{kind}: physical custom maximize and restore buttons retain the native host", async () =>
                {
                    using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                    // Xvfb without a window manager cannot implement EWMH maximize.
                    // The dedicated Openbox job and Windows run execute this behavior.
                    if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_REQUIRE_WM") != "1")
                    { Check.True(f.Button("Maximize").IsEnabled); return; }
                    var native = f.Native; var before = FloatingChromeProbe.Bounds(native);
                    await input.Click(f.Button("Maximize")); await Wait(() => f.Control.IsMaximized);
                    Check.True(f.Control.IsCustomTitleBar);
                    await input.Click(f.Button("Maximize")); await Wait(() => !f.Control.IsMaximized);
                    await Task.Delay(100); Near(before, FloatingChromeProbe.Bounds(native), 2);
                    Check.Same(native, f.Native); f.AssertEditors();
                });
                tests.Test($"chrome/{kind}: physical custom close honors veto and then closes once", async () =>
                {
                    using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                    var veto = true; var calls = 0;
                    f.Control.Closing += (_, e) => { calls++; e.Cancel = veto; };
                    await input.Click(f.Button("Close")); await Wait(() => calls == 1);
                    Check.True(f.Control.NativeWindow != null && f.Control.IsCustomTitleBar); f.AssertEditors();
                    veto = false; await input.Click(f.Button("Close")); await Wait(() => f.Control.NativeWindow == null);
                    Check.Equal(2, calls); Check.False(f.Control.IsResizing); Check.False(f.Control.IsDragging);
                });
            }
        }
        return await tests.Run(output, "floating-chrome");
    }
    internal sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new() { Width = 920, Height = 580, FloatingWindowMode = FloatingWindowMode.Native, Theme = new FluentTheme() };
        internal readonly Window Owner;
        internal readonly LayoutContent[] Source;
        internal LayoutFloatingWindowControl Control = null!;
        internal Window Native => Control.NativeWindow ?? throw new InvalidOperationException("The native floating window is missing.");
        internal Border Caption => Control.FindVisualChildren<Border>().Single(b => b.Name == "PART_FloatingDragHandle");
        private readonly object?[] _editors;
        private readonly IDisposable _registration;
        private readonly List<Exception> _errors = [];
        internal Fixture(bool tools)
        {
            var documents = new LayoutDocumentPane(new LayoutDocument { Title = "Destination", Content = new TextBox { Text = "Destination" } });
            Manager.Layout = new() { RootPanel = new LayoutPanel(documents) };
            if (tools)
            {
                Source = Enumerable.Range(0, 2).Select(i => (LayoutContent)new LayoutAnchorable { Title = "Tool " + i, ContentId = "tool-" + i,
                    Content = new TextBox { Text = "Unsaved tool draft " + i }, CanDockAsTabbedDocument = true,
                    FloatingLeft = 160, FloatingTop = 120, FloatingWidth = 440, FloatingHeight = 320 }).ToArray();
                var pane = new LayoutAnchorablePane((LayoutAnchorable)Source[0]); pane.Children.Add((LayoutAnchorable)Source[1]);
                var group = new LayoutAnchorablePaneGroup(); group.Children.Add(pane);
                Manager.Layout.FloatingWindows.Add(new LayoutAnchorableFloatingWindow { RootPanel = group });
            }
            else
            {
                var source = new LayoutDocument { Title = "Document", ContentId = "document", Content = new TextBox { Text = "Unsaved document draft" },
                    FloatingLeft = 160, FloatingTop = 120, FloatingWidth = 440, FloatingHeight = 320 };
                Source = [source]; Manager.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootDocument = source });
            }
            Source[0].IsActive = true; _editors = Source.Select(s => s.Content).ToArray();
            Owner = new Window { Content = Manager, Title = "Custom floating chrome acceptance" };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(Owner);
            Owner.AppWindow.Move(new() { X = 20, Y = 20 }); Owner.AppWindow.Resize(new() { Width = 1000, Height = 700 }); Owner.Activate();
        }
        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded); Manager.Refresh(); await Wait(() => Manager.FloatingWindows.Count() == 1);
            Control = Manager.FloatingWindows.Single(); Control.MessageFilterFailed += (_, e) => _errors.Add(e);
            await Wait(() => Control.IsLoaded && Control.NativeWindow != null && Control.ActualWidth > 0);
            await Task.Delay(100); Check.True(Control.IsCustomTitleBar, string.Join("\n", _errors));
        }
        internal Border Grip(ChromeHit edge) => Control.FindVisualChildren<Border>().Single(b => b.Name == "PART_FloatingResize" + edge);
        internal Button Button(string action) => Control.FindVisualChildren<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "FloatingWindow" + action);
        internal void AssertEditors()
        {
            for (var i = 0; i < Source.Length; i++) Check.Same(_editors[i], Source[i].Content);
            Check.Equal(0, _errors.Count);
        }
        public void Dispose()
        {
            try { Manager.Dispose(); }
            finally { Owner.Content = null; Owner.Close(); _registration.Dispose(); }
        }
    }
    private sealed class PointerInput : IDisposable
    {
        private readonly X11TestInput? _x11;
        private readonly WindowsFloatingInputTests.NativeInput? _windows;
        internal PointerInput()
        { if (OperatingSystem.IsLinux()) _x11 = new(); else _windows = new(); }
        internal void MoveTo(FrameworkElement e, Point p) { if (_x11 != null) _x11.MoveTo(e, p); else _windows!.MoveTo(e, p); }
        internal Point OwnerPoint(FrameworkElement source, Point p, FrameworkElement owner)
        {
            var screen = _x11?.ScreenPoint(source, p) ?? _windows!.ScreenPoint(source, p);
            var origin = _x11?.ScreenPoint(owner, new(0, 0)) ?? _windows!.ScreenPoint(owner, new(0, 0));
            return new((screen.X - origin.X) / owner.XamlRoot!.RasterizationScale, (screen.Y - origin.Y) / owner.XamlRoot.RasterizationScale);
        }
        internal void Press() { if (_x11 != null) _x11.Press(); else _windows!.Press(); }
        internal void Release() { if (_x11 != null) _x11.Release(); else _windows!.Release(); }
        internal void EscapeDown() { if (_x11 != null) _x11.KeyDown(0xff1b); else _windows!.KeyDown(0x1b); }
        internal void EscapeUp() { if (_x11 != null) _x11.KeyUp(0xff1b); else _windows!.KeyUp(0x1b); }
        internal async Task Click(FrameworkElement e)
        { MoveTo(e, new(e.ActualWidth / 2, e.ActualHeight / 2)); await Task.Delay(70); Press(); await Task.Delay(50); Release(); }
        public void Dispose() { _x11?.Dispose(); _windows?.Dispose(); }
    }
    private static object? Call(object target, string name, params object?[] args)
    {
        var method = typeof(LayoutFloatingWindowControl).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(name);
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private static void Near(double expected, double actual, double tolerance = 1)
        => Check.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual}, tolerance {tolerance}.");
    private static void Near(DockRect expected, DockRect actual, double tolerance = 1)
    { Near(expected.X, actual.X, tolerance); Near(expected.Y, actual.Y, tolerance); Near(expected.Width, actual.Width, tolerance); Near(expected.Height, actual.Height, tolerance); }
    private static async Task Wait(Func<bool> ready)
    { for (var i = 0; i < 160 && !ready(); i++) await Task.Delay(25); Check.True(ready(), "Native chrome transition did not settle."); }
}
