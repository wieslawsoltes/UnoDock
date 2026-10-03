using System.Reflection;
using System.Runtime.ExceptionServices;
using UnoDock.Controls;
using UnoDock.Themes;

namespace UnoDock.Testing;
/// <summary>Exercise consumer resource ownership on actual platform templates,
/// including styles created after a consumer has configured a retained control.</summary>
internal static class FluentStateResourceTests
{
    internal static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var mode in new[]
        {
            ElementTheme.Light,
            ElementTheme.Dark
        }

        )
        {
            tests.Test($"Fluent resources: button/{mode} vector glyph follows native foreground states", async () =>
            {
                using var f = new Fixture(false, mode);
                var glyph = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Data = new RectangleGeometry
                    {
                        Rect = new(1, 1, 10, 10)
                    },
                    Width = 12,
                    Height = 12,
                    StrokeThickness = 1
                };
                ((Button)f.Control).Content = glyph;
                var over = new SolidColorBrush(Microsoft.UI.Colors.DarkOrchid);
                var pressed = new SolidColorBrush(Microsoft.UI.Colors.Teal);
                var disabled = new SolidColorBrush(Microsoft.UI.Colors.Salmon);
                var states = new ResourceDictionary
                {
                    ["ButtonForegroundPointerOver"] = over,
                    ["ButtonForegroundPressed"] = pressed,
                    ["ButtonForegroundDisabled"] = disabled
                };
                f.Control.Resources.ThemeDictionaries[mode.ToString()] = states;
                await f.Show();
                var template = f.Control.Template;
                await f.State("PointerOver", null);
                await Wait(() => ReferenceEquals(glyph.Stroke, over) && ReferenceEquals(glyph.Fill, over));
                await f.State("Pressed", null);
                await Wait(() => ReferenceEquals(glyph.Stroke, pressed));
                f.Control.IsEnabled = false;
                await Wait(() => ReferenceEquals(glyph.Stroke, disabled) && ReferenceEquals(glyph.Fill, disabled));
                Check.Equal(1d, f.Control.Opacity);
                Check.Same(template, f.Control.Template);
                Check.Same(glyph, ((Button)f.Control).Content);
            });
        }

        foreach (var menu in new[]
        {
            false,
            true
        }

        )
        {
            var kind = menu ? "menu" : "button";
            foreach (var mode in new[]
            {
                ElementTheme.Light,
                ElementTheme.Dark
            }

            )
            {
                foreach (var placement in new[]
                {
                    "direct",
                    "theme",
                    "merged-theme",
#if HAS_UNO
                    // Native WinUI gives a ResourceDictionary one parent, so one instance
                    // cannot serve two theme keys.
                    "shared-theme",
#endif
                    "default"
                }

                )
                {
                    tests.Test($"Fluent resources: {kind}/{mode}/{placement} consumer states remain authoritative", async () =>
                    {
                        using var f = new Fixture(menu, mode);
                        var root = f.Control.Resources;
                        var original = new ResourceDictionary();
                        var hover = new SolidColorBrush(Microsoft.UI.Colors.DarkOrchid);
                        var pressed = new SolidColorBrush(Microsoft.UI.Colors.Teal);
                        original[f.HoverKey] = hover;
                        original[f.PressedKey] = pressed;
                        original["ApplicationSentinel"] = new object();
                        switch (placement)
                        {
                            case "direct":
                                f.Control.Resources = original;
                                break;
                            case "theme":
                                root.ThemeDictionaries[mode.ToString()] = original;
                                break;
                            case "merged-theme":
                                var merged = new ResourceDictionary();
                                merged.ThemeDictionaries[mode.ToString()] = original;
                                root.MergedDictionaries.Add(merged);
                                break;
                            case "shared-theme":
                                root.ThemeDictionaries["Light"] = original;
                                root.ThemeDictionaries["Dark"] = original;
                                break;
                            default:
                                root.ThemeDictionaries["Default"] = original;
                                break;
                        }

                        await f.Show();
                        Check.Same(hover, original[f.HoverKey]);
                        Check.Same(pressed, original[f.PressedKey]);
                        Check.Equal(3, original.Count);
                        if (placement == "theme")
                            Check.Same(original, root.ThemeDictionaries[mode.ToString()]);
                        if (placement == "shared-theme")
                        {
                            Check.Same(original, root.ThemeDictionaries["Light"]);
                            Check.Same(original, root.ThemeDictionaries["Dark"]);
                        }

                        await f.State("PointerOver", hover);
                        await f.State("Pressed", pressed);
                        var template = f.Control.Template;
                        f.Configure(true);
                        await f.State("PointerOver", hover);
                        Check.Same(template, f.Control.Template);
                    });
                }
            }

            tests.Test($"Fluent resources: {kind} contrast dictionary and unrelated entries are not replaced", async () =>
            {
                using var f = new Fixture(menu, ElementTheme.Light);
                var light = new ResourceDictionary
                {
                    ["ApplicationSentinel"] = new object()
                };
                var contrast = new ResourceDictionary
                {
                    [f.HoverKey] = new SolidColorBrush(Microsoft.UI.Colors.Yellow)
                };
                f.Control.Resources.ThemeDictionaries["Light"] = light;
                f.Control.Resources.ThemeDictionaries["HighContrast"] = contrast;
                await f.Show();
                Check.Same(light, f.Control.Resources.ThemeDictionaries["Light"]);
                Check.Same(contrast, f.Control.Resources.ThemeDictionaries["HighContrast"]);
                Check.Equal(1, light.Count);
                Check.Equal(1, contrast.Count);
                await f.State("PointerOver", f.Hover);
                // Do not simulate an OS contrast transition by changing application
                // colors: this assertion is about resource ownership, not hardware.
            });
            tests.Test($"Fluent resources: {kind} changing the resource owner retires only the old forwarding dictionary", async () =>
            {
                using var f = new Fixture(menu, ElementTheme.Light);
                await f.Show();
                var old = f.Control.Resources;
                var consumer = new ResourceDictionary
                {
                    ["ApplicationSentinel"] = new object()
                };
                old.MergedDictionaries.Add(consumer);
                var replacement = new ResourceDictionary();
                f.Control.Resources = replacement;
                f.Configure(true);
                Check.Equal(1, old.MergedDictionaries.Count);
                Check.Same(consumer, old.MergedDictionaries[0]);
                Check.Equal(1, replacement.MergedDictionaries.Count);
                for (var i = 0; i < 10; i++)
                    f.Configure(true);
                Check.Equal(1, replacement.MergedDictionaries.Count);
                await f.State("PointerOver", f.Hover);
            });
            tests.Test($"Fluent resources: {kind} Generic round trips remove forwarding but retain consumer dictionaries", async () =>
            {
                using var f = new Fixture(menu, ElementTheme.Light);
                var consumer = new ResourceDictionary
                {
                    ["ApplicationSentinel"] = new object()
                };
                var light = new ResourceDictionary
                {
                    ["OtherConsumerValue"] = new object()
                };
                f.Control.Resources.MergedDictionaries.Add(consumer);
                f.Control.Resources.ThemeDictionaries["Light"] = light;
                await f.Show();
                for (var i = 0; i < 3; i++)
                {
                    f.Configure(false);
                    Check.Equal(1, f.Control.Resources.MergedDictionaries.Count);
                    Check.Same(consumer, f.Control.Resources.MergedDictionaries[0]);
                    Check.Same(light, f.Control.Resources.ThemeDictionaries["Light"]);
                    f.Configure(true);
                    Check.Equal(2, f.Control.Resources.MergedDictionaries.Count);
                    Check.Same(light, f.Control.Resources.ThemeDictionaries["Light"]);
                    await f.State("PointerOver", f.Hover);
                }
            });
            tests.Test($"Fluent resources: {kind} adding and removing a consumer state is reconciled on refresh", async () =>
            {
                using var f = new Fixture(menu, ElementTheme.Light);
                var consumer = new ResourceDictionary();
                f.Control.Resources.ThemeDictionaries["Light"] = consumer;
                await f.Show();
                await f.State("PointerOver", f.Hover);
                var custom = new SolidColorBrush(Microsoft.UI.Colors.Tomato);
                consumer[f.PressedKey] = custom;
                f.Configure(true);
                await f.State("Pressed", custom);
                consumer.Remove(f.PressedKey);
                f.Configure(true);
                await f.State("Normal", null);
                await f.State("Pressed", f.Pressed);
                Check.Same(consumer, f.Control.Resources.ThemeDictionaries["Light"]);
                Check.Equal(0, consumer.Count);
            });
        }

        return tests.Run(output, "fluent-state-resources");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Control Control;
        internal readonly SolidColorBrush Hover = new(Microsoft.UI.Colors.CornflowerBlue);
        internal readonly SolidColorBrush Pressed = new(Microsoft.UI.Colors.SlateBlue);
        internal readonly string HoverKey, PressedKey;
        private readonly DockingManager _manager;
        private readonly Window _window;
        private readonly ElementTheme _theme;
        private readonly bool _menu;
        internal Fixture(bool menu, ElementTheme theme)
        {
            _menu = menu;
            _theme = theme;
            HoverKey = menu ? "MenuFlyoutItemBackgroundPointerOver" : "ButtonBackgroundPointerOver";
            PressedKey = menu ? "MenuFlyoutItemBackgroundPressed" : "ButtonBackgroundPressed";
            var type = typeof(DockingManager).Assembly.GetType("UnoDock.Internal." + (menu ? "DockMenuRow" : "DockChromeButton"), true)!;
            Control = (Control)Activator.CreateInstance(type, true)!;
            Control.Width = 260;
            Control.Height = 40;
            Control.RequestedTheme = theme;
            if (Control is Button button)
                button.Content = "Application-styled action";
            if (Control is MenuFlyoutItem row)
                row.Text = "Application-styled menu";
            _manager = new DockingManager
            {
                RequestedTheme = theme,
                Theme = new FluentTheme(theme)
            };
            _manager.Resources["UnoDock.HoverBrush"] = Hover;
            _manager.Resources["UnoDock.PressedBrush"] = Pressed;
            _window = new Window
            {
                Content = Control,
                Title = "UnoDock Fluent state resources"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 520,
                Height = 300
            });
        }

        internal async Task Show()
        {
            Configure(true);
            _window.Activate();
            await Wait(() => Control.IsLoaded && Control.ActualWidth > 0);
            Control.ApplyTemplate();
            Control.UpdateLayout();
            await Task.Delay(40);
        }

        internal void Configure(bool fluent)
        {
            _manager.Theme = fluent ? new FluentTheme(_theme) : new GenericTheme();
            var assembly = typeof(DockingManager).Assembly;
            var type = assembly.GetType("UnoDock.Internal." + (_menu ? "DockMenuPalette" : "DockChrome"), true)!;
            var palette = type.GetMethod(_menu ? "Resolve" : "Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [_manager])!;
            try
            {
                Control.GetType().GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Control, [palette]);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            }
        }

        internal async Task State(string name, Brush? expected)
        {
            Control.ApplyTemplate();
            Control.UpdateLayout();
            await Task.Delay(20);
            Check.True(VisualStateManager.GoToState(Control, name, false), "Missing platform state " + name);
            if (expected != null)
            {
                for (var i = 0; i < 100 && !Control.FindVisualChildren<FrameworkElement>().Any(element => ReferenceEquals(Background(element), expected)); i++)
                    await Task.Delay(20);
                var found = Control.FindVisualChildren<FrameworkElement>().Any(element => ReferenceEquals(Background(element), expected));
                Check.True(found, $"The actual Fluent template did not resolve the expected consumer resource in {name}: expected {(expected as SolidColorBrush)?.Color}; backgrounds [{string.Join(", ", Control.FindVisualChildren<FrameworkElement>().Select(Background).OfType<SolidColorBrush>().Select(b => b.Color.ToString()).Distinct())}]; merged {Control.Resources.MergedDictionaries.Count}; themes [{string.Join(",", Control.Resources.ThemeDictionaries.Keys)}]; {Published(expected)}.");
            }
        }

        private string Published(Brush expected)
        {
            var parts = new List<string>();
            var key = ReferenceEquals(expected, Pressed) ? PressedKey : HoverKey;
            parts.Add("lookup " + (Control.Resources.TryGetValue(key, out var found) ? (ReferenceEquals(found, expected) ? "expected" : (found as SolidColorBrush)?.Color.ToString()) : "none"));
            for (var i = 0; i < Control.Resources.MergedDictionaries.Count; i++)
            {
                var merged = Control.Resources.MergedDictionaries[i];
                foreach (var theme in new[]
                {
                    "Light",
                    "Dark"
                }

                )
                    if (merged.ThemeDictionaries.TryGetValue(theme, out var t) && t is ResourceDictionary d)
                        parts.Add($"merged[{i}].{theme} " + (d.TryGetValue(key, out var v) ? (ReferenceEquals(v, expected) ? "expected" : (v as SolidColorBrush)?.Color.ToString()) : "none"));
            }

            return string.Join("; ", parts);
        }

        public void Dispose()
        {
            _window.Content = null;
            _window.Close();
            _manager.Dispose();
        }
    }

    private static Brush? Background(FrameworkElement element) => element switch
    {
        Border border => border.Background,
        Panel panel => panel.Background,
        ContentPresenter presenter => presenter.Background,
        Control control => control.Background,
        _ => null
    };
    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
            await Task.Delay(20);
        Check.True(predicate(), "The actual Fluent template did not resolve the expected consumer resource.");
    }
}
