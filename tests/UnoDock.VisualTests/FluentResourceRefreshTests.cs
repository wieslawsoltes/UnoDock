using System.Reflection;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class FluentResourceRefreshTests
{
    internal static void Add(TestRunner tests, GalleryPage page, Window window)
    {
        foreach (var menuRow in new[]
        {
            false,
            true
        }

        )
            foreach (var state in new[]
            {
                "PointerOver",
                "Pressed"
            }

            )
                foreach (var themeChange in new[]
                {
                    false,
                    true
                }

                )
                {
                    var kind = menuRow ? "menu" : "button";
                    var change = themeChange ? "theme change" : "palette replacement";
                    tests.Test($"Fluent resources: active {kind} {state} follows {change}", async () =>
                    {
                        page.SetSampleTheme(SampleTheme.Light);
                        await Settle(page);
                        MenuFlyout? menu = null;
                        var control = menuRow ? await OpenMenu() : Label(page);
                        var template = control.Template;
                        var member = state == "PointerOver" ? "Hover" : "Pressed";
                        var resource = "UnoDock." + (menuRow ? "Menu" : "") + member + "Brush";
                        var replacement = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue);
                        var clicks = 0;
                        RoutedEventHandler clicked = (_, _) => clicks++;
                        if (control is Button button)
                            button.Click += clicked;
                        try
                        {
                            Check.True(VisualStateManager.GoToState(control, state, false));
                            if (themeChange)
                                page.SetSampleTheme(SampleTheme.Dark);
                            else
                                page.Dock.Resources[resource] = replacement;
                            await Settle(page);
                            var brush = themeChange ? PaletteBrush(page.Dock, member) : replacement;
                            Check.True(HasBackground(control, brush), "The active native state retained an obsolete palette brush.");
                            Check.Same(template, control.Template);
                            Check.Equal(0, clicks);
                        }
                        finally
                        {
                            if (control is Button releasedButton)
                                releasedButton.Click -= clicked;
                            VisualStateManager.GoToState(control, "Normal", false);
                            menu?.Hide();
                            page.Dock.Resources.Remove(resource);
                            page.SetSampleTheme(SampleTheme.Light);
                            await Settle(page);
                        }

                        async Task<Control> OpenMenu()
                        {
                            var tab = page.Dock.FindVisualChildren<LayoutDocumentTabItem>().First();
                            menu = (MenuFlyout)tab.ContextFlyout;
                            var opened = false;
                            void Opened(object? sender, object args) => opened = true;
                            menu.Opened += Opened;
                            try
                            {
                                // Native WinUI ignores ShowAt while the same flyout is still closing.
                                for (var i = 0; i < 50 && menu.IsOpen; i++)
                                    await Task.Delay(20);
                                menu.ShowAt(tab);
                                await Wait(() => opened && menu.Items.OfType<MenuFlyoutItem>().Any(item => item.IsEnabled && item.ActualHeight > 0));
                                // Let the native flyout complete its initial focus/state
                                // projection before explicitly testing a retained state.
                                await Task.Delay(80);
                                return menu.Items.OfType<MenuFlyoutItem>().First(item => item.IsEnabled && item.ActualHeight > 0);
                            }
                            finally
                            {
                                menu.Opened -= Opened;
                            }
                        }
                    });
                }

        tests.Test("Fluent resources: construction preserves application theme dictionaries", () =>
        {
            var button = new Button();
            var light = new ResourceDictionary
            {
                ["ApplicationMarker"] = "light"
            };
            var dark = new ResourceDictionary
            {
                ["ApplicationMarker"] = "dark"
            };
            var contrast = new ResourceDictionary
            {
                ["ApplicationMarker"] = "contrast"
            };
            button.Resources.ThemeDictionaries["Light"] = light;
            button.Resources.ThemeDictionaries["Dark"] = dark;
            button.Resources.ThemeDictionaries["HighContrast"] = contrast;
            // Internal helper protocol; actual native-state behavior is tested above.
            var type = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockControlStateResources", true)!;
            _ = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [button], null);
            Check.Same(light, button.Resources.ThemeDictionaries["Light"]);
            Check.Same(dark, button.Resources.ThemeDictionaries["Dark"]);
            Check.Same(contrast, button.Resources.ThemeDictionaries["HighContrast"]);
        });
        foreach (var theme in new[]
        {
            SampleTheme.Light,
            SampleTheme.Dark
        }

        )
        {
            tests.Test("Fluent resources: application state overrides remain authoritative in " + theme, async () =>
            {
                page.SetSampleTheme(theme);
                await Settle(page);
                var label = Label(page);
                var key = theme.ToString();
                var dictionaries = label.Resources.ThemeDictionaries;
                var previous = dictionaries.Keys.Contains(key) ? dictionaries[key] : null;
                var hover = new SolidColorBrush(Microsoft.UI.Colors.DarkCyan);
                var application = new ResourceDictionary
                {
                    ["ButtonBackgroundPointerOver"] = hover
                };
                dictionaries[key] = application;
                try
                {
                    Check.True(VisualStateManager.GoToState(label, "PointerOver", false));
                    await Settle(page);
                    Check.Same(application, dictionaries[key]);
                    Check.True(HasBackground(label, hover));
                    var replacement = new SolidColorBrush(Microsoft.UI.Colors.SlateBlue);
                    application["ButtonBackgroundPointerOver"] = replacement;
                    await Settle(page);
                    Check.True(HasBackground(label, replacement));
                    Check.Same(application, dictionaries[key]);
                }
                finally
                {
                    VisualStateManager.GoToState(label, "Normal", false);
                    if (previous == null)
                        dictionaries.Remove(key);
                    else
                        dictionaries[key] = previous;
                    await Settle(page);
                }
            });
        }

        tests.Test("Fluent resources: replacing a control resource scope retains the application scope", async () =>
        {
            page.SetSampleTheme(SampleTheme.Dark);
            await Settle(page);
            var label = Label(page);
            var original = label.Resources;
            var marker = new object();
            var replacement = new ResourceDictionary
            {
                ["ApplicationMarker"] = marker
            };
            label.Resources = replacement;
            try
            {
                VisualStateManager.GoToState(label, "PointerOver", false);
                await Settle(page);
                Check.Same(replacement, label.Resources);
                Check.Same(marker, replacement["ApplicationMarker"]);
                Check.True(HasBackground(label, PaletteBrush(page.Dock, "Hover")));
                Check.Equal(0, original.MergedDictionaries.Count);
                Check.Equal(1, replacement.MergedDictionaries.Count);
            }
            finally
            {
                VisualStateManager.GoToState(label, "Normal", false);
                label.Resources = original;
                await Settle(page);
                Check.Equal(0, replacement.MergedDictionaries.Count);
            }
        });
        tests.Test("Fluent resources: repeated Generic round trips release only the owned fallback", async () =>
        {
            page.SetSampleTheme(SampleTheme.Light);
            await Settle(page);
            var label = Label(page);
            var marker = new ResourceDictionary
            {
                ["ApplicationMarker"] = "retained"
            };
            label.Resources.MergedDictionaries.Add(marker);
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    page.SetSampleTheme(SampleTheme.Generic);
                    await Settle(page);
                    Check.Equal(1, label.Resources.MergedDictionaries.Count);
                    Check.Same(marker, label.Resources.MergedDictionaries[0]);
                    page.SetSampleTheme(SampleTheme.Dark);
                    await Settle(page);
                    Check.Equal(2, label.Resources.MergedDictionaries.Count);
                    Check.Same(marker, label.Resources.MergedDictionaries[1]);
                }
            }
            finally
            {
                label.Resources.MergedDictionaries.Remove(marker);
            }
        });
        tests.Test("Fluent resources: native state callbacks may supersede palette replay", async () =>
        {
            page.SetSampleTheme(SampleTheme.Light);
            await Settle(page);
            var label = Label(page);
            var group = label.FindVisualChildren<FrameworkElement>().SelectMany(VisualStateManager.GetVisualStateGroups).First(item => item.Name == "CommonStates");
            VisualStateManager.GoToState(label, "PointerOver", false);
            var redirects = 0;
            void Redirect(object? sender, VisualStateChangedEventArgs args)
            {
                if (args.NewState?.Name == "Normal" && redirects == 0)
                {
                    redirects++;
                    VisualStateManager.GoToState(label, "Disabled", false);
                }
            }

            group.CurrentStateChanged += Redirect;
            try
            {
                page.Dock.Resources["UnoDock.HoverBrush"] = new SolidColorBrush(Microsoft.UI.Colors.SlateGray);
                await Settle(page);
                Check.Equal(1, redirects);
                Check.Equal("Disabled", group.CurrentState?.Name);
            }
            finally
            {
                group.CurrentStateChanged -= Redirect;
                VisualStateManager.GoToState(label, "Normal", false);
                page.Dock.Resources.Remove("UnoDock.HoverBrush");
                await Settle(page);
            }
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            tests.Test("XTEST Fluent: live palette repaint preserves held input and activates only on release", async () =>
            {
                page.SetSampleTheme(SampleTheme.Light);
                await Settle(page);
                var label = Label(page);
                var template = label.Template;
                var clicks = 0;
                RoutedEventHandler clicked = (_, _) => clicks++;
                label.Click += clicked;
                using var input = new X11TestInput();
                try
                {
                    window.Activate();
                    input.MoveTo(label, new(label.ActualWidth / 2, label.ActualHeight / 2));
                    await Task.Delay(100);
                    await Wait(() => HasBackground(label, PaletteBrush(page.Dock, "Hover")));
                    page.SetSampleTheme(SampleTheme.Dark);
                    await Settle(page);
                    Check.True(HasBackground(label, PaletteBrush(page.Dock, "Hover")));
                    Check.Equal(0, clicks);
                    Check.True(label.Focus(FocusState.Keyboard));
                    await Task.Delay(60);
                    input.KeyDown(0x20);
                    await Wait(() => label.IsPressed);
                    page.SetSampleTheme(SampleTheme.Light);
                    await Settle(page);
                    Check.True(label.IsPressed, "Palette repaint canceled the native key press.");
                    Check.True(HasBackground(label, PaletteBrush(page.Dock, "Pressed")));
                    Check.Equal(0, clicks);
                    input.KeyUp(0x20);
                    await Wait(() => !label.IsPressed && clicks == 1);
                    Check.Same(template, label.Template);
                }
                finally
                {
                    label.Click -= clicked;
                }
            });
        }
    }

    private static async Task Settle(GalleryPage page)
    {
        page.Dock.Refresh();
        page.UpdateLayout();
        await Task.Delay(50);
    }

    private static Button Label(GalleryPage page)
    {
        var tab = page.Dock.FindVisualChildren<LayoutDocumentTabItem>().First();
        return (Button)typeof(LayoutTabItemBase).GetField("_label", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tab)!;
    }

    private static Brush PaletteBrush(DockingManager manager, string name)
    {
        var palette = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
        return (Brush)palette.GetType().GetProperty(name)!.GetValue(palette)!;
    }

    private static bool HasBackground(FrameworkElement control, Brush brush) => control.FindVisualChildren<FrameworkElement>().Any(element => ReferenceEquals(element switch
    {
        ContentPresenter presenter => presenter.Background,
        Border border => border.Background,
        Panel panel => panel.Background,
        Control child => child.Background,
        _ => null
    }, brush));
    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Fluent state fixture did not reach its expected native state.");
    }
}
