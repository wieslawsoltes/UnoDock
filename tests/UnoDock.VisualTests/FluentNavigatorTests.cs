using System.Reflection;
using System.Windows.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Testing;

internal static class FluentNavigatorTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var theme in new[]
        {
            ElementTheme.Light,
            ElementTheme.Dark
        }

        )
        {
            tests.Test($"Fluent navigator: {theme} native button composition preserves ListBox contracts", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                Check.True(f.List is NavigatorListBox);
                Check.True(f.Row(1) is ListBoxItem);
                var button = f.Action(1);
                Check.Equal(typeof(Button), button.GetType());
                Check.True(button.Template != null && button.UseSystemFocusVisuals);
                Check.False(button.IsTabStop);
                Check.Equal(f.Documents[1].Title, AutomationProperties.GetName(button));
                var states = button.FindVisualChildren<FrameworkElement>().SelectMany(VisualStateManager.GetVisualStateGroups).SelectMany(group => group.States).Select(state => state.Name).ToHashSet();
                Check.True(new[] { "Normal", "PointerOver", "Pressed", "Disabled" }.All(states.Contains));
                Check.Equal(new CornerRadius(8), f.Navigator.CornerRadius);
                Check.Equal(new Thickness(1), f.Navigator.BorderThickness);
                Check.True(f.Row(1).ActualHeight >= 28);
                Check.Same(f.Items[1], f.Row(1).Content);
            });
            tests.Test($"Fluent navigator: {theme} preview moves indicator without activating editors", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var active = f.Manager.Layout.ActiveContent;
                var row = f.Row(0);
                var button = f.Action(0);
                var template = button.Template;
                f.Navigator.PreviewDocument(f.Items[0]);
                await f.Settle();
                Check.Equal(Visibility.Visible, f.Marker(0).Visibility);
                Check.False(f.Marker(0).IsHitTestVisible);
                Check.Equal(Microsoft.UI.Text.FontWeights.SemiBold, button.FontWeight);
                f.Navigator.PreviewDocument(f.Items[2]);
                await f.Settle();
                Check.Equal(Visibility.Collapsed, f.Marker(0).Visibility);
                Check.Equal(Visibility.Visible, f.Marker(2).Visibility);
                Check.Same(active, f.Manager.Layout.ActiveContent);
                Check.Same(row, f.Row(0));
                Check.Same(button, f.Action(0));
                Check.Same(template, button.Template);
            });
            tests.Test($"Fluent navigator: {theme} native Invoke commits the requested command exactly once", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var executions = 0;
                f.Items[2].ActivateCommand = new Command(() =>
                {
                    executions++;
                    f.Documents[2].IsActive = true;
                });
                var button = f.Action(2);
                Invoke(button);
                await Wait(() => !f.Navigator.IsLoaded);
                Check.Equal(1, executions);
                Check.Same(f.Documents[2], f.Manager.Layout.ActiveContent);
                Invoke(button);
                await Task.Delay(30);
                Check.Equal(1, executions);
            });
            tests.Test($"Fluent navigator: {theme} vetoed native action cannot fall back to model activation", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var active = f.Manager.Layout.ActiveContent;
                var executions = 0;
                f.Items[2].ActivateCommand = new Command(() => executions++, () => false);
                Invoke(f.Action(2));
                await f.Settle();
                Check.Equal(0, executions);
                Check.Same(active, f.Manager.Layout.ActiveContent);
            });
            tests.Test($"Fluent navigator: {theme} consumer selection brushes and active hover refresh retain identity", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var selection = new SolidColorBrush(Microsoft.UI.Colors.DarkOliveGreen);
                var hover = new SolidColorBrush(Microsoft.UI.Colors.DarkOrchid);
                // The scope belongs to the consumer, not to the manager itself.
                f.Scope.Resources["UnoDock.NavigatorSelectionBrush"] = selection;
                f.Scope.Resources["UnoDock.HoverBrush"] = hover;
                f.Navigator.PreviewDocument(f.Items[1]);
                await f.Settle();
                var button = f.Action(1);
                var template = button.Template;
                Check.Same(selection, button.Background);
                Check.True(VisualStateManager.GoToState(button, "PointerOver", false));
                await Wait(() => HasBackground(button, hover));
                var replacement = new SolidColorBrush(Microsoft.UI.Colors.CadetBlue);
                f.Scope.Resources["UnoDock.HoverBrush"] = replacement;
                await f.Settle();
                await Wait(() => HasBackground(button, replacement));
                Check.Same(button, f.Action(1));
                Check.Same(template, button.Template);
            });
            tests.Test($"Fluent navigator: {theme} theme and density changes retain rows, sources and editors", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var row = f.Row(2);
                var button = f.Action(2);
                var template = button.Template;
                var source = f.List.ItemsSource;
                var editor = f.Documents[2].Content;
                f.Navigator.PreviewDocument(f.Items[2]);
                f.Manager.Theme = new FluentTheme(theme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark);
                f.Manager.ChromeDensity = DockChromeDensity.Spacious;
                await f.Settle();
                Check.Same(source, f.List.ItemsSource);
                Check.Same(row, f.Row(2));
                Check.Same(button, f.Action(2));
                Check.Same(template, button.Template);
                Check.Same(editor, f.Documents[2].Content);
                Check.True(row.ActualHeight >= 36);
                Check.Same(f.Items[2], f.Navigator.SelectedDocument);
            });
            tests.Test($"Fluent navigator: {theme} Generic round trip retains containers and legacy geometry", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var row = f.Row(1);
                var oldButton = f.Action(1);
                var source = f.List.ItemsSource;
                f.Manager.Theme = new GenericTheme();
                f.Manager.ChromeDensity = DockChromeDensity.Default;
                await f.Settle();
                Check.Same(row, f.Row(1));
                Check.Same(source, f.List.ItemsSource);
                Check.Equal(new CornerRadius(0), f.Navigator.CornerRadius);
                Check.Equal(new Thickness(3), f.Navigator.BorderThickness);
                Check.Equal(24d, row.Height);
                Check.False(row.FindVisualChildren<Button>().Any(b => b.Name == "PART_NavigatorAction"));
                var active = f.Manager.Layout.ActiveContent;
                Invoke(oldButton);
                Check.Same(active, f.Manager.Layout.ActiveContent);
                f.Manager.Theme = new FluentTheme(theme);
                await f.Settle();
                Check.Same(row, f.Row(1));
                Check.True(f.Action(1).Template != null);
                Check.Equal(new CornerRadius(8), f.Navigator.CornerRadius);
            });
            foreach (var allowed in new[]
            {
                false,
                true
            }

            )
            {
                tests.Test($"Fluent navigator: {theme} tool action honors eligibility={allowed}", async () =>
                {
                    using var f = new Fixture(theme);
                    await f.Show();
                    var tool = f.Navigator.Anchorables.Single();
                    var tools = (ListBox)typeof(NavigatorWindow).GetField("_anchorablesList", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Navigator)!;
                    var row = (ListBoxItem)tools.ContainerFromItem(tool);
                    var button = row.FindVisualChildren<Button>().Single(b => b.Name == "PART_NavigatorAction");
                    var executions = 0;
                    var active = f.Manager.Layout.ActiveContent;
                    tool.ActivateCommand = new Command(() =>
                    {
                        executions++;
                        tool.LayoutElement.IsActive = true;
                    }, () => allowed);
                    Invoke(button);
                    await f.Settle();
                    Check.Equal(allowed ? 1 : 0, executions);
                    Check.Same(allowed ? tool.LayoutElement : active, f.Manager.Layout.ActiveContent);
                    Check.Same(tool, row.Content);
                });
            }

            tests.Test($"Fluent navigator: {theme} pending native action cannot revive a closed session", async () =>
            {
                using var f = new Fixture(theme);
                await f.Show();
                var button = f.Action(2);
                var active = f.Manager.Layout.ActiveContent;
                f.Close();
                await f.Settle();
                Invoke(button);
                await Task.Delay(20);
                Check.Same(active, f.Manager.Layout.ActiveContent);
                Check.False(f.Navigator.IsLoaded);
            });
        }

        tests.Test("Fluent navigator: far-row reveal follows arranged native row sizes", async () =>
        {
            using var f = new Fixture(ElementTheme.Dark, 60);
            await f.Show();
            f.Manager.ChromeDensity = DockChromeDensity.Spacious;
            await f.Settle();
            var item = f.Navigator.Documents.Last();
            f.Navigator.PreviewDocument(item);
            await Wait(() => FullyVisible(f.List, item));
            Check.True(f.List.FindVisualChildren<ScrollViewer>().First().VerticalOffset > 0);
            f.Navigator.PreviewDocument(f.Navigator.Documents[0]);
            await Wait(() => FullyVisible(f.List, f.Navigator.Documents[0]));
        });
        tests.Test("Fluent navigator: reentrant command selection cannot activate an obsolete native row", async () =>
        {
            using var f = new Fixture(ElementTheme.Light);
            await f.Show();
            var executions = 0;
            var active = f.Manager.Layout.ActiveContent;
            f.Items[2].ActivateCommand = new Command(() => executions++, () =>
            {
                f.Navigator.PreviewDocument(f.Items[0]);
                return true;
            });
            Invoke(f.Action(2));
            await f.Settle();
            Check.Equal(0, executions);
            Check.Same(active, f.Manager.Layout.ActiveContent);
        });
        tests.Test("Fluent navigator: corner overrides are scoped, finite and bounded", async () =>
        {
            using var f = new Fixture(ElementTheme.Light);
            await f.Show();
            foreach (var (value, expected) in new[]
            {
                (0d, 0d),
                (12d, 12d),
                (double.NaN, 8d),
                (-20d, 0d),
                (200d, 24d)
            }

            )
            {
                f.Scope.Resources["UnoDock.NavigatorCornerRadius"] = value;
                await f.Settle();
                Check.Equal(new CornerRadius(expected), f.Navigator.CornerRadius);
            }
        });
        foreach (var scene in new[]
        {
            "light",
            "dark",
            "rtl",
            "large-text",
            "narrow"
        }

        )
        {
            tests.Test("Fluent navigator: rendered native selection and frame: " + scene, async () =>
            {
                using var f = new Fixture(scene == "light" ? ElementTheme.Light : ElementTheme.Dark);
                await f.Show();
                if (scene == "rtl")
                    f.Manager.FlowDirection = FlowDirection.RightToLeft;
                if (scene == "large-text")
                    f.Manager.Resources["UnoDock.FontSize"] = 22d;
                if (scene == "narrow")
                    f.Scope.Width = 600;
                f.Navigator.PreviewDocument(f.Items[2]);
                await f.Settle();
                var button = f.Action(2);
                var marker = f.Marker(2);
                Check.True(button.ActualWidth > 0 && button.ActualHeight >= button.FontSize);
                Check.Equal(Visibility.Visible, marker.Visibility);
                Check.False(marker.IsHitTestVisible);
                if (scene == "rtl")
                {
                    // Compare in the unmirrored owner scope. Row-local coordinates
                    // are logical and would hide that row's RTL transform.
                    var rowBounds = f.Row(2).TransformToVisual(f.Scope).TransformBounds(new(0, 0, f.Row(2).ActualWidth, f.Row(2).ActualHeight));
                    var markerBounds = marker.TransformToVisual(f.Scope).TransformBounds(new(0, 0, marker.ActualWidth, marker.ActualHeight));
                    Check.True(markerBounds.Left > rowBounds.X + rowBounds.Width / 2, "The selection indicator is not on the physical RTL leading edge.");
                }

                var path = Path.Combine(output, "visuals", "fluent-navigator-" + scene + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Preserve native Fluent brush transitions; capture their settled
                // presentation rather than the previous selection fading out.
                await Task.Delay(250);
                await VisualCapture.Save(f.Scope, path);
            });
        }

        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            tests.Test("XTEST Fluent navigator: held pointer previews native pressed state before one release commit", async () =>
            {
                using var f = new Fixture(ElementTheme.Light);
                await f.Show();
                var count = 0;
                var button = f.Action(2);
                f.Items[2].ActivateCommand = new Command(() =>
                {
                    count++;
                    f.Documents[2].IsActive = true;
                });
                using var input = new X11TestInput();
                input.MoveTo(button, new(button.ActualWidth / 2, button.ActualHeight / 2));
                await Task.Delay(80);
                // The navigator owns keyboard focus; nested actions deliberately
                // add no Tab stops. Test their native capture/pressed lifecycle
                // with physical pointer input, not a forced keyboard focus.
                input.Press();
                await Wait(() => button.IsPressed);
                Check.Equal(0, count);
                input.Release();
                await Wait(() => count == 1 && !f.Navigator.IsLoaded);
                Check.Same(f.Documents[2], f.Manager.Layout.ActiveContent);
            });
            tests.Test("XTEST Fluent navigator: a real row click commits only its target", async () =>
            {
                using var f = new Fixture(ElementTheme.Dark);
                await f.Show();
                var count = 0;
                f.Items[2].ActivateCommand = new Command(() =>
                {
                    count++;
                    f.Documents[2].IsActive = true;
                });
                using var input = new X11TestInput();
                await input.Click(f.Action(2));
                await Wait(() => count == 1 && !f.Navigator.IsLoaded);
                Check.Same(f.Documents[2], f.Manager.Layout.ActiveContent);
            });
        }

        return await tests.Run(output, "fluent-navigator");
    }

    private static void Invoke(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
        Check.True(provider != null);
        provider!.Invoke();
    }

    private static bool HasBackground(Control control, Brush brush) => control.FindVisualChildren<FrameworkElement>().Any(element => ReferenceEquals(element switch
    {
        Border b => b.Background,
        Panel p => p.Background,
        ContentPresenter c => c.Background,
        Control c => c.Background,
        _ => null
    }, brush));
    private static bool FullyVisible(ListBox list, LayoutItem item)
    {
        if (list.ContainerFromItem(item) is not FrameworkElement row || row.ActualHeight <= 0)
            return false;
        var scroll = list.FindVisualChildren<ScrollViewer>().First();
        var bounds = row.TransformToVisual(scroll).TransformBounds(new(0, 0, row.ActualWidth, row.ActualHeight));
        return bounds.Top >= -1 && bounds.Bottom <= scroll.ViewportHeight + 2;
    }

    private static object? Call(object instance, string method, params object[] args)
    {
        try
        {
            return instance.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 160 && !predicate(); i++)
            await Task.Delay(20);
        Check.True(predicate(), "Native navigator state did not converge.");
    }

    private sealed class Command(Action action, Func<bool>? eligible = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public bool CanExecute(object? parameter) => eligible?.Invoke() ?? true;
        public void Execute(object? parameter) => action();
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Grid Scope = new();
        internal readonly DockingManager Manager = new()
        {
            FloatingWindowMode = FloatingWindowMode.InSurface,
            ChromeDensity = DockChromeDensity.Comfortable
        };
        internal readonly LayoutDocument[] Documents;
        internal NavigatorWindow Navigator = null!;
        internal LayoutDocumentItem[] Items = [];
        internal ListBox List => (ListBox)typeof(NavigatorWindow).GetField("_documentsList", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Navigator)!;

        private readonly Window _window;
        private readonly IDisposable _registration;
        private object Surface => typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager)!;

        internal Fixture(ElementTheme theme, int count = 4)
        {
            Scope.Resources.MergedDictionaries.Add(new WorkbenchResources());
            Scope.RequestedTheme = theme;
            Manager.Theme = new FluentTheme(theme);
            Documents = Enumerable.Range(0, count).Select(i => new LayoutDocument { Title = $"Document {i + 1:00}.cs", ContentId = "native-nav-" + i, Description = "Workspace / Source / Editing", Content = new TextBox { Text = "Retained editor " + i } }).ToArray();
            var documents = new LayoutDocumentPane();
            foreach (var item in Documents)
                documents.Children.Add(item);
            var tools = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Solution Explorer", ContentId = "native-nav-explorer", Content = new TextBlock { Text = "Tools" } });
            var panel = new LayoutPanel(tools);
            panel.Children.Add(documents);
            Manager.Layout = new()
            {
                RootPanel = panel
            };
            Documents[0].IsActive = true;
            Scope.Children.Add(Manager);
            _window = new Window
            {
                Content = Scope,
                Title = "UnoDock Fluent navigator acceptance"
            };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Resize(new()
            {
                Width = 1000,
                Height = 720
            });
            _window.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualWidth > 0);
            Manager.Refresh();
            Navigator = new NavigatorWindow(Manager);
            Call(Surface, "ShowNavigator", Navigator);
            await Wait(() => Navigator.IsLoaded && Navigator.ActualWidth > 0 && Navigator.Documents.Length == Documents.Length);
            Items = Documents.Select(doc => Navigator.Documents.Single(item => ReferenceEquals(item.LayoutElement, doc))).ToArray();
            await Wait(() => List.ContainerFromItem(Items[0]) is FrameworkElement row && row.FindVisualChildren<Button>().Any(button => button.Name == "PART_NavigatorAction" && button.ActualWidth > 0));
            await Settle();
        }

        internal ListBoxItem Row(int index) => (ListBoxItem)List.ContainerFromItem(Items[index]);
        internal Button Action(int index) => Row(index).FindVisualChildren<Button>().Single(button => button.Name == "PART_NavigatorAction");
        internal Border Marker(int index) => Row(index).FindVisualChildren<Border>().Single(border => border.Name == "PART_NavigatorSelection");
        internal async Task Settle()
        {
            Manager.Refresh();
            Scope.UpdateLayout();
            await Task.Delay(50);
        }

        internal void Close() => Call(Surface, "CloseNavigator", false);
        public void Dispose()
        {
            try
            {
                if (Navigator != null)
                    Close();
                Manager.Dispose();
            }
            finally
            {
                _window.Content = null;
                _window.Close();
                _registration.Dispose();
            }
        }
    }
}
