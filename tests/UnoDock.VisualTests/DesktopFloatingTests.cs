using System.Reflection;
using System.Runtime.ExceptionServices;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using UnoDock.VisualValidation;
using Windows.Foundation;

namespace UnoDock.Testing;

internal static class DesktopFloatingTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var native in new[] { false, true })
        {
            foreach (var edge in Enumerable.Range(0, 4))
                Add($"tool subtree survives root edge {edge}", native, true, async f =>
                {
                    var group = f.Group!;
                    var parents = f.Source.Select(c => c.Parent).ToArray();
                    var docked = new List<LayoutContent>(); var preview = new List<LayoutContent>();
                    f.Manager.PreviewDock += (_, e) => preview.Add(((DockEventArgs)e).Content);
                    f.Manager.Docked += (_, e) => docked.Add(((DockEventArgs)e).Content);
                    using var session = f.Session();
                    var plan = DockDropPlan.Create(f.Source[0], f.Manager.Layout.RootPanel, (DropTargetType)edge, new(0, 0, 1000, 600));
                    Check.True(session.Execute(plan));
                    Check.Equal(3, preview.Count); Check.Equal(3, docked.Count);
                    Check.True(preview.SequenceEqual(f.Source)); Check.True(docked.SequenceEqual(f.Source));
                    Check.Same(f.Manager.Layout, group.Root);
                    for (var i = 0; i < parents.Length; i++) Check.Same(parents[i], f.Source[i].Parent);
                    Check.True(f.Source.All(c => c.FindParent<LayoutFloatingWindow>() == null));
                    Check.False(session.Execute(plan));
                    await Task.CompletedTask;
                });
            Add("all tool tabs merge in order and retain their editor objects", native, true, async f =>
            {
                var editors = f.Source.Select(c => c.Content).ToArray();
                using var session = f.Session();
                var plan = DockDropPlan.Create(f.Source[0], f.Tools, DropTargetType.AnchorablePaneDockInside, new(0, 0, 1000, 600));
                Check.True(session.Execute(plan));
                Check.True(f.Tools.Children.Skip(1).SequenceEqual(f.Source));
                for (var i = 0; i < editors.Length; i++) Check.Same(editors[i], f.Source[i].Content);
                await Task.CompletedTask;
            });
            Add("document docks into the existing document pane", native, false, async f =>
            {
                var editor = f.Source[0].Content;
                using var session = f.Session();
                var plan = DockDropPlan.Create(f.Source[0], f.Documents, DropTargetType.DocumentPaneDockInside, new(0, 0, 1000, 600));
                Check.True(session.Execute(plan)); Check.Same(f.Documents, f.Source[0].Parent); Check.Same(editor, f.Source[0].Content);
                await Task.CompletedTask;
            });
            Add("second-tab veto precedes every library mutation", native, true, async f =>
            {
                var parents = f.Source.Select(c => c.Parent).ToArray(); var calls = 0; var docked = 0;
                f.Manager.PreviewDock += (_, e) => { calls++; if (ReferenceEquals(((DockEventArgs)e).Content, f.Source[1])) ((DockEventArgs)e).Cancel = true; };
                f.Manager.Docked += (_, _) => docked++;
                using var session = f.Session();
                Check.False(session.Execute(f.InsidePlan())); Check.Equal(2, calls); Check.Equal(0, docked);
                for (var i = 0; i < parents.Length; i++) Check.Same(parents[i], f.Source[i].Parent);
                Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)));
                await Task.CompletedTask;
            });
            foreach (var mutation in new[] { "enabled ABA", "parent ABA", "target children ABA", "root ABA", "throwing preview" })
                Add("preflight revocation: " + mutation, native, true, async f =>
                {
                    var first = true;
                    RoutedEventHandler callback = (_, _) =>
                    {
                        if (!first) return; first = false;
                        switch (mutation)
                        {
                            case "enabled ABA": f.Source[1].IsEnabled = false; f.Source[1].IsEnabled = true; break;
                            case "parent ABA": var pane = (LayoutAnchorablePane)f.Source[1].Parent!; pane.Children.Remove((LayoutAnchorable)f.Source[1]); pane.Children.Add((LayoutAnchorable)f.Source[1]); break;
                            case "target children ABA": var tool = f.Tools.Children[0]; f.Tools.Children.Remove(tool); f.Tools.Children.Add(tool); break;
                            case "root ABA": var root = f.Manager.Layout; f.Manager.Layout = new(); f.Manager.Layout = root; break;
                            default: throw new InvalidOperationException("application preview failure");
                        }
                    };
                    f.Manager.PreviewDock += callback;
                    using var session = f.Session();
                    var plan = f.InsidePlan();
                    if (mutation == "throwing preview")
                    {
                        Exception? error = null;
                        try { session.Execute(plan); } catch (Exception e) { error = e; }
                        Check.True(error is InvalidOperationException { Message: "application preview failure" });
                        f.Manager.PreviewDock -= callback;
                        using var retry = f.Session(); Check.True(retry.Execute(plan));
                    }
                    else
                    {
                        Check.False(session.Execute(plan));
                        Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)));
                    }
                    await Task.CompletedTask;
                });
            Add("whole-group tabbed-document permission is checked", native, true, async f =>
            {
                ((LayoutAnchorable)f.Source[1]).CanDockAsTabbedDocument = false;
                using var session = f.Session();
                var plan = DockDropPlan.Create(f.Source[0], f.Documents, DropTargetType.DocumentPaneDockInside, new(0, 0, 1000, 600));
                Check.False(session.Execute(plan));
                Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)));
                await Task.CompletedTask;
            });
            Add("a canceled surface gesture cannot dock on late release", native, false, async f =>
            {
                var generation = (long)Call(f.Surface, "BeginFloatingDrag", f.Control)!;
                Check.True(generation > 0);
                Call(f.Surface, "CancelDrag");
                Check.False((bool)Call(f.Surface, "CompleteFloatingDrag", f.Control, generation, f.DocumentCenter(), false)!);
                Check.Same(f.Floating, f.Source[0].FindParent<LayoutFloatingWindow>());
                await Task.CompletedTask;
            });
            Add("Ctrl suppression and a fresh release outside the client do not reuse hover", native, false, async f =>
            {
                var generation = (long)Call(f.Surface, "BeginFloatingDrag", f.Control)!;
                Check.True(generation > 0);
                Call(f.Surface, "UpdateFloatingDrag", f.Control, generation, f.DocumentCenter(), false);
                Check.False((bool)Call(f.Surface, "CompleteFloatingDrag", f.Control, generation, f.DocumentCenter(), true)!);
                Check.Same(f.Floating, f.Source[0].FindParent<LayoutFloatingWindow>());
                generation = (long)Call(f.Surface, "BeginFloatingDrag", f.Control)!;
                Call(f.Surface, "UpdateFloatingDrag", f.Control, generation, f.DocumentCenter(), false);
                Check.False((bool)Call(f.Surface, "CompleteFloatingDrag", f.Control, generation, new Point(-4000, -4000), false)!);
                Check.Same(f.Floating, f.Source[0].FindParent<LayoutFloatingWindow>());
                await Task.CompletedTask;
            });
            Add("theme switches keep the native host and existing editors", native, true, async f =>
            {
                var host = f.Control.NativeWindow; var editors = f.Source.Select(c => c.Content).ToArray();
                foreach (var mode in new[] { ElementTheme.Dark, ElementTheme.Light, ElementTheme.Dark })
                {
                    var theme = new FluentTheme(mode); f.Manager.Theme = theme; f.Manager.Refresh();
                    await Task.Delay(50);
                    Check.Same(host, f.Control.NativeWindow);
                    var frame = Field<Grid>(f.Control, "_frame"); var caption = Field<TextBlock>(f.Control, "_caption");
                    Check.Same(theme.ThemeResourceDictionary["UnoDock.PaneBrush"], frame.Background);
                    Check.Same(theme.ThemeResourceDictionary["UnoDock.ForegroundBrush"], caption.Foreground);
                    for (var i = 0; i < editors.Length; i++) Check.Same(editors[i], f.Source[i].Content);
                }
                var path = Path.Combine(output, "visuals", "desktop-tools-" + (native ? "native" : "surface") + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); await VisualCapture.Save(f.Control, path);
            });
        }
        foreach (var tools in new[] { false, true })
        {
            Add("native coordinate round-trip and source-window stacking exclusion", true, tools, async f =>
            {
                foreach (var point in new[] { new Point(1, 1), new Point(70.25, 40.5), new Point(240, 110) })
                {
                    var roundTrip = f.Coordinates.FromScreen(f.Coordinates.ToScreen(f.Control, point), f.Control);
                    Check.Near(point.X, roundTrip.X, 1.1); Check.Near(point.Y, roundTrip.Y, 1.1);
                    var mapped = f.Coordinates.Translate(f.Control, point, f.Surface);
                    var reversed = f.Coordinates.Translate(f.Surface, mapped, f.Control);
                    Check.Near(point.X, reversed.X, 1.1); Check.Near(point.Y, reversed.Y, 1.1);
                }
                var target = f.DocumentCenter();
                await f.CoverTarget(target);
                var args = new object?[] { f.Surface, target, null, f.Control.NativeWindow };
                Check.True((bool)Call(f.Coordinates, "TryGetTopmostRoot", args)!);
                Check.Same(f.Manager.XamlRoot, args[2]);
                args = [f.Surface, target, null, null];
                Check.True((bool)Call(f.Coordinates, "TryGetTopmostRoot", args)!);
                Check.Same(f.Control.XamlRoot, args[2]);
            });
        }
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            foreach (var tools in new[] { false, true })
                foreach (var cancel in new[] { false, true })
                    Add("XTEST: native caption " + (cancel ? "Escape restores geometry" : "moves and docks on release"), true, tools, async f =>
                    {
                        using var input = new X11TestInput();
                        var handle = Field<Border>(f.Control, "_dragHandle");
                        var origin = f.Control.NativeWindow!.AppWindow.Position;
                        input.MoveTo(handle, new(Math.Min(100, handle.ActualWidth / 2), handle.ActualHeight / 2));
                        await Task.Delay(60); input.Press(); await Task.Delay(60);
                        var target = f.DocumentCenter();
                        input.MoveTo(f.Surface, target); await Wait(() => f.Control.IsDragging);
                        if (cancel)
                        {
                            input.KeyDown(0xff1b); await Wait(() => !f.Control.IsDragging); input.KeyUp(0xff1b); input.Release();
                            await Task.Delay(80);
                            Check.Equal(origin.X, f.Control.NativeWindow!.AppWindow.Position.X);
                            Check.Equal(origin.Y, f.Control.NativeWindow.AppWindow.Position.Y);
                            Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)));
                        }
                        else
                        {
                            input.Release();
                            await Wait(() => f.Source.All(c => ReferenceEquals(c.Parent, f.Documents)));
                            Check.False(f.Control.IsDragging);
                            Check.True(f.Source.All(c => ReferenceEquals(c.Root, f.Manager.Layout)));
                        }
                    });
        return await tests.Run(output, "desktop-floating");

        void Add(string name, bool native, bool tools, Func<Fixture, Task> body) => tests.Test(
            $"{(native ? "native" : "surface")}/{(tools ? "tools" : "document")}: {name}", async () =>
            {
                using var fixture = new Fixture(native, tools); await fixture.Show(); await body(fixture);
                Check.Equal(0, fixture.NativeFailures.Count);
            });
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DesktopWindowCoordinates Coordinates = new();
        internal readonly DockingManager Manager;
        internal readonly LayoutDocumentPane Documents = new(new LayoutDocument { Title = "Main document", ContentId = "main", Content = new TextBox { Text = "Main document editor" } });
        internal readonly LayoutAnchorablePane Tools = new(new LayoutAnchorable { Title = "Existing tool", ContentId = "existing", Content = new TextBox { Text = "Existing tool editor" } }) { DockWidth = new(220) };
        internal readonly LayoutFloatingWindow Floating;
        internal readonly LayoutAnchorablePaneGroup? Group;
        internal readonly LayoutContent[] Source;
        internal readonly List<Exception> NativeFailures = [];
        internal LayoutFloatingWindowControl Control = null!;
        private readonly Window _window;
        private readonly IDisposable _registration;
        private readonly bool _native;
        internal FrameworkElement Surface => (FrameworkElement)typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager)!;
        internal Fixture(bool native, bool tools)
        {
            _native = native;
            Manager = new() { Width = 1000, Height = 640, FloatingWindowMode = native ? FloatingWindowMode.Native : FloatingWindowMode.InSurface,
                CrossWindowCoordinates = Coordinates, Theme = new FluentTheme(ElementTheme.Light) };
            var panel = new LayoutPanel(Tools); panel.Children.Add(Documents); Manager.Layout = new() { RootPanel = panel };
            if (tools)
            {
                Source = Enumerable.Range(0, 3).Select(i => (LayoutContent)new LayoutAnchorable { Title = "Tool " + i, ContentId = "tool" + i,
                    Content = new TextBox { Text = "Unsaved buffer " + i, AcceptsReturn = true }, FloatingLeft = 180, FloatingTop = 160, FloatingWidth = 480, FloatingHeight = 320 }).ToArray();
                var first = new LayoutAnchorablePane((LayoutAnchorable)Source[0]); first.Children.Add((LayoutAnchorable)Source[1]);
                Group = new() { Orientation = Orientation.Vertical }; Group.Children.Add(first); Group.Children.Add(new LayoutAnchorablePane((LayoutAnchorable)Source[2]));
                Floating = new LayoutAnchorableFloatingWindow { RootPanel = Group };
            }
            else
            {
                var document = new LayoutDocument { Title = "Floating document", ContentId = "floating", Content = new TextBox { Text = "Unsaved document", AcceptsReturn = true },
                    FloatingLeft = 180, FloatingTop = 160, FloatingWidth = 480, FloatingHeight = 320 };
                Source = [document]; Floating = new LayoutDocumentFloatingWindow { RootDocument = document };
            }
            Manager.Layout.FloatingWindows.Add(Floating); Source[0].IsActive = true;
            _window = new() { Content = Manager, Title = "UnoDock native docking acceptance" };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Move(new() { X = 40, Y = 40 }); _window.AppWindow.Resize(new() { Width = 1100, Height = 780 }); _window.Activate();
        }
        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualHeight > 0);
            Manager.Refresh();
            await Wait(() => Manager.FloatingWindows.Count() == 1);
            Control = Manager.FloatingWindows.Single(); Control.MessageFilterFailed += (_, error) => NativeFailures.Add(error);
            await Wait(() => Control.IsLoaded && Control.ActualWidth > 0 && Control.ActualHeight > 0 && (!_native || Control.NativeWindow != null));
            Manager.UpdateLayout(); Control.UpdateLayout(); await Task.Delay(50);
        }
        internal SessionLease Session() => new(Manager, Control);
        internal DockDropPlan? InsidePlan() => DockDropPlan.Create(Source[0], Tools, DropTargetType.AnchorablePaneDockInside, new(0, 0, 1000, 600));
        internal Point DocumentCenter()
        {
            var view = (FrameworkElement)Call(Surface, "GetView", Documents)!;
            return view.TransformToVisual(Surface).TransformPoint(new(view.ActualWidth / 2, view.ActualHeight / 2));
        }
        internal async Task CoverTarget(Point target)
        {
            var desired = Coordinates.ToScreen(Surface, target);
            var current = Coordinates.ToScreen(Control, new(Control.ActualWidth / 2, Control.ActualHeight / 2));
            var origin = (Point)CallStatic(typeof(DesktopWindowCoordinates), "NativeOrigin", Control.NativeWindow)!;
            CallStatic(typeof(DesktopWindowCoordinates), "MoveNative", Control.NativeWindow, new Point(origin.X + desired.X - current.X, origin.Y + desired.Y - current.Y));
            Control.NativeWindow!.Activate(); await Task.Delay(100);
        }
        public void Dispose()
        {
            try { Manager.Dispose(); }
            finally { _window.Content = null; _window.Close(); _registration.Dispose(); Coordinates.Dispose(); }
        }
    }
    private sealed class SessionLease : IDisposable
    {
        private readonly object _session;
        internal SessionLease(DockingManager manager, LayoutFloatingWindowControl window)
        {
            var type = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.FloatingDockSession", true)!;
            _session = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [manager, window], null)!;
        }
        internal bool Execute(DockDropPlan? plan) => (bool)Call(_session, "Execute", plan)!;
        public void Dispose() => ((IDisposable)_session).Dispose();
    }
    private static T Field<T>(object value, string name)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) is { } field) return (T)field.GetValue(value)!;
        throw new MissingFieldException(name);
    }
    private static object? CallStatic(Type type, string name, params object?[] args) => Invoke(type, null, name, args, BindingFlags.Static);
    private static object? Call(object target, string name, params object?[] args) => Invoke(target.GetType(), target, name, args, BindingFlags.Instance);
    private static object? Invoke(Type type, object? target, string name, object?[] args, BindingFlags kind)
    {
        try { return type.GetMethods(kind | BindingFlags.Public | BindingFlags.NonPublic).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 100 && !ready(); i++) await Task.Delay(20);
        Check.True(ready(), "Desktop floating host did not settle within the bounded wait.");
    }
}
