using Microsoft.UI.Xaml.Data;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Compatibility;

namespace UnoDock.Testing;

public static class DropDownQualityTests
{
    public static async Task<int> Run(string output)
    {
        var window = new Window { Title = "Dropdown contract tests" };
        var host = new StackPanel { Padding = new Thickness(24), Spacing = 16 };
        window.Content = host;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 640, Height = 500 });
        window.Activate();
        await Task.Delay(150);
        var tests = new TestRunner();
        void Add(string name, Func<Fixture, Task> body) => tests.Test(name, async () =>
        {
            using var fixture = new Fixture(host);
            window.Activate(); host.UpdateLayout();
            await Task.Delay(65);
            await body(fixture);
        });
        void Sync(string name, Action<Fixture> body) => Add(name, f => { body(f); return Task.CompletedTask; });

        Sync("dropdown attaches and releases its temporary context", f =>
        { f.A.Invoke(); f.Owner(f.A, f.ContextA); f.A.Invoke(); f.Closed(); });
        Sync("shared dropdown transfer changes the actual popup owner", f =>
        { f.A.Invoke(); f.B.Invoke(); f.Owner(f.B, f.ContextB); Check.False(f.A.IsChecked == true); });
        Sync("unloading the old trigger cannot close its successor", f =>
        { f.A.Invoke(); f.B.Invoke(); host.Children.Remove(f.A); f.Owner(f.B, f.ContextB); });
        Sync("disabling the old trigger cannot close its successor", f =>
        { f.A.Invoke(); f.B.Invoke(); f.A.IsEnabled = false; f.Owner(f.B, f.ContextB); });
        Sync("disabling the active trigger closes and cleans its menu", f =>
        { f.A.Invoke(); f.A.IsEnabled = false; f.Closed(); });
        Sync("replacing the active menu closes only the old configured menu", f =>
        {
            f.A.Invoke(); var next = new MenuFlyout(); next.Items.Add(new MenuFlyoutItem { Text = "Next" });
            f.A.DropDownContextMenu = next; f.Closed(); Check.False(next.IsOpen);
            f.A.Invoke(); Check.True(next.IsOpen); next.Hide();
        });
        Sync("dropdown explicit context refresh preserves popup and row identity", f =>
        {
            f.A.Invoke(); var next = new object(); f.A.DropDownContextMenuDataContext = next;
            f.Owner(f.A, next); Check.Same(f.First, f.Menu.Items[0]);
        });
        Sync("inherited trigger context refresh reaches open menu rows", f =>
        { f.A.Invoke(); var next = new object(); f.A.DataContext = next; f.Owner(f.A, next); });
        Sync("explicit dropdown context takes priority over inherited context", f =>
        {
            f.A.DropDownContextMenuDataContext = f.ContextB; f.A.Invoke();
            f.A.DataContext = new object(); f.Owner(f.A, f.ContextB);
            f.A.ClearValue(DropDownButton.DropDownContextMenuDataContextProperty); f.Owner(f.A, f.A.DataContext!);
        });
        Sync("application local row context survives shared transfer and close", f =>
        {
            var app = new object(); f.Second.DataContext = app; f.A.Invoke(); f.B.Invoke(); f.B.Invoke();
            Check.Same(app, f.Second.DataContext); Unassigned(f.First);
        });
        Sync("application row binding survives open refresh and close", f =>
        {
            var value = new object(); var binding = new Binding { Source = value };
            f.Second.SetBinding(FrameworkElement.DataContextProperty, binding);
            f.A.Invoke(); f.A.DataContext = new object(); f.A.Invoke();
            Check.Same(value, f.Second.DataContext);
            Check.Same(binding, f.Second.GetBindingExpression(FrameworkElement.DataContextProperty).ParentBinding);
        });
        Sync("opening callback can remove the configured menu without a stale popup", f =>
        { f.Menu.Opening += (_, _) => f.A.DropDownContextMenu = null; f.A.Invoke(); f.Closed(); });
        Sync("opening callback transfers ownership without recursive ShowAt", f =>
        {
            var depth = 0; var maximum = 0; var redirected = false;
            f.Menu.Opening += (_, _) =>
            {
                depth++; maximum = Math.Max(maximum, depth);
                try { if (!redirected) { redirected = true; f.B.Invoke(); } }
                finally { depth--; }
            };
            f.A.Invoke(); f.Owner(f.B, f.ContextB); Check.Equal(1, maximum);
        });
        Sync("context callback can replace the configured menu", f =>
        {
            var next = new MenuFlyout(); next.Items.Add(new MenuFlyoutItem());
            f.First.DataContextChanged += (_, _) => { if (ReferenceEquals(f.First.DataContext, f.ContextA)) f.A.DropDownContextMenu = next; };
            f.A.Invoke(); f.Closed(); Check.False(next.IsOpen);
            f.A.Invoke(); Check.True(next.IsOpen); next.Hide();
        });
        Sync("checked callback can request another trigger before native opening", f =>
        {
            f.A.Checked += (_, _) => f.B.Invoke(); f.A.Invoke(); f.Owner(f.B, f.ContextB);
            Check.False(f.A.IsChecked == true);
        });
        Sync("context cleanup can reopen the shared menu for another trigger", f =>
        {
            f.A.Invoke(); var redirected = false;
            f.First.DataContextChanged += (_, _) =>
            { if (!redirected && f.First.DataContext == null) { redirected = true; f.B.Invoke(); } };
            f.A.Invoke(); f.Owner(f.B, f.ContextB); Check.True(redirected);
        });
        Sync("opening callback can detach the owner", f =>
        { f.Menu.Opening += (_, _) => host.Children.Remove(f.A); f.A.Invoke(); f.Closed(); });
        Sync("checked callback can disable the owner before ShowAt", f =>
        {
            var opened = 0; f.Menu.Opened += (_, _) => opened++;
            f.A.Checked += (_, _) => f.A.IsEnabled = false;
            f.A.Invoke(); f.Closed(); Check.Equal(0, opened);
        });
        Sync("opening failure releases state and allows a later retry", f =>
        {
            var fail = true; f.Menu.Opening += (_, _) => { if (fail) throw new InvalidOperationException("opening callback"); };
            Check.Throws<InvalidOperationException>(f.A.Invoke); f.Closed(); fail = false;
            f.A.Invoke(); f.Owner(f.A, f.ContextA);
        });
        Sync("context assignment failure releases the partial scope", f =>
        {
            var fail = true;
            f.Second.DataContextChanged += (_, _) =>
            { if (fail && ReferenceEquals(f.Second.DataContext, f.ContextA)) throw new InvalidOperationException("context callback"); };
            Check.Throws<InvalidOperationException>(f.A.Invoke); f.Closed(); fail = false;
            f.B.Invoke(); f.Owner(f.B, f.ContextB);
        });
        Sync("native dismissal unchecks the owner and clears scoped values", f =>
        { f.A.Invoke(); f.Menu.Hide(); f.Closed(); });
        Sync("cancelled native closing retains owner and context", f =>
        {
            var cancel = true; f.Menu.Closing += (_, e) => e.Cancel = cancel;
            f.A.Invoke(); f.A.Invoke(); f.Owner(f.A, f.ContextA);
            cancel = false; f.A.Invoke(); f.Closed();
        });
        Sync("repeated shared transfers retain the original menu items", f =>
        {
            for (var i = 0; i < 20; i++) { f.A.Invoke(); f.B.Invoke(); }
            f.Owner(f.B, f.ContextB); Check.Same(f.First, f.Menu.Items[0]); Check.Same(f.Second, f.Menu.Items[1]);
        });
        Sync("old Closed invocation cannot clear an application-reopened successor", f =>
        {
            // Register before the session's handler so it receives the old close first.
            var reopen = true;
            f.Menu.Closed += (_, _) => { if (reopen) { reopen = false; f.B.Invoke(); } };
            f.A.Invoke(); f.Menu.Hide(); f.Owner(f.B, f.ContextB);
        });
        Sync("disabled trigger does not open through the protected click hook", f =>
        { f.A.IsEnabled = false; f.A.Invoke(); f.Closed(); });
        Sync("detached trigger fails before assigning menu contexts", f =>
        { host.Children.Remove(f.A); Check.Throws<InvalidOperationException>(f.A.Invoke); f.Closed(); });

        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            foreach (var veto in new[] { "none", "press", "release" })
                Add("native area right-button stages: " + veto, async f =>
                {
                    f.Area.VetoPress = veto == "press"; f.Area.VetoRelease = veto == "release";
                    using var input = new X11TestInput(); input.MoveTo(f.Area, new(35, 35)); await Task.Delay(50);
                    input.Press(3); await Task.Delay(65); input.Release(3); await Task.Delay(100);
                    Check.Equal(1, f.Area.PressCount); Check.Equal(1, f.Area.ReleaseCount);
                    Check.Equal(veto == "none", f.Menu.IsOpen);
                    if (f.Menu.IsOpen) { Check.Same(f.Area, f.Menu.Target); Check.Same(f.ContextB, f.First.DataContext); }
                });
            Add("native Shift F10 opens the focused dropdown area without pointer stages", async f =>
            {
                Check.True(f.Area.Focus(FocusState.Programmatic)); using var input = new X11TestInput();
                input.KeyDown(0xffe1); input.KeyPress(0xffc7); input.KeyUp(0xffe1); await Task.Delay(100);
                Check.True(f.Menu.IsOpen); Check.Same(f.Area, f.Menu.Target); Check.Equal(0, f.Area.PressCount);
            });
            Add("native button click and Escape update the actual checked state", async f =>
            {
                using var input = new X11TestInput(); input.MoveTo(f.A, new(35, 15)); await Task.Delay(50);
                input.Press(); await Task.Delay(50); input.Release(); await Task.Delay(100);
                f.Owner(f.A, f.ContextA); input.Escape(); await Task.Delay(100); f.Closed();
            });
        }
        try { return await tests.Run(output, "dropdown-quality"); }
        finally { window.Close(); }
    }

    private static void Unassigned(MenuFlyoutItemBase item) =>
        Check.Same(DependencyProperty.UnsetValue, item.ReadLocalValue(FrameworkElement.DataContextProperty));

    private sealed class ButtonProbe : DropDownButton { public void Invoke() => OnClick(); }
    private sealed class AreaProbe : DropDownControlArea
    {
        public bool VetoPress, VetoRelease;
        public int PressCount, ReleaseCount;
        protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
        { PressCount++; if (VetoPress) e.Handled = true; base.OnMouseRightButtonDown(e); }
        protected override void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e)
        { ReleaseCount++; if (VetoRelease) e.Handled = true; base.OnPreviewMouseRightButtonUp(e); }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly StackPanel _host;
        internal readonly object ContextA = new(), ContextB = new();
        internal readonly MenuFlyout Menu = new() { AreOpenCloseAnimationsEnabled = false };
        internal readonly MenuFlyoutItem First = new() { Text = "First command" }, Second = new() { Text = "Second command" };
        internal readonly ButtonProbe A = new() { Content = "Editor A", Width = 180, Height = 34 };
        internal readonly ButtonProbe B = new() { Content = "Editor B", Width = 180, Height = 34 };
        internal readonly AreaProbe Area = new() { IsTabStop = true, Height = 100, Content = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray), Child = new TextBlock { Text = "Right-click or Shift+F10", Margin = new Thickness(12) } } };
        internal Fixture(StackPanel host)
        {
            _host = host; Menu.Items.Add(First); Menu.Items.Add(Second);
            A.DataContext = ContextA; B.DataContext = ContextB; Area.DataContext = ContextB;
            A.DropDownContextMenu = B.DropDownContextMenu = Area.DropDownContextMenu = Menu;
            host.Children.Add(A); host.Children.Add(B); host.Children.Add(Area);
        }
        internal void Owner(ButtonProbe button, object context)
        { Check.True(Menu.IsOpen); Check.Same(button, Menu.Target); Check.True(button.IsChecked == true); Check.Same(context, First.DataContext); }
        internal void Closed()
        { Check.False(Menu.IsOpen); Check.False(A.IsChecked == true); Check.False(B.IsChecked == true); Unassigned(First); Unassigned(Second); }
        public void Dispose()
        {
            Menu.Hide(); A.DropDownContextMenu?.Hide(); B.DropDownContextMenu?.Hide();
            _host.Children.Clear();
        }
    }
}
