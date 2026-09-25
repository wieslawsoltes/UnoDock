using System.Reflection;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Testing;

internal static class UnoThemeTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var explicitTheme in new[]
        {
            false,
            true
        }

        )
            foreach (var mode in new[]
            {
                ElementTheme.Light,
                ElementTheme.Dark
            }

            )
                tests.Test($"Uno resources: explicit={explicitTheme}, mode={mode}", async () =>
                {
                    using var f = new Fixture();
                    await f.Show();
                    f.Manager.RequestedTheme = explicitTheme ? Opposite(mode) : mode;
                    var theme = explicitTheme ? new FluentTheme(mode) : new FluentTheme();
                    f.Manager.Theme = theme;
                    f.Manager.Refresh();
                    await Task.Delay(40);
                    var palette = Palette(f.Manager);
                    Check.Same(mode == ElementTheme.Light ? f.LightSurface : f.DarkSurface, Property<Brush>(palette, "Surface"));
                    Check.Same(mode == ElementTheme.Light ? f.LightText : f.DarkText, Property<Brush>(palette, "Foreground"));
                    if (explicitTheme)
                    {
                        Check.Same(theme.ThemeResourceDictionary["UnoDock.PaneBrush"], Property<Brush>(palette, "Surface"));
                        Check.Same(theme.ThemeResourceDictionary["UnoDock.ForegroundBrush"], Property<Brush>(palette, "Foreground"));
                    }
                });
        tests.Test("Uno resources: local docking override wins without recoloring the application brush", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new FluentTheme(ElementTheme.Dark);
            var custom = new SolidColorBrush(Microsoft.UI.Colors.Crimson);
            f.Manager.Resources["UnoDock.PaneBrush"] = custom;
            f.Manager.Refresh();
            Check.Same(custom, Property<Brush>(Palette(f.Manager), "Surface"));
            Check.Equal(Microsoft.UI.Colors.Crimson, custom.Color);
            f.Manager.Resources.Remove("UnoDock.PaneBrush");
            f.Manager.Refresh();
            Check.Same(f.DarkSurface, Property<Brush>(Palette(f.Manager), "Surface"));
        });
        tests.Test("Uno resources: explicit dictionary customizations are never overwritten", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var theme = new FluentTheme(ElementTheme.Dark);
            var custom = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod);
            theme.ThemeResourceDictionary["UnoDock.ForegroundBrush"] = custom;
            f.Manager.Theme = theme;
            f.Manager.Refresh();
            Check.Same(custom, Property<Brush>(Palette(f.Manager), "Foreground"));
            Check.Same(custom, theme.ThemeResourceDictionary["UnoDock.ForegroundBrush"]);
        });
        tests.Test("Uno resources: default theme follows host changes without replacing editors or views", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new FluentTheme();
            var editor = f.Document.Content;
            var pane = f.Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single();
            foreach (var mode in new[]
            {
                ElementTheme.Dark,
                ElementTheme.Light,
                ElementTheme.Dark
            }

            )
            {
                f.Scope.RequestedTheme = mode;
                await Wait(() => f.Manager.ActualTheme == mode);
                f.Manager.Refresh();
                Check.Same(mode == ElementTheme.Light ? f.LightSurface : f.DarkSurface, Property<Brush>(Palette(f.Manager), "Surface"));
                Check.Same(editor, f.Document.Content);
                Check.Same(pane, f.Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single());
            }
        });
        tests.Test("Uno resources: brush replacement at equal dictionary size is observed on refresh", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new FluentTheme(ElementTheme.Light);
            Check.Same(f.LightSurface, Property<Brush>(Palette(f.Manager), "Surface"));
            var replacement = new SolidColorBrush(Microsoft.UI.Colors.AliceBlue);
            ((ResourceDictionary)f.Scope.Resources.ThemeDictionaries["Light"])["LayerFillColorDefaultBrush"] = replacement;
            f.Manager.Refresh();
            Check.Same(replacement, Property<Brush>(Palette(f.Manager), "Surface"));
        });
        tests.Test("Uno resources: Generic remains an explicit legacy palette", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new GenericTheme();
            f.Manager.Refresh();
            Check.False(ReferenceEquals(f.LightSurface, Property<Brush>(Palette(f.Manager), "Surface")));
            Check.False(ReferenceEquals(f.DarkSurface, Property<Brush>(Palette(f.Manager), "Surface")));
        });
        tests.Test("Uno resources: Fluent preserves compact docking metrics", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.Theme = new GenericTheme();
            var legacy = Palette(f.Manager);
            f.Manager.Theme = new FluentTheme();
            var fluent = Palette(f.Manager);
            foreach (var name in new[]
            {
                "FontSize",
                "TitleHeight",
                "TabHeight",
                "ToolTabHeight",
                "RailThickness"
            }

            )
                Check.Equal(Property<double>(legacy, name), Property<double>(fluent, name));
        });
        return await tests.Run(output, "uno-theme");
    }

    private static ElementTheme Opposite(ElementTheme mode) => mode == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
    private static object Palette(DockingManager manager) => typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
    private static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            Width = 800,
            Height = 480,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        internal readonly Grid Scope = new();
        internal readonly LayoutDocument Document = new()
        {
            Title = "Retained editor",
            ContentId = "theme-editor",
            Content = new TextBox
            {
                Text = "Unsaved text"
            }
        };
        internal readonly SolidColorBrush LightSurface = new(Microsoft.UI.Colors.Ivory), DarkSurface = new(Microsoft.UI.Colors.MidnightBlue);
        internal readonly SolidColorBrush LightText = new(Microsoft.UI.Colors.DarkSlateGray), DarkText = new(Microsoft.UI.Colors.WhiteSmoke);
        private readonly Window _window;
        internal Fixture()
        {
            Scope.Resources.ThemeDictionaries["Light"] = new ResourceDictionary
            {
                ["LayerFillColorDefaultBrush"] = LightSurface,
                ["TextFillColorPrimaryBrush"] = LightText
            };
            Scope.Resources.ThemeDictionaries["Dark"] = new ResourceDictionary
            {
                ["LayerFillColorDefaultBrush"] = DarkSurface,
                ["TextFillColorPrimaryBrush"] = DarkText
            };
            Manager.Layout = new()
            {
                RootPanel = new(new LayoutDocumentPane(Document))
            };
            Document.IsActive = true;
            Scope.Children.Add(Manager);
            _window = new()
            {
                Content = Scope,
                Title = "UnoDock theme acceptance"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 920,
                Height = 650
            });
            _window.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualWidth > 0);
            Manager.Refresh();
            await Task.Delay(30);
        }

        public void Dispose()
        {
            Manager.Dispose();
            _window.Content = null;
            _window.Close();
        }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
            await Task.Delay(20);
        Check.True(predicate(), "Theme host did not settle.");
    }
}
