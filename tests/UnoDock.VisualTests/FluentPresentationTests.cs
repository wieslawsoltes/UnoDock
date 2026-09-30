using System.Reflection;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Testing;

internal static class FluentPresentationTests
{
    internal static async Task<int> Run(string output)
    {
        using var page = new GalleryPage
        {
            Width = 1100,
            Height = 760
        };
        var window = new Window
        {
            Content = page,
            Title = "UnoDock Fluent presentation acceptance"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1180,
            Height = 860
        });
        window.Activate();
        try
        {
            await Wait(() => page.IsLoaded && page.Dock.ActualWidth > 0);
            var tests = new TestRunner();
            FluentGalleryThemeTests.Add(tests, page);
            var initialRoot = page.Dock.Layout;
            var docs = initialRoot.Descendents().OfType<LayoutDocument>().ToArray();
            var editors = docs.Select(d => d.Content).ToArray();
            tests.Test("Fluent: Gallery starts in Light with comfortable native controls", () =>
            {
                Check.Equal(SampleTheme.Light, page.CurrentSampleTheme);
                Check.True(page.Dock.Theme is FluentTheme);
                Check.Equal(DockChromeDensity.Comfortable, page.Dock.ChromeDensity);
                Check.Equal(26d, Metric(page.Dock, "TitleHeight"));
            });
            tests.Test("Fluent: toolbar commands use stock buttons and compiled font icons", () =>
            {
                foreach (var command in new[]
                {
                    "new",
                    "save",
                    "restore",
                    "float",
                    "reset"
                }

                )
                {
                    var button = page.FindVisualChildren<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "SampleToolbar-" + command);
                    Check.Equal(typeof(Button), button.GetType());
                    Check.True(button.Template != null && button.UseSystemFocusVisuals);
                    Check.True(button.MinWidth >= 28 && button.MinHeight >= 28);
                    Check.True(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                    if (command != "reset")
                        Check.True(button.Content is FontIcon);
                }
            });
            foreach (var mode in new[]
            {
                SampleTheme.Light,
                SampleTheme.Dark
            }

            )
            {
                tests.Test("Fluent: native Button common states and semantic text in " + mode, async () =>
                {
                    page.SetSampleTheme(mode);
                    await Settle();
                    var tab = Tab(docs[0]);
                    var label = Field<Button>(tab, "_label");
                    var close = Field<Button>(tab, "_close");
                    Check.False(ReferenceEquals(LegacyTemplate(), label.Template));
                    Check.True(label.Template != null && close.Template != null);
                    Check.True(label.UseSystemFocusVisuals && close.UseSystemFocusVisuals);
                    var states = label.FindVisualChildren<FrameworkElement>().SelectMany(VisualStateManager.GetVisualStateGroups).SelectMany(g => g.States).Select(s => s.Name).ToHashSet();
                    Check.True(new[] { "Normal", "PointerOver", "Pressed", "Disabled" }.All(states.Contains), "The platform button template did not expose its native common states.");
                    Check.Equal(1d, label.Opacity);
                    Check.Equal(new CornerRadius(3), label.CornerRadius);
                    Check.Equal(0d, label.BorderThickness.Left);
                });
                tests.Test("Fluent: tab surfaces distinguish active, inactive and disabled text in " + mode, async () =>
                {
                    page.SetSampleTheme(mode);
                    docs[0].IsActive = true;
                    await Settle();
                    var active = Tab(docs[0]);
                    var inactive = Tab(docs[1]);
                    var label = Field<Button>(inactive, "_label");
                    Check.Same(Brush(page.Dock, "SecondaryForeground"), label.Foreground);
                    Check.Equal(Microsoft.UI.Text.FontWeights.SemiBold, Field<Button>(active, "_label").FontWeight);
                    docs[1].IsEnabled = false;
                    await Settle();
                    Check.Same(Brush(page.Dock, "DisabledForeground"), label.Foreground);
                    Check.Equal(1d, label.Opacity);
                    Check.False(label.IsEnabled);
                    docs[1].IsEnabled = true;
                    var chrome = Field<Grid>(active, "_chrome");
                    Check.Equal(new CornerRadius(4, 4, 0, 0), chrome.CornerRadius);
                    Check.Equal(new Thickness(6, 0, 6, 0), chrome.Padding);
                });
                tests.Test("Fluent: selection indicator is retained and cannot intercept dragging in " + mode, async () =>
                {
                    page.SetSampleTheme(mode);
                    docs[0].IsActive = true;
                    await Settle();
                    var tab = Tab(docs[0]);
                    var indicator = Field<Border>(tab, "_selectionIndicator");
                    Check.False(indicator.IsHitTestVisible);
                    Check.Equal(Visibility.Visible, indicator.Visibility);
                    Check.Equal(2d, indicator.Height);
                    Check.Same(Brush(page.Dock, "Accent"), indicator.Background);
                    docs[1].IsActive = true;
                    await Settle();
                    Check.Same(indicator, Field<Border>(tab, "_selectionIndicator"));
                    Check.Equal(Visibility.Collapsed, indicator.Visibility);
                });
                tests.Test("Fluent: inspector disclosure resources are neutral and remain native in " + mode, async () =>
                {
                    page.SetSampleTheme(mode);
                    await Settle();
                    var category = page.PropertyInspector!.FindVisualChildren<ToggleButton>().First(b => b.Name == "CategoryToggle");
                    Check.Equal(typeof(ToggleButton), category.GetType());
                    Check.True(category.IsChecked == true && category.Template != null);
                    var colors = category.FindVisualChildren<FrameworkElement>().Select(Background).OfType<SolidColorBrush>().Select(b => b.Color).Where(c => c.A > 0).ToArray();
                    Check.True(colors.Length > 0);
                    Check.True(colors.All(c => Math.Abs(c.R - c.B) < 25 && Math.Abs(c.R - c.G) < 25), "A disclosure header used an accent command fill instead of a neutral surface.");
                });
                tests.Test("Fluent: context menu uses native templates and survives live theme changes from " + mode, async () =>
                {
                    page.SetSampleTheme(mode);
                    docs[0].IsActive = true;
                    await Settle();
                    var menu = Tab(docs[0]).ContextFlyout as MenuFlyout;
                    Check.True(menu != null);
                    menu!.ShowAt(Tab(docs[0]));
                    try
                    {
                        await Wait(() => VisualTreeHelper.GetOpenPopupsForXamlRoot(page.XamlRoot).Any());
                        await Task.Delay(60);
                        var row = menu.Items.OfType<MenuFlyoutItem>().First(i => i.IsEnabled && i.Visibility == Visibility.Visible);
                        Check.True(row.Template != null);
                        Check.False(row.FindVisualChildren<Border>().Any(b => b.Name == "PART_MenuGutter"));
                        Check.True(row.ActualHeight >= 28 && row.UseSystemFocusVisuals);
                        var template = row.Template;
                        page.SetSampleTheme(mode == SampleTheme.Dark ? SampleTheme.Light : SampleTheme.Dark);
                        await Settle();
                        Check.Same(row, menu.Items.OfType<MenuFlyoutItem>().First(i => i.IsEnabled && i.Visibility == Visibility.Visible));
                        Check.Same(template, row.Template);
                        Check.True(row.Focus(FocusState.Keyboard));
                    }
                    finally
                    {
                        menu.Hide();
                    }
                });
            }

            tests.Test("Fluent: theme changes retain root, editors, tabs and native style identity", async () =>
            {
                page.SetSampleTheme(SampleTheme.Light);
                await Settle();
                var tab = Tab(docs[0]);
                var label = Field<Button>(tab, "_label");
                var template = label.Template;
                var menu = tab.ContextFlyout;
                foreach (var mode in new[]
                {
                    SampleTheme.Dark,
                    SampleTheme.Light,
                    SampleTheme.Dark
                }

                )
                {
                    page.SetSampleTheme(mode);
                    await Settle();
                    Check.Same(initialRoot, page.Dock.Layout);
                    Check.True(docs.Select(d => d.Content).SequenceEqual(editors));
                    Check.Same(tab, Tab(docs[0]));
                    Check.Same(label, Field<Button>(tab, "_label"));
                    Check.Same(template, label.Template);
                    Check.Same(menu, tab.ContextFlyout);
                }
            });
            tests.Test("Fluent: resource bindings survive repeated themes and a Generic round trip", async () =>
            {
                var shell = Field<Grid>(page, "_sampleShell");
                var status = Field<TextBlock>(page, "_status");
                foreach (var mode in new[]
                {
                    SampleTheme.Light,
                    SampleTheme.Dark,
                    SampleTheme.Dark,
                    SampleTheme.Generic,
                    SampleTheme.Dark,
                    SampleTheme.Light
                }

                )
                {
                    page.SetSampleTheme(mode);
                    await Settle();
                    var surface = ((SolidColorBrush)shell.Background).Color;
                    var text = ((SolidColorBrush)status.Foreground).Color;
                    if (mode == SampleTheme.Dark)
                    {
                        Check.True(surface.R < 80 && text.R > 140, "Dark presentation lost its semantic background or text resource.");
                    }
                    else
                    {
                        Check.True(surface.R > 200 && text.R < 140, "Light presentation lost its semantic background or text resource.");
                    }

                    Check.Same(initialRoot, page.Dock.Layout);
                }
            });
            tests.Test("Fluent: explicit generic mode restores legacy metrics and templates", async () =>
            {
                page.SetSampleTheme(SampleTheme.Generic);
                await Settle();
                Check.Equal(18d, Metric(page.Dock, "TitleHeight"));
                Check.Equal(20d, Metric(page.Dock, "TabHeight"));
                Check.Equal(0d, Metric(page.Dock, "TabCornerRadius"));
                Check.Equal(0d, Metric(page.Dock, "PaneCornerRadius"));
                Check.Same(LegacyTemplate(), Field<Button>(Tab(docs[0]), "_label").Template);
                Check.Same(initialRoot, page.Dock.Layout);
                page.SetSampleTheme(SampleTheme.Light);
                await Settle();
                Check.Equal(26d, Metric(page.Dock, "TitleHeight"));
            });
            tests.Test("Fluent: local shape tokens override defaults without replacing controls", async () =>
            {
                page.SetSampleTheme(SampleTheme.Dark);
                await Settle();
                var tab = Tab(docs[0]);
                page.Dock.Resources["UnoDock.TabCornerRadius"] = 7d;
                page.Dock.Resources["UnoDock.TabHorizontalPadding"] = 9d;
                page.Dock.Resources["UnoDock.PaneCornerRadius"] = 8d;
                try
                {
                    await Settle();
                    Check.Same(tab, Tab(docs[0]));
                    Check.Equal(new CornerRadius(7, 7, 0, 0), Field<Grid>(tab, "_chrome").CornerRadius);
                    Check.Equal(9d, Field<Grid>(tab, "_chrome").Padding.Left);
                    Check.Equal(8d, Metric(page.Dock, "PaneCornerRadius"));
                    page.Dock.Resources["UnoDock.TabCornerRadius"] = double.NaN;
                    await Settle();
                    Check.Equal(4d, Metric(page.Dock, "TabCornerRadius"));
                }
                finally
                {
                    foreach (var key in new[]
                    {
                        "TabCornerRadius",
                        "TabHorizontalPadding",
                        "PaneCornerRadius"
                    }

                    )
                        page.Dock.Resources.Remove("UnoDock." + key);
                    await Settle();
                }
            });
            tests.Test("Fluent: native Button states resolve scoped palette brushes without copying them", async () =>
            {
                page.SetSampleTheme(SampleTheme.Light);
                var hover = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue);
                var pressed = new SolidColorBrush(Microsoft.UI.Colors.SlateBlue);
                page.Dock.Resources["UnoDock.HoverBrush"] = hover;
                page.Dock.Resources["UnoDock.PressedBrush"] = pressed;
                var label = Field<Button>(Tab(docs[0]), "_label");
                try
                {
                    await Settle();
                    var template = label.Template;
                    Check.True(VisualStateManager.GoToState(label, "PointerOver", false));
                    await Wait(() => label.FindVisualChildren<FrameworkElement>().Any(e => ReferenceEquals(Background(e), hover)));
                    Check.True(VisualStateManager.GoToState(label, "Pressed", false));
                    await Wait(() => label.FindVisualChildren<FrameworkElement>().Any(e => ReferenceEquals(Background(e), pressed)));
                    Check.Same(template, label.Template);
                    Check.False(label.Resources.ThemeDictionaries.ContainsKey("HighContrast"));
                }
                finally
                {
                    VisualStateManager.GoToState(label, "Normal", false);
                    page.Dock.Resources.Remove("UnoDock.HoverBrush");
                    page.Dock.Resources.Remove("UnoDock.PressedBrush");
                    await Settle();
                }
            });
            tests.Test("Fluent: native menu states retain scoped application brush identities", async () =>
            {
                page.SetSampleTheme(SampleTheme.Dark);
                var hover = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue);
                var pressed = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray);
                page.Dock.Resources["UnoDock.MenuHoverBrush"] = hover;
                page.Dock.Resources["UnoDock.MenuPressedBrush"] = pressed;
                await Settle();
                var menu = (MenuFlyout)Tab(docs[0]).ContextFlyout;
                menu.ShowAt(Tab(docs[0]));
                try
                {
                    await Wait(() => menu.Items.OfType<MenuFlyoutItem>().Any(row => row.ActualHeight > 0));
                    var row = menu.Items.OfType<MenuFlyoutItem>().First(item => item.IsEnabled && item.Visibility == Visibility.Visible);
                    var template = row.Template;
                    Check.True(VisualStateManager.GoToState(row, "PointerOver", false));
                    await Wait(() => row.FindVisualChildren<FrameworkElement>().Any(e => ReferenceEquals(Background(e), hover)));
                    Check.True(VisualStateManager.GoToState(row, "Pressed", false));
                    await Wait(() => row.FindVisualChildren<FrameworkElement>().Any(e => ReferenceEquals(Background(e), pressed)));
                    Check.Same(template, row.Template);
                    Check.False(row.Resources.ThemeDictionaries.ContainsKey("HighContrast"));
                }
                finally
                {
                    menu.Hide();
                    page.Dock.Resources.Remove("UnoDock.MenuHoverBrush");
                    page.Dock.Resources.Remove("UnoDock.MenuPressedBrush");
                    await Settle();
                }
            });
            tests.Test("Fluent: high-contrast dictionary supplies every semantic chrome slot", () =>
            {
                var resources = new WorkbenchResources();
                Check.True(resources.ThemeDictionaries.ContainsKey("HighContrast"));
                var contrast = (ResourceDictionary)resources.ThemeDictionaries["HighContrast"];
                foreach (var key in new[]
                {
                    "PaneBrush",
                    "HeaderBrush",
                    "InactiveTabBrush",
                    "BorderBrush",
                    "ForegroundBrush",
                    "SecondaryForegroundBrush",
                    "DisabledForegroundBrush",
                    "HoverBrush",
                    "PressedBrush",
                    "AccentBrush",
                    "ActiveTitleBrush"
                }

                )
                    Check.True(contrast["UnoDock." + key] is Brush, "Missing high-contrast resource: " + key);
            });
            FluentResourceRefreshTests.Add(tests, page, window);
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                foreach (var mode in new[]
                {
                    SampleTheme.Light,
                    SampleTheme.Generic
                }

                )
                {
                    tests.Test("XTEST Fluent: keyboard press uses ButtonBase state and activates once in " + mode, async () =>
                    {
                        page.SetSampleTheme(mode);
                        docs[0].IsActive = true;
                        await Settle();
                        var label = Field<Button>(Tab(docs[0]), "_label");
                        var clicks = 0;
                        RoutedEventHandler clicked = (_, _) => clicks++;
                        label.Click += clicked;
                        using var input = new X11TestInput();
                        try
                        {
                            window.Activate();
                            // A bare Xvfb display starts with PointerRoot keyboard
                            // focus. Put the real pointer over the intended native
                            // host before assigning its XAML keyboard focus.
                            input.MoveTo(label, new(label.ActualWidth / 2, label.ActualHeight / 2));
                            await Task.Delay(100);
                            Check.True(label.Focus(FocusState.Keyboard));
                            await Wait(() => label.XamlRoot is { } root && ReferenceEquals(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root), label));
                            await Task.Delay(60);
                            var resting = label.Background;
                            input.KeyDown(0x20);
                            await Wait(() => label.IsPressed);
                            Check.Equal(0, clicks);
                            // The active tab keeps its state fill while pressed.
                            if (mode == SampleTheme.Generic)
                                Check.Same(resting, label.Background);
                            else
                            {
                                await Wait(() => label.FindVisualChildren<FrameworkElement>().SelectMany(VisualStateManager.GetVisualStateGroups).Any(group => group.Name == "CommonStates" && group.CurrentState?.Name == "Pressed"));
                            }

                            input.KeyUp(0x20);
                            await Wait(() => !label.IsPressed && clicks == 1);
                            Check.Same(docs[0], page.Dock.Layout.ActiveContent);
                        }
                        finally
                        {
                            label.Click -= clicked;
                        }
                    });
                }
            }

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
                tests.Test("Fluent: rendered workspace retains accessible controls: " + scene, async () =>
                {
                    page.SetSampleTheme(scene == "light" ? SampleTheme.Light : SampleTheme.Dark);
                    page.Dock.FlowDirection = scene == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    page.Width = scene == "narrow" ? 640 : 1100;
                    page.Height = scene == "narrow" ? 560 : 760;
                    page.Dock.Resources["UnoDock.FontSize"] = scene == "large-text" ? 18d : 12d;
                    docs[1].IsActive = true;
                    await Settle();
                    await Task.Delay(100);
                    Check.True(page.Dock.ActualHeight >= page.ActualHeight - 90);
                    Check.True(Tab(docs[1]).ActualHeight >= Metric(page.Dock, "FontSize"));
                    var menu = page.FindVisualChildren<MenuBar>().Single();
                    Check.True(menu.ActualHeight <= 28.1);
                    foreach (var item in menu.Items)
                        Check.True(item.ActualWidth > 20);
                    var path = Path.Combine(output, "visuals", "fluent-workspace-" + scene + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await VisualCapture.Save(page, path);
                });
            }

            return await tests.Run(output, "fluent-presentation");
            async Task Settle()
            {
                page.Dock.Refresh();
                page.UpdateLayout();
                await Task.Delay(50);
            }

            LayoutTabItemBase Tab(LayoutDocument document) => page.Dock.FindVisualChildren<LayoutTabItemBase>().Single(t => ReferenceEquals(t.Model, document));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static Brush? Background(FrameworkElement element) => element switch
    {
        ContentPresenter presenter => presenter.Background,
        Border border => border.Background,
        Panel panel => panel.Background,
        Control control => control.Background,
        _ => null
    };
    private static object Palette(DockingManager manager) => typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
    private static double Metric(DockingManager manager, string name) => (double)Palette(manager).GetType().GetProperty(name)!.GetValue(Palette(manager))!;
    private static Brush Brush(DockingManager manager, string name) => (Brush)Palette(manager).GetType().GetProperty(name)!.GetValue(Palette(manager))!;
    private static ControlTemplate LegacyTemplate() => (ControlTemplate)typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetProperty("ButtonTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private static T Field<T>(object value, string name)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
                return (T)field.GetValue(value)!;
        throw new MissingFieldException(name);
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Fluent presentation did not reach the expected native state.");
    }
}
