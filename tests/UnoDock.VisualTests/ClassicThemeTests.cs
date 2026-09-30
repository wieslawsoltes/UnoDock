using System.Reflection;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.UI;
using PathShape = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Testing;
/// <summary>Acceptance for the classic Aero, Metro and VS2010 themes: fixed
/// light chrome, state-specific tab/title/frame brushes and theme switching
/// that retains application content.</summary>
internal static class ClassicThemeTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var (name, create, pane) in new (string, Func<Theme>, uint)[]
        {
            ("Aero", () => new AeroTheme(), 0xFFFFFF),
            ("Metro", () => new MetroTheme(), 0xFFFFFF),
            ("VS2010", () => new VS2010Theme(), 0xFFFFFF)
        }

        )
            tests.Test($"{name}: fixed light chrome resolves the theme dictionary under a dark host", async () =>
            {
                using var f = new Fixture();
                await f.Show();
                f.Manager.RequestedTheme = ElementTheme.Dark;
                f.Manager.Theme = create();
                await f.Settle();
                Check.Equal(ElementTheme.Light, EffectiveTheme(f.Manager));
                Check.Equal(pane, Rgb(Property<Brush>(Palette(f.Manager), "Surface")));
                await VisualCapture.Save(f.Manager, Path.Combine(output, "classic-themes", name + ".png"));
            });
        tests.Test("VS2010: active document owns the gold tab and content band; tool activation releases both", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new VS2010Theme();
            f.Document.IsActive = true;
            await f.Settle();
            var tab = f.Tab(f.Document);
            Check.True(Stops(Chrome(tab).Background).Contains(0xFFE8A6u), "The active document tab must use the active gradient.");
            Check.Equal(3.0, Chrome(tab).CornerRadius.TopLeft);
            // The band spans the content below the tabs only: top and bottom, no sides.
            Check.Equal(new Thickness(0), f.DocumentFrame().BorderThickness);
            var band = f.ContentFrame();
            Check.Equal(0xFFE8A6u, Rgb(band.BorderBrush));
            Check.Equal(new Thickness(0, 3, 0, 4), band.BorderThickness);
            Check.Equal(3.0, band.CornerRadius.TopLeft);
            f.Tool.IsActive = true;
            await f.Settle();
            Check.True(!Stops(Chrome(f.Tab(f.Document)).Background).Contains(0xFFE8A6u), "An inactive selected document must not keep the active gradient.");
            Check.Equal(0xCED4DFu, Rgb(f.ContentFrame().BorderBrush));
        });
        tests.Test("VS2010: caption buttons use the caption hover states and the disabled brush", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new VS2010Theme();
            f.Document.IsActive = true;
            await f.Settle();
            var pane = f.Manager.FindVisualChildren<LayoutAnchorablePaneControl>().Single();
            var close = pane.FindVisualChildren<Button>().First(b => ToolTipService.GetToolTip(b) as string == UnoDock.Properties.Resources.Anchorable_BtnClose_Hint);
            Check.Equal(0xFFFFFFu, Rgb(close.Foreground));
            Check.Equal(0xFFFFFFu, Rgb(Hover(close, "_hoverOverride")));
            Check.Equal(0x000000u, Rgb(Hover(close, "_hoverForegroundOverride")));
            f.Tool.IsEnabled = false;
            await f.Settle();
            Check.Equal(0x8C95A6u, Rgb(close.Foreground));
            Check.Equal(1d, close.Opacity);
        });
        tests.Test("VS2010: the auto-hide flyout caption uses the tool title states", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new VS2010Theme();
            f.Tool.ToggleAutoHide();
            f.Document.IsActive = true;
            await f.Settle();
            var open = typeof(DockingManager).GetMethod("OpenAutoHide", BindingFlags.Instance | BindingFlags.NonPublic)!;
            open.Invoke(f.Manager, [f.Tool, false]);
            await f.Settle();
            var flyout = f.Manager.AutoHideWindow!;
            TextBlock Title() => flyout.FindVisualChildren<TextBlock>().First(t => t.Text == f.Tool.Title);
            Check.False(f.Tool.IsActive);
            Check.Equal(0xFFFFFFu, Rgb(Title().Foreground));
            f.Tool.IsActive = true;
            await f.Settle();
            Check.Equal(0x000000u, Rgb(Title().Foreground));
            typeof(DockingManager).GetMethod("CloseAutoHide", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f.Manager, []);
        });
        tests.Test("Metro: selected tabs carry a top indicator that takes the accent only while active", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new MetroTheme();
            f.Document.IsActive = true;
            await f.Settle();
            var indicator = Named<Border>(f.Tab(f.Document), "PART_SelectedTabIndicator");
            Check.Equal(Visibility.Visible, indicator.Visibility);
            Check.Equal(VerticalAlignment.Top, indicator.VerticalAlignment);
            Check.Equal(3.0, indicator.Height);
            Check.Equal(0x41B1E1u, Rgb(indicator.Background));
            f.Tool.IsActive = true;
            await f.Settle();
            Check.Equal(0x333333u, Rgb(Named<Border>(f.Tab(f.Document), "PART_SelectedTabIndicator").Background));
            Check.Equal(Visibility.Collapsed, Named<Border>(f.Tab(f.Second), "PART_SelectedTabIndicator").Visibility);
        });
        tests.Test("Aero: document tabs use a slanted outline and a bold selected title", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new AeroTheme();
            f.Document.IsActive = true;
            await f.Settle();
            var tab = f.Tab(f.Document);
            var shape = Named<PathShape>(tab, "PART_TabShape");
            Check.Equal(Visibility.Visible, shape.Visibility);
            Check.True(shape.Data is PathGeometry { Figures.Count: 1 }, "The outline must be a single closed figure.");
            Check.True(shape.ActualWidth <= tab.ActualWidth + 1, "The outline must follow the arranged tab, not widen it.");
            Check.Equal(Microsoft.UI.Text.FontWeights.Bold.Weight, tab.FindVisualChildren<Button>().First().FontWeight.Weight);
            Check.Equal(Microsoft.UI.Text.FontWeights.Normal.Weight, f.Tab(f.Second).FindVisualChildren<Button>().First().FontWeight.Weight);
        });
        tests.Test("Theme switching restores Generic defaults and retains application content", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var editor = f.Document.Content;
            foreach (var theme in new Theme[]
            {
                new VS2010Theme(),
                new AeroTheme(),
                new MetroTheme(),
                new GenericTheme()
            }

            )
            {
                f.Manager.Theme = theme;
                await f.Settle();
                Check.Same(editor, f.Document.Content);
            }

            var tab = f.Tab(f.Document);
            Check.Equal(0xFFFFFFu, Rgb(Chrome(tab).Background));
            Check.True(tab.FindVisualChildren<PathShape>().All(p => p.Name != "PART_TabShape" || p.Visibility == Visibility.Collapsed || VisualTreeHelper.GetParent(p) is UIElement { Visibility: Visibility.Collapsed }), "Generic tabs must not keep a theme outline.");
            Check.Equal(ElementTheme.Light, EffectiveTheme(f.Manager));
        });
        tests.Test("Application overrides win over classic theme state brushes", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new VS2010Theme();
            var custom = new SolidColorBrush(Microsoft.UI.Colors.Crimson);
            f.Manager.Resources["UnoDock.WorkspaceBrush"] = custom;
            f.Manager.Refresh();
            await f.Settle();
            Check.Same(custom, States(f.Manager, "Workspace"));
            f.Manager.Resources.Remove("UnoDock.WorkspaceBrush");
            f.Manager.Refresh();
            await f.Settle();
            Check.Equal(0x2C3D5Au, Rgb((Brush)States(f.Manager, "Workspace")));
        });
        return await tests.Run(output, "classic-themes");
    }

    private static object Palette(DockingManager manager) => typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
    private static object States(DockingManager manager, string name)
    {
        var palette = Palette(manager);
        var states = palette.GetType().GetProperty("States", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(palette)!;
        return states.GetType().GetProperty(name)!.GetValue(states)!;
    }

    private static ElementTheme EffectiveTheme(DockingManager manager) => (ElementTheme)typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockThemeResources", true)!.GetMethod("EffectiveTheme", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
    private static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
    private static Grid Chrome(LayoutTabItemBase tab) => (Grid)tab.Content;
    private static Brush? Hover(Button button, string field) => (Brush?)button.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button);
    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement => root.FindVisualChildren<T>().First(e => e.Name == name);
    private static uint Rgb(Brush? brush) => brush is SolidColorBrush { Color: var c } ? Pack(c) : throw new InvalidOperationException("Expected a solid brush, found " + (brush?.GetType().Name ?? "null") + ".");
    private static uint Pack(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);
    private static uint[] Stops(Brush? brush) => brush switch
    {
        GradientBrush gradient => gradient.GradientStops.Select(s => Pack(s.Color)).ToArray(),
        SolidColorBrush solid => [Pack(solid.Color)],
        _ => []
    };
    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            Width = 900,
            Height = 520,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        internal readonly LayoutDocument Document = new()
        {
            Title = "Program.cs",
            ContentId = "classic-document",
            Content = new TextBox
            {
                Text = "Retained editor"
            }
        };
        internal readonly LayoutDocument Second = new()
        {
            Title = "App.xaml",
            ContentId = "classic-second",
            Content = new TextBlock
            {
                Text = "Second"
            }
        };
        internal readonly LayoutAnchorable Tool = new()
        {
            Title = "Solution Explorer",
            ContentId = "classic-tool",
            Content = new TextBlock
            {
                Text = "Tool"
            }
        };
        private readonly Window _window;
        internal Fixture()
        {
            var tools = new LayoutAnchorablePane(Tool)
            {
                DockWidth = new(220)
            };
            Manager.Layout = new()
            {
                RootPanel = new(tools)
            };
            Manager.Layout.RootPanel.Children.Add(new LayoutDocumentPane(Document));
            ((LayoutDocumentPane)Manager.Layout.RootPanel.Children[1]).Children.Add(Second);
            _window = new()
            {
                Content = Manager,
                Title = "UnoDock classic theme acceptance"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 960,
                Height = 600
            });
        }

        internal async Task Show()
        {
            _window.Activate();
            await Settle();
        }

        internal async Task Settle()
        {
            for (var i = 0; i < 4; i++)
            {
                await Task.Delay(40);
                Manager.UpdateLayout();
            }
        }

        internal LayoutTabItemBase Tab(LayoutContent content) => Manager.FindVisualChildren<LayoutTabItemBase>().First(t => ReferenceEquals(t.Model, content));
        internal Grid DocumentFrame() => (Grid)Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single().Content;
        internal Border ContentFrame() => Named<Border>(Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single(), "PART_ContentFrame");
        public void Dispose() => _window.Close();
    }
}
