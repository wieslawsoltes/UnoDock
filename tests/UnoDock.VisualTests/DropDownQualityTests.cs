using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Compatibility;

namespace UnoDock.Testing;

/// <summary>Independent product regressions on actual Uno controls, not evidence
/// of original WPF event ordering. Native input runs only on an opted-in XTEST host.</summary>
public static class DropDownQualityTests
{
    private sealed class Area : DropDownControlArea
    {
        internal int Downs, Ups;
        internal string LastKey = "none";
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        { LastKey = e.Key.ToString(); base.OnKeyDown(e); }
        internal bool Veto;
        protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
        { Downs++; Check.Equal(DockMouseButton.Right, e.ChangedButton); base.OnMouseRightButtonDown(e); }
        protected override void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e)
        { Ups++; if (Veto) e.Handled = true; base.OnPreviewMouseRightButtonUp(e); }
    }
    private sealed class Button : Xceed.Wpf.AvalonDock.Controls.DropDownButton
    {
        internal bool Veto;
        internal void InvokeClick() => OnClick();
        protected override void OnClick() { if (!Veto) base.OnClick(); }
    }
    private sealed class Trigger
    {
        internal readonly Button? Button;
        internal readonly Area? Area;
        internal Control View => (Control?)Button ?? Area!;
        internal Trigger(bool area)
        {
            if (area) Area = new() { Content = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Child = new TextBlock { Text = "Right-click area", Margin = new(12) } } };
            else Button = new() { Content = "Document menu" };
            View.Width = 380; View.Height = 60; View.IsTabStop = true;
        }
        internal MenuFlyout Menu
        {
            get => (Button != null ? Button.DropDownContextMenu : Area!.DropDownContextMenu) ?? throw new InvalidOperationException("No test menu is configured.");
            set { if (Button != null) Button.DropDownContextMenu = value; else Area!.DropDownContextMenu = value; }
        }
        internal object? Context
        {
            set { if (Button != null) Button.DropDownContextMenuDataContext = value; else Area!.DropDownContextMenuDataContext = value; }
        }
        internal void Open() { if (Button != null) Button.OpenDropDown(); else Area!.OpenDropDown(); }
        internal void Close() { if (Button != null) Button.CloseDropDown(); else Area!.CloseDropDown(); }
        internal void CheckClosed() { if (Button != null) Check.Equal(false, Button.IsChecked); }
    }

    public static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var root = new StackPanel { Spacing = 12, Margin = new(20) };
        var window = new Window { Title = "UnoDock dropdown contracts", Content = root };
        window.AppWindow.Resize(new() { Width = 600, Height = 500 }); window.Activate();
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        foreach (var area in new[] { false, true })
        {
            Add("open/close scopes context to retained row", async t =>
            {
                var menu = NewMenu(out var row); var context = new object(); t.Menu = menu; t.Context = context;
                t.Open(); await Wait(() => menu.IsOpen); Check.Same(context, row.DataContext);
                t.Close(); await Wait(() => !menu.IsOpen); Check.Unset(row); t.CheckClosed();
            });
            Add("closed context changes do not touch menu rows", t =>
            {
                t.Menu = NewMenu(out var row); t.Context = new object(); t.View.DataContext = new object();
                Check.Unset(row); return Task.CompletedTask;
            });
            Add("replacement from DataContextChanged cannot open old menu", async t =>
            {
                var old = NewMenu(out var row); var replacement = NewMenu(out _); var context = new object();
                var opens = 0; old.Opened += (_, _) => opens++;
                row.DataContextChanged += (_, _) => { if (ReferenceEquals(row.DataContext, context)) t.Menu = replacement; };
                t.Menu = old; t.Context = context; t.Open(); await Task.Delay(100);
                Check.Equal(0, opens); Check.False(old.IsOpen); Check.False(replacement.IsOpen); Check.Unset(row); t.CheckClosed();
                t.Open(); await Wait(() => replacement.IsOpen);
            });
            Add("close from DataContextChanged cancels before native showing", async t =>
            {
                var menu = NewMenu(out var row); var context = new object(); var opens = 0;
                menu.Opened += (_, _) => opens++;
                row.DataContextChanged += (_, _) => { if (ReferenceEquals(row.DataContext, context)) t.Close(); };
                t.Menu = menu; t.Context = context; t.Open(); await Task.Delay(90);
                Check.Equal(0, opens); Check.Unset(row); t.CheckClosed();
            });
            Add("latest nested context request wins", async t =>
            {
                var menu = NewMenu(out var row); var first = new object(); var latest = new object();
                row.DataContextChanged += (_, _) => { if (ReferenceEquals(row.DataContext, first)) t.Context = latest; };
                t.Menu = menu; t.Context = first; t.Open(); await Wait(() => menu.IsOpen);
                Check.Same(latest, row.DataContext); Check.Same(row, menu.Items[0]);
            });
            Add("explicit context updates an open retained row", async t =>
            {
                var menu = NewMenu(out var row); var first = new object(); var latest = new object(); t.Menu = menu; t.Context = first;
                t.Open(); await Wait(() => menu.IsOpen); t.Context = latest; Check.Same(latest, row.DataContext); Check.Same(row, menu.Items[0]);
            });
            Add("inherited trigger context is live only while open", async t =>
            {
                var menu = NewMenu(out var row); var first = new object(); var latest = new object(); t.Menu = menu; t.View.DataContext = first;
                t.Open(); await Wait(() => menu.IsOpen); Check.Same(first, row.DataContext);
                t.View.DataContext = latest; Check.Same(latest, row.DataContext);
                t.Close(); await Wait(() => !menu.IsOpen); t.View.DataContext = new object(); Check.Unset(row);
            });
            Add("explicit row context survives opening and cleanup", async t =>
            {
                var menu = NewMenu(out var row); var local = new object(); row.DataContext = local; t.Menu = menu; t.Context = new object();
                t.Open(); await Wait(() => menu.IsOpen); Check.Same(local, row.DataContext);
                t.Close(); await Wait(() => !menu.IsOpen); Check.Same(local, row.ReadLocalValue(FrameworkElement.DataContextProperty));
            });
            Add("application row changes while open survive cleanup", async t =>
            {
                var menu = NewMenu(out var row); t.Menu = menu; t.Context = new object(); t.Open(); await Wait(() => menu.IsOpen);
                var local = new object(); row.DataContext = local; t.Close(); await Wait(() => !menu.IsOpen); Check.Same(local, row.DataContext);
            });
            Add("disable closes current menu and releases row contexts", async t =>
            {
                var menu = NewMenu(out var row); t.Menu = menu; t.Context = new object(); t.Open(); await Wait(() => menu.IsOpen);
                t.View.IsEnabled = false; await Wait(() => !menu.IsOpen); Check.Unset(row); t.CheckClosed();
                t.View.IsEnabled = true; t.Open(); await Wait(() => menu.IsOpen);
            });
            Add("unloading trigger cancels menu and permits later reattachment", async t =>
            {
                var menu = NewMenu(out var row); t.Menu = menu; t.Context = new object(); t.Open(); await Wait(() => menu.IsOpen);
                root.Children.Remove(t.View); await Wait(() => !menu.IsOpen); Check.Unset(row); t.CheckClosed();
                root.Children.Add(t.View); await Wait(() => t.View.IsLoaded); t.Open(); await Wait(() => menu.IsOpen);
            });
            Add("menu replacement closes old without auto-opening new", async t =>
            {
                var old = NewMenu(out var row); var next = NewMenu(out _); t.Menu = old; t.Context = new object(); t.Open(); await Wait(() => old.IsOpen);
                t.Menu = next; await Wait(() => !old.IsOpen); Check.False(next.IsOpen); Check.Unset(row); t.CheckClosed();
                t.Open(); await Wait(() => next.IsOpen);
            });
            Add("Opened callback cannot resurrect cancelled state", async t =>
            {
                var menu = NewMenu(out var row); menu.Opened += (_, _) => t.Close(); t.Menu = menu; t.Context = new object();
                t.Open(); await Task.Delay(150); Check.False(menu.IsOpen); Check.Unset(row); t.CheckClosed();
            });
            Add("Opening callback replacement cannot leave the old menu open", async t =>
            {
                var menu = NewMenu(out var row); var next = NewMenu(out _); menu.Opening += (_, _) => t.Menu = next;
                t.Menu = menu; t.Context = new object(); t.Open(); await Task.Delay(150); Check.False(menu.IsOpen); Check.False(next.IsOpen); Check.Unset(row); t.CheckClosed();
            });
            Add("native Hide releases the opening scope", async t =>
            {
                var menu = NewMenu(out var row); t.Menu = menu; t.Context = new object(); t.Open(); await Wait(() => menu.IsOpen);
                menu.Hide(); await Wait(() => !menu.IsOpen && ReferenceEquals(row.ReadLocalValue(FrameworkElement.DataContextProperty), DependencyProperty.UnsetValue)); t.CheckClosed();
            });
            Add("failed context assignment cleans up and can be retried", async t =>
            {
                var menu = NewMenu(out var row); var context = new object(); var fail = true;
                row.DataContextChanged += (_, _) => { if (fail && ReferenceEquals(row.DataContext, context)) { fail = false; throw new InvalidOperationException("owned failure"); } };
                t.Menu = menu; t.Context = context;
                Check.Throws<InvalidOperationException>(t.Open); Check.False(menu.IsOpen); Check.Unset(row); t.CheckClosed();
                t.Open(); await Wait(() => menu.IsOpen); Check.Same(context, row.DataContext);
            });
            Add("shared menu transfers ownership between two triggers", async t =>
            {
                var next = new Trigger(!area); root.Children.Add(next.View); await Wait(() => next.View.IsLoaded);
                try
                {
                    var menu = NewMenu(out var row); var first = new object(); var last = new object();
                    t.Menu = next.Menu = menu; t.Context = first; next.Context = last;
                    t.Open(); await Wait(() => menu.IsOpen); next.Open(); await Wait(() => menu.IsOpen && ReferenceEquals(row.DataContext, last));
                    t.Close(); root.Children.Remove(t.View); await Task.Delay(80); Check.True(menu.IsOpen); Check.Same(last, row.DataContext); t.CheckClosed();
                }
                finally { next.Close(); root.Children.Remove(next.View); }
            });
            Add("shared takeover during context cleanup does not clear the new owner", async t =>
            {
                var next = new Trigger(!area); root.Children.Add(next.View); await Wait(() => next.View.IsLoaded);
                try
                {
                    var menu = NewMenu(out var row); var first = new object(); var last = new object(); var armed = false;
                    t.Menu = next.Menu = menu; t.Context = first; next.Context = last;
                    row.DataContextChanged += (_, _) => { if (armed && !ReferenceEquals(row.DataContext, first)) { armed = false; next.Open(); } };
                    t.Open(); await Wait(() => menu.IsOpen); armed = true; t.Close();
                    await Wait(() => menu.IsOpen && ReferenceEquals(row.DataContext, last)); t.CheckClosed();
                }
                finally { next.Close(); root.Children.Remove(next.View); }
            });
            Add("five open/close cycles preserve row identity and release contexts", async t =>
            {
                var menu = NewMenu(out var row); t.Menu = menu;
                for (var i = 0; i < 5; i++)
                {
                    var value = new object(); t.Context = value; t.Open(); await Wait(() => menu.IsOpen); Check.Same(value, row.DataContext);
                    t.Close(); await Wait(() => !menu.IsOpen); Check.Unset(row); Check.Same(row, menu.Items[0]);
                }
            });
            Add("null menu and detached opening are inert", async t =>
            {
                t.Open(); t.CheckClosed(); t.Menu = NewMenu(out var row); root.Children.Remove(t.View); await Wait(() => !t.View.IsLoaded);
                t.Open(); Check.False(t.Menu.IsOpen); Check.Unset(row); t.CheckClosed();
            });

            void Add(string name, Func<Trigger, Task> body) => tests.Test((area ? "area: " : "button: ") + name, async () =>
            {
                var trigger = new Trigger(area); root.Children.Add(trigger.View); window.Activate();
                try { await Wait(() => trigger.View.IsLoaded); root.UpdateLayout(); await body(trigger); }
                finally { trigger.Close(); root.Children.Remove(trigger.View); await Task.Delay(30); }
            });
        }
        tests.Test("Checked callback may reject opening", async () =>
        {
            var t = new Trigger(false); root.Children.Add(t.View);
            try
            {
                await Wait(() => t.View.IsLoaded); t.Menu = NewMenu(out var row); t.Context = new object();
                t.Button!.Checked += (_, _) => t.Button.IsChecked = false;
                t.Open(); await Task.Delay(120); Check.False(t.Menu.IsOpen); Check.Unset(row); t.CheckClosed();
            }
            finally { t.Close(); root.Children.Remove(t.View); }
        });
        tests.Test("Click override may veto and remains callable after acceptance", async () =>
        {
            var t = new Trigger(false); root.Children.Add(t.View);
            try
            {
                await Wait(() => t.View.IsLoaded); t.Menu = NewMenu(out _); t.Button!.Veto = true;
                t.Button.InvokeClick(); Check.False(t.Menu.IsOpen); t.Button.Veto = false; t.Button.InvokeClick(); await Wait(() => t.Menu.IsOpen);
                t.Button.InvokeClick(); await Wait(() => !t.Menu.IsOpen);
            }
            finally { t.Close(); root.Children.Remove(t.View); }
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            foreach (var veto in new[] { false, true }) tests.Test("XTEST right press/release visits hooks once; veto=" + veto, async () =>
            {
                var t = new Trigger(true); root.Children.Add(t.View);
                try
                {
                    await Wait(() => t.View.IsLoaded); window.Activate(); root.UpdateLayout(); await Task.Delay(80);
                    t.Menu = NewMenu(out _); t.Area!.Veto = veto;
                    var opens = 0; t.Menu.Opened += (_, _) => opens++;
                    using var input = new X11TestInput(); input.MoveTo(t.View, new(30, 25)); await Task.Delay(50);
                    input.Press(3); await Task.Delay(50); input.Release(3); await Task.Delay(180);
                    Check.Equal(1, t.Area.Downs); Check.Equal(1, t.Area.Ups); Check.Equal(veto ? 0 : 1, opens); Check.Equal(!veto, t.Menu.IsOpen);
                }
                finally { t.Close(); root.Children.Remove(t.View); }
            });
            tests.Test("XTEST keyboard context menu key and Escape", async () =>
            {
                var t = new Trigger(true); root.Children.Add(t.View);
                try
                {
                    await Wait(() => t.View.IsLoaded); window.Activate(); root.UpdateLayout(); await Task.Delay(80);
                    Check.True(t.View.Focus(FocusState.Keyboard), "Context area refused keyboard focus.");
                    await Wait(() => ReferenceEquals(FocusManager.GetFocusedElement(t.View.XamlRoot!), t.View));
                    t.Menu = NewMenu(out _); using var input = new X11TestInput(); input.KeyPress(0xff67);
                    try { await Wait(() => t.Menu.IsOpen); }
                    catch (Exception error) { throw new InvalidOperationException("Application key did not open menu. Last native key: " + t.Area!.LastKey, error); }
                    await Task.Delay(80); input.Escape();
                    await Wait(() => !t.Menu.IsOpen);
                    Check.Equal(0, t.Area!.Downs); Check.Equal(0, t.Area.Ups);
                }
                finally { t.Close(); root.Children.Remove(t.View); }
            });
        }
        DropDownTransitionTests.Register(tests, root, window);
        try { return await tests.Run(output, "dropdown-quality"); }
        finally { window.Close(); }
    }
    private static MenuFlyout NewMenu(out MenuFlyoutItem row)
    { var menu = new MenuFlyout(); row = new() { Text = "Close document" }; menu.Items.Add(row); return menu; }
    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 80; i++) { if (condition()) return; await Task.Delay(20); }
        Check.True(condition(), "Dropdown condition did not converge within the bounded wait.");
    }
    // Keep local-value ownership assertions separate from inherited presenter context.
    private static class Check
    {
        internal static void True(bool value, string? message = null) => UnoDock.Testing.Check.True(value, message);
        internal static void False(bool value) => UnoDock.Testing.Check.False(value);
        internal static void Equal<T>(T expected, T actual) => UnoDock.Testing.Check.Equal(expected, actual);
        internal static void Same(object? a, object? b) => UnoDock.Testing.Check.Same(a, b);
        internal static T Throws<T>(Action body) where T : Exception => UnoDock.Testing.Check.Throws<T>(body);
        internal static void Unset(FrameworkElement row) => Same(DependencyProperty.UnsetValue, row.ReadLocalValue(FrameworkElement.DataContextProperty));
    }
}
