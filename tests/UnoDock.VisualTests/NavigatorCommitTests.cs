using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class NavigatorCommitTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        // These are navigator transaction tests, not native-window creation tests.
        // Keep one real shell and replace/dispose the complete docking tree per case.
        // The pinned X11 host retains closed native renderer state; one shell per
        // mutation exhausted CI memory before later suites could execute.
        var window = new Window { Title = "UnoDock navigator commit acceptance" };
        window.AppWindow.Resize(new() { Width = 1100, Height = 800 });
        var mutations = new (string Name, Action<Fixture> Mutate)[]
        {
            ("disabled target", f => f.Target.IsEnabled = false),
            ("disabled target ABA", f => { f.Target.IsEnabled = false; f.Target.IsEnabled = true; }),
            ("closed document or hidden tool", f => { if (f.Target is LayoutDocument doc) doc.Close(); else ((LayoutAnchorable)f.Target).Hide(); }),
            ("removed target", f => f.Remove()),
            ("reinserted target ABA", f => { f.Remove(); f.Insert(); }),
            ("replaced workspace", f => f.Host.Layout = Replacement()),
            ("replaced workspace ABA", f => { f.Host.Layout = Replacement(); f.Host.Layout = f.Root; }),
            ("replaced command", f => f.Item.ActivateCommand = new ProbeCommand(() => { }, () => true)),
            ("replaced command ABA", f => { var command = f.Item.ActivateCommand; f.Item.ActivateCommand = null; f.Item.ActivateCommand = command; }),
            ("changed preview", f => f.Nav.PreviewDocument((LayoutDocumentItem)f.Host.GetLayoutItemFromModel(f.A))),
            ("changed preview ABA", f => { f.Nav.PreviewDocument((LayoutDocumentItem)f.Host.GetLayoutItemFromModel(f.A)); f.SelectTarget(); }),
            ("disabled manager", f => f.Host.IsEnabled = false),
            ("disabled manager ABA", f => { f.Host.IsEnabled = false; f.Host.IsEnabled = true; }),
            ("disabled navigator ABA", f => { f.Nav.IsEnabled = false; f.Nav.IsEnabled = true; }),
            ("disposed manager", f => f.Host.Dispose())
        };
        foreach (var tool in new[] { false, true })
        foreach (var close in new[] { false, true })
        {
            var prefix = (tool ? "tool" : "document") + (close ? " close" : " direct");
            foreach (var mutation in mutations)
                Add(prefix + ": veto stale request after " + mutation.Name, tool, async f =>
                {
                    var executed = 0; var queried = 0;
                    f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => { queried++; mutation.Mutate(f); return true; });
                    f.Commit(close);
                    Check.Equal(1, queried); Check.Equal(0, executed);
                    if (close) Check.True(f.Current == null);
                    await Task.CompletedTask;
                });
            Add(prefix + ": normal activation executes exactly once", tool, async f =>
            {
                var executed = 0;
                f.Item.ActivateCommand = new ProbeCommand(() => { executed++; f.Target.IsActive = true; }, () => true);
                f.Commit(close); Check.Equal(1, executed); Check.Same(f.Target, f.Host.Layout.ActiveContent);
                await Task.CompletedTask;
            });
            Add(prefix + ": custom command remains authoritative", tool, async f =>
            {
                var executed = 0;
                f.Item.ActivateCommand = new ProbeCommand(() => { executed++; f.A.IsActive = true; }, () => true);
                f.Commit(close); Check.Equal(1, executed); Check.Same(f.A, f.Host.Layout.ActiveContent);
                await Task.CompletedTask;
            });
            Add(prefix + ": false CanExecute never invokes Execute", tool, async f =>
            {
                var executed = 0;
                f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => false);
                f.Commit(close); Check.Equal(0, executed);
                await Task.CompletedTask;
            });
            Add(prefix + ": harmless title changes do not veto activation", tool, async f =>
            {
                var executed = 0;
                f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => { f.Target.Title = "Renamed during query"; return true; });
                f.Commit(close); Check.Equal(1, executed);
                await Task.CompletedTask;
            });
            Add(prefix + ": recursive query cannot double-execute", tool, async f =>
            {
                var queried = 0; var executed = 0;
                f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => { queried++; Call(f.Nav, "CommitSelection"); return true; });
                f.Commit(close); Check.Equal(1, queried); Check.Equal(1, executed);
                await Task.CompletedTask;
            });
            foreach (var fromQuery in new[] { false, true })
                Add(prefix + ": throwing " + (fromQuery ? "CanExecute" : "Execute") + " leaves a usable next session", tool, async f =>
                {
                    var failure = new InvalidOperationException("Application activation failure");
                    f.Item.ActivateCommand = new ProbeCommand(() => { if (!fromQuery) throw failure; }, () => fromQuery ? throw failure : true);
                    Exception? observed = null;
                    try { f.Commit(close); } catch (Exception error) { observed = error; }
                    Check.Same(failure, observed);
                    Surface(f.Host, "CloseNavigator", false);
                    f.Show(); await Ready(f.Nav); f.SelectTarget();
                    var executed = 0; f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => true);
                    f.Commit(true); Check.Equal(1, executed); Check.True(f.Current == null);
                });
        }
        Add("cancelled navigator cannot commit a retained selection", false, async f =>
        {
            var executed = 0; f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => true);
            f.Commit(false); Check.Equal(1, executed);
            Surface(f.Host, "CloseNavigator", false);
            Call(f.Nav, "CommitSelection"); Check.Equal(1, executed);
            await Task.CompletedTask;
        });
        Add("stale input cannot close a replacement navigator", false, async f =>
        {
            var old = f.Nav; Surface(f.Host, "CloseNavigator", false); f.Show(); await Ready(f.Nav);
            Call(old, "CloseNavigatorForInput", true); Check.Same(f.Nav, f.Current);
        });
        Add("CanExecute opening another navigator invalidates the closing activation", false, async f =>
        {
            var executed = 0; NavigatorWindow? next = null;
            f.Item.ActivateCommand = new ProbeCommand(() => executed++, () =>
            {
                next = new(f.Host); Surface(f.Host, "ShowNavigator", next); return true;
            });
            f.Commit(true); Check.Equal(0, executed); Check.Same(next, f.Current);
            await Ready(next!);
        });
        Add("Execute opening another navigator does not lose its keyboard focus", false, async f =>
        {
            NavigatorWindow? next = null;
            f.Item.ActivateCommand = new ProbeCommand(() => { next = new(f.Host); Surface(f.Host, "ShowNavigator", next); }, () => true);
            f.Commit(true); Check.Same(next, f.Current); await Ready(next!); await Task.Delay(60);
            Check.True(Within(next!, FocusManager.GetFocusedElement(f.Host.XamlRoot!) as DependencyObject), "Old close stole focus from the replacement navigator.");
        });
        Add("default close restores focus to the activated document editor", false, async f =>
        {
            f.Commit(true); await Task.Delay(100);
            Check.Same(f.Target, f.Host.Layout.ActiveContent);
            Check.True(Within((FrameworkElement)f.Target.Content!, FocusManager.GetFocusedElement(f.Host.XamlRoot!) as DependencyObject));
        });
        Add("opening callbacks may replace the navigator without old focus work", false, async f =>
        {
            Surface(f.Host, "CloseNavigator", false); var old = new NavigatorWindow(f.Host); NavigatorWindow? next = null;
            var token = old.RegisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, (_, _) =>
            {
                Surface(f.Host, "CloseNavigator", false); next = new(f.Host); Surface(f.Host, "ShowNavigator", next);
            });
            try
            {
                Surface(f.Host, "ShowNavigator", old); Check.Same(next, f.Current); await Ready(next!);
                Check.False(old.IsLoaded); Check.True(Within(next!, FocusManager.GetFocusedElement(f.Host.XamlRoot!) as DependencyObject));
            }
            finally { old.UnregisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, token); }
        });
        Add("throwing initialization publication removes its reservation and allows retry", false, async f =>
        {
            Surface(f.Host, "CloseNavigator", false); var broken = new NavigatorWindow(f.Host);
            var failure = new InvalidOperationException("Documents observer failed");
            var token = broken.RegisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, (_, _) => throw failure);
            try
            {
                Exception? observed = null;
                try { Surface(f.Host, "ShowNavigator", broken); } catch (Exception error) { observed = error; }
                Check.Same(failure, observed); Check.True(f.Current == null); Check.False(broken.IsLoaded);
            }
            finally { broken.UnregisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, token); }
            f.Show(); await Ready(f.Nav); Check.Same(f.Nav, f.Current);
        });
        Add("foreign navigator rejected without disturbing current session", false, async f =>
        {
            using var other = new DockingManager(); var foreign = new NavigatorWindow(other);
            Check.Throws<ArgumentException>(() => Surface(f.Host, "ShowNavigator", foreign));
            Check.Same(f.Nav, f.Current); await Task.CompletedTask;
        });
        Add("captured close intent is one-use", false, async f =>
        {
            var executed = 0; f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => true);
            var action = (Action)Call(f.Nav, "CaptureSelectionCommit", (Func<bool>)(() => true), true)!;
            Surface(f.Host, "CloseNavigator", false);
            action(); action(); Check.Equal(1, executed); await Task.CompletedTask;
        });
        Add("captured close intent cannot cross a reinitialized navigator session", false, async f =>
        {
            var executed = 0; f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => true);
            var old = f.Nav;
            var action = (Action)Call(old, "CaptureSelectionCommit", (Func<bool>)(() => true), true)!;
            Surface(f.Host, "CloseNavigator", false); Surface(f.Host, "ShowNavigator", old); await Ready(old);
            Surface(f.Host, "CloseNavigator", false); action(); Check.Equal(0, executed);
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            Add("XTEST: Enter commits the selected document through the guarded close", false, async f =>
            {
                using var input = new X11TestInput(); await FocusForNativeKeys(f.Nav, input);
                input.KeyPress(0xff0d); await Wait(() => f.Current == null);
                Check.Same(f.Target, f.Host.Layout.ActiveContent);
            });
            Add("XTEST: Escape closes without invoking selected activation", false, async f =>
            {
                var executed = 0; f.Item.ActivateCommand = new ProbeCommand(() => executed++, () => true);
                using var input = new X11TestInput(); await FocusForNativeKeys(f.Nav, input);
                input.Escape(); await Wait(() => f.Current == null); Check.Equal(0, executed); Check.Same(f.A, f.Host.Layout.ActiveContent);
            });
        }
        try { return await tests.Run(output, "navigator-commit"); }
        finally { window.Content = null; window.Close(); }

        void Add(string name, bool tool, Func<Fixture, Task> body) => tests.Test(name, async () =>
        {
            using var fixture = new Fixture(tool, window);
            await Wait(() => fixture.Host.IsLoaded && fixture.Host.ActualWidth > 0);
            fixture.Show(); await Ready(fixture.Nav); fixture.SelectTarget();
            await body(fixture);
        });
    }

    private static async Task FocusForNativeKeys(NavigatorWindow navigator, X11TestInput input)
    {
        // Establish native input focus in this shell, not just XAML focus. Xvfb
        // has no window manager to honour an activation request; the pointer may
        // still be over the other application's main window after previous cases.
        // Hit only the empty outer border, never a selectable row.
        input.MoveTo(navigator, new(2, 2));
        input.Press(); input.Release();
        await Task.Delay(50);
        Check.True(navigator.Focus(FocusState.Keyboard));
        await Task.Delay(50);
    }

    private static LayoutRoot Replacement() => new() { RootPanel = new(new LayoutDocumentPane(new LayoutDocument { ContentId = "replacement", Title = "Replacement" })) };
    private static bool Within(FrameworkElement root, DependencyObject? value)
    {
        for (var current = value; current != null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, root)) return true;
        return false;
    }
    private static async Task Ready(NavigatorWindow navigator)
    { await Wait(() => navigator.IsLoaded && navigator.ActualHeight > 0); await Task.Delay(25); }
    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(20);
        Check.True(predicate(), "Navigator condition did not converge within the bounded wait.");
    }
    private static object? Call(object target, string name, params object?[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static object? Surface(DockingManager host, string name, params object?[] args) => Call(GetSurface(host), name, args);
    private static object GetSurface(DockingManager host) => typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
    private sealed class ProbeCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Host = new() { Width = 1000, Height = 640, FloatingWindowMode = FloatingWindowMode.InSurface };
        internal readonly LayoutDocument A = new() { ContentId = "A", Title = "First.cs", Content = new TextBox { Text = "First buffer", AcceptsReturn = true } };
        internal readonly LayoutDocument B = new() { ContentId = "B", Title = "Second.cs", Content = new TextBox { Text = "Second buffer", AcceptsReturn = true } };
        internal readonly LayoutAnchorable Tool = new() { ContentId = "tool", Title = "Properties", Content = new TextBox { Text = "Tool buffer", AcceptsReturn = true } };
        internal readonly LayoutRoot Root;
        private readonly LayoutDocumentPane _documents;
        private readonly LayoutAnchorablePane _tools;
        private readonly Window _window;
        internal readonly LayoutContent Target;
        internal NavigatorWindow Nav = null!;
        internal LayoutItem Item => Host.GetLayoutItemFromModel(Target);
        internal NavigatorWindow? Current => typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Host) is { } surface
            ? surface.GetType().GetField("_navigator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(surface) as NavigatorWindow : null;
        internal Fixture(bool tool, Window window)
        {
            Target = tool ? Tool : B;
            _documents = new(A); _documents.Children.Add(B); _tools = new(Tool) { DockWidth = new(200) };
            var panel = new LayoutPanel(_tools); panel.Children.Add(_documents); Root = new() { RootPanel = panel };
            Host.Layout = Root; A.IsActive = true;
            _window = window; _window.Content = Host; _window.Activate();
        }
        internal void Show() { Nav = new(Host); Surface(Host, "ShowNavigator", Nav); }
        internal void SelectTarget()
        { if (Item is LayoutDocumentItem doc) Nav.PreviewDocument(doc); else Nav.PreviewAnchorable((LayoutAnchorableItem)Item); }
        internal void Remove() { if (Target is LayoutDocument doc) _documents.Children.Remove(doc); else _tools.Children.Remove((LayoutAnchorable)Target); }
        internal void Insert() { if (Target is LayoutDocument doc) _documents.Children.Add(doc); else _tools.Children.Add((LayoutAnchorable)Target); }
        internal void Commit(bool close) { if (close) Surface(Host, "CloseNavigator", true); else Call(Nav, "CommitSelection"); }
        public void Dispose()
        {
            try { Host.Dispose(); }
            finally { if (ReferenceEquals(_window.Content, Host)) _window.Content = null; }
        }
    }
}
