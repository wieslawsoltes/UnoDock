using System.Reflection;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Testing;
/// <summary>Runs the compiled consumer XAML on the real Uno UI thread.</summary>
internal static partial class XamlWorkspaceTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        AddBindingAndPaletteTests(tests, output);
        tests.Test("compiled XAML: nested layout, namescope and auto-hidden tool", async () =>
        {
            using var host = Declarative();
            await host.Show();
            var manager = host.Manager;
            Check.Equal(2, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
            Check.Equal(4, manager.Layout.Descendents().OfType<LayoutAnchorable>().Count());
            var help = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(item => item.ContentId == "declarative-help");
            Check.True(help.IsAutoHidden);
            Check.Same(manager.Layout.RightSide, help.Parent!.Parent);
            Check.Equal(260d, help.AutoHideWidth);
            Check.True(host.Page.FindName("Editor") is TextBox);
            AssertOwnership(manager.Layout);
        });
        tests.Test("compiled XAML: direct content survives float, dock and XML restoration", async () =>
        {
            using var host = Declarative();
            await host.Show();
            var page = host.Page;
            var editor = (TextBox)page.FindName("Editor");
            editor.Text = "Retained unsaved XAML buffer";
            var document = host.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
            var content = document.Content;
            page.Actions.Save();
            document.Float();
            host.Manager.Refresh();
            Check.True(document.IsFloating);
            Check.Same(content, document.Content);
            document.Dock();
            page.Actions.Restore();
            host.Manager.Refresh();
            Check.Same(content, host.Manager.Layout.Descendents().OfType<LayoutDocument>().Single(item => item.ContentId == document.ContentId).Content);
            Check.Equal("Retained unsaved XAML buffer", editor.Text);
            AssertOwnership(host.Manager.Layout);
        });
        tests.Test("compiled XAML: observable sources, item styles and template selector", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var page = host.Page;
            var manager = host.Manager;
            Check.Equal(2, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
            Check.Equal(2, manager.Layout.Descendents().OfType<LayoutAnchorable>().Count());
            var item = page.ViewModel.Documents[0];
            page.ViewModel.ActiveDocument = item;
            await Wait(() => ReferenceEquals(manager.ActiveContent, item));
            var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single(model => ReferenceEquals(model.Content, item));
            Check.Equal(item.ContentId, document.ContentId);
            item.Title = "Renamed through binding";
            await Wait(() => document.Title == item.Title);
            var adapter = manager.GetLayoutItemFromModel(document);
            adapter.Title = "Renamed through LayoutItem";
            await Wait(() => item.Title == adapter.Title);
            item.CanClose = false;
            await Wait(() => !document.CanClose);
            manager.Refresh();
            await Wait(() => manager.FindVisualChildren<XamlDocumentView>().Any());
            Check.True(manager.FindVisualChildren<XamlDocumentView>().Any(view => ReferenceEquals(view.DataContext, item)));
        });
        tests.Test("compiled XAML: add and remove commands reconcile actual source identities", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var vm = host.Page.ViewModel;
            vm.AddDocumentCommand.Execute(null);
            await Wait(() => host.Manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 3);
            var added = vm.Documents.Last();
            await Wait(() => ReferenceEquals(host.Manager.ActiveContent, added));
            vm.RemoveDocumentCommand.Execute(null);
            await Wait(() => host.Manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2);
            Check.False(vm.Documents.Contains(added));
            AssertOwnership(host.Manager.Layout);
        });
        tests.Test("compiled XAML: source models roundtrip without duplicate content identities", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var page = host.Page;
            page.Actions.Save();
            page.Actions.Restore();
            host.Manager.Refresh();
            var documents = host.Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
            Check.Equal(page.ViewModel.Documents.Count, documents.Length);
            foreach (var item in page.ViewModel.Documents)
                Check.Equal(1, documents.Count(document => ReferenceEquals(document.Content, item)));
        });
        tests.Test("compiled XAML: Content and nested Title x:Bind observe source replacement", async () =>
        {
            using var host = Templates();
            await host.Show();
            var replacement = new XamlWorkspaceItem
            {
                Title = "Replacement",
                Text = "Replacement buffer"
            };
            host.Page.ViewModel.PrimaryItem = replacement;
            var document = host.Manager.Layout.Descendents().OfType<LayoutDocument>().Single();
            await Wait(() => ReferenceEquals(document.Content, replacement) && document.Title == replacement.Title);
            replacement.Title = "Nested property change";
            await Wait(() => document.Title == replacement.Title);
            host.Manager.Refresh();
            await Wait(() => host.Manager.FindVisualChildren<XamlDocumentView>().Any(view => ReferenceEquals(view.DataContext, replacement)));
        });
        tests.Test("compiled XAML: repeated ControlTemplate replacement retains the surface and editor", async () =>
        {
            using var host = Templates();
            await host.Show();
            var manager = host.Manager;
            var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
            var presenter = manager.GetLayoutItemFromModel(document).View;
            await Wait(() => manager.FindVisualChildren<XamlDocumentView>().Any());
            var editor = manager.FindVisualChildren<XamlDocumentView>().First();
            for (var i = 0; i < 4; i++)
            {
                host.Page.SwapTemplate();
                manager.ApplyTemplate();
                manager.UpdateLayout();
                manager.Refresh();
                await Wait(() => presenter.IsLoaded);
                Check.Same(presenter, manager.GetLayoutItemFromModel(document).View);
                Check.True(manager.FindVisualChildren<XamlDocumentView>().Any(view => ReferenceEquals(view, editor)));
                Check.Same(document, manager.Layout.Descendents().OfType<LayoutDocument>().Single());
            }
        });
        foreach (var mode in new[]
        {
            ElementTheme.Light,
            ElementTheme.Dark
        }

        )
            tests.Test("theme: XAML-configurable same-instance explicit theme updates: " + mode, async () =>
            {
                using var host = Declarative();
                await host.Show();
                var theme = (FluentTheme)host.Manager.Theme!;
                theme.RequestedTheme = mode;
                await Wait(() => host.Manager.FindVisualChildren<LayoutDocumentPaneControl>().First().ActualTheme == mode);
                Check.Same(theme, host.Manager.Theme);
                Check.Equal(mode, (ElementTheme)theme.GetValue(FluentTheme.RequestedThemeProperty));
            });
        foreach (var density in new[]
        {
            DockChromeDensity.Compact,
            DockChromeDensity.Comfortable,
            DockChromeDensity.Spacious
        }

        )
            tests.Test("theme: density update preserves realized pane and content: " + density, async () =>
            {
                using var host = Declarative();
                await host.Show();
                var manager = host.Manager;
                var pane = manager.FindVisualChildren<LayoutDocumentPaneControl>().First();
                var document = manager.Layout.Descendents().OfType<LayoutDocument>().First();
                var content = document.Content;
                manager.ChromeDensity = density;
                var expected = density == DockChromeDensity.Compact ? 19d : density == DockChromeDensity.Comfortable ? 27d : 35d;
                await Wait(() => manager.FindVisualChildren<LayoutDocumentTabItem>().First().Height == expected);
                Check.Same(pane, manager.FindVisualChildren<LayoutDocumentPaneControl>().First());
                Check.Same(content, document.Content);
            });
        tests.Test("theme: inherited metric tokens and local overrides have stable precedence", async () =>
        {
            using var host = Declarative();
            await host.Show();
            host.Page.Resources["UnoDock.TabHeight"] = 38d;
            host.Manager.Refresh();
            Check.Equal(38d, Metric(host.Manager, "TabHeight"));
            host.Manager.Resources["UnoDock.TabHeight"] = 42d;
            host.Manager.Refresh();
            Check.Equal(42d, Metric(host.Manager, "TabHeight"));
            host.Manager.Resources.Remove("UnoDock.TabHeight");
            host.Manager.Refresh();
            Check.Equal(38d, Metric(host.Manager, "TabHeight"));
        });
        tests.Test("theme: consumer dictionary replacement and explicit refresh update retained views", async () =>
        {
            using var host = Declarative();
            await host.Show();
            var first = new SolidColorBrush(Microsoft.UI.Colors.Ivory);
            var second = new SolidColorBrush(Microsoft.UI.Colors.MidnightBlue);
            var theme = new ResourceDictionaryTheme
            {
                Resources = new ResourceDictionary
                {
                    ["UnoDock.PaneBrush"] = first
                }
            };
            host.Manager.Theme = theme;
            host.Manager.Refresh();
            Check.Same(first, PaletteProperty<Brush>(host.Manager, "Surface"));
            theme.Resources = new ResourceDictionary
            {
                ["UnoDock.PaneBrush"] = second
            };
            await Wait(() => ReferenceEquals(PaletteProperty<Brush>(host.Manager, "Surface"), second));
            theme.Resources["UnoDock.PaneBrush"] = first;
            theme.Refresh();
            Check.Same(first, PaletteProperty<Brush>(host.Manager, "Surface"));
        });
        tests.Test("theme: merged dictionaries precede selected theme and local entries win", async () =>
        {
            using var host = Declarative();
            await host.Show();
            var selected = new SolidColorBrush(Microsoft.UI.Colors.Red);
            var merged = new SolidColorBrush(Microsoft.UI.Colors.Blue);
            var local = new SolidColorBrush(Microsoft.UI.Colors.Green);
            var resources = new ResourceDictionary();
            resources.ThemeDictionaries["Light"] = new ResourceDictionary
            {
                ["UnoDock.PaneBrush"] = selected
            };
            resources.MergedDictionaries.Add(new ResourceDictionary { ["UnoDock.PaneBrush"] = merged });
            host.Page.RequestedTheme = ElementTheme.Light;
            host.Manager.Theme = new ResourceDictionaryTheme
            {
                Resources = resources
            };
            host.Manager.Refresh();
            Check.Same(merged, PaletteProperty<Brush>(host.Manager, "Surface"));
            resources["UnoDock.PaneBrush"] = local;
            host.Manager.Refresh();
            Check.Same(local, PaletteProperty<Brush>(host.Manager, "Surface"));
        });
        tests.Test("theme: switching explicit mode preserves consumer dictionary entries", () =>
        {
            var theme = new FluentTheme(ElementTheme.Light);
            var brush = new SolidColorBrush(Microsoft.UI.Colors.Purple);
            theme.ThemeResourceDictionary["UnoDock.AccentBrush"] = brush;
            theme.RequestedTheme = ElementTheme.Dark;
            Check.Same(brush, theme.ThemeResourceDictionary["UnoDock.AccentBrush"]);
            theme.RequestedTheme = ElementTheme.Default;
            Check.Same(brush, theme.ThemeResourceDictionary["UnoDock.AccentBrush"]);
        });
        tests.Test("theme: invalid CLR and native DP settings cannot poison subsequent changes", () =>
        {
            var theme = new FluentTheme();
            using var manager = new DockingManager
            {
                Theme = theme
            };
            Check.Throws<ArgumentOutOfRangeException>(() => manager.ChromeDensity = (DockChromeDensity)999);
            Check.Throws<ArgumentOutOfRangeException>(() => theme.SetValue(FluentTheme.RequestedThemeProperty, (ElementTheme)999));
            Check.Equal(ElementTheme.Default, theme.RequestedTheme);
            manager.ChromeDensity = DockChromeDensity.Spacious;
            Check.Equal(DockChromeDensity.Spacious, manager.ChromeDensity);
        });
        tests.Test("theme: replaced and disposed manager unsubscribes from theme definition", () =>
        {
            var theme = new FluentTheme();
            var manager = new DockingManager
            {
                Theme = theme
            };
            var changed = typeof(Theme).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check.True(changed.GetValue(theme) is Delegate);
            manager.Theme = new FluentTheme();
            Check.True(changed.GetValue(theme) == null);
            var current = manager.Theme;
            manager.Dispose();
            Check.True(changed.GetValue(current) == null);
        });
        tests.Test("binding: ordinary Content and ToolTip bindings remain live after target writes", () =>
        {
            var source = new XamlWorkspaceItem
            {
                Text = "Initial",
                Title = "Tooltip"
            };
            var document = new LayoutDocument();
            BindingOperations.SetBinding(document, LayoutContent.ContentProperty, new Binding { Source = source, Path = new PropertyPath(nameof(source.Text)), Mode = BindingMode.TwoWay });
            BindingOperations.SetBinding(document, LayoutContent.ToolTipProperty, new Binding { Source = source, Path = new PropertyPath(nameof(source.Title)), Mode = BindingMode.OneWay });
            Check.Equal("Initial", document.Content);
            source.Text = "Source edit";
            Check.Equal("Source edit", document.Content);
            document.Content = "Target edit";
            Check.Equal("Target edit", source.Text);
            source.Text = "Still bound";
            Check.Equal("Still bound", document.Content);
            source.Title = "New tooltip";
            Check.Equal("New tooltip", document.ToolTip);
        });
        tests.Test("compiled resources: chrome templates are constructible without runtime XAML parsing", () =>
        {
            var resources = new DockChromeResources();
            Check.True(resources["UnoDock.ChromeButtonTemplate"] is ControlTemplate);
            Check.True(resources["UnoDock.ChromeThumbTemplate"] is ControlTemplate);
        });
        foreach (var kind in new[]
        {
            "declarative",
            "mvvm",
            "templates"
        }

        )
            foreach (var theme in new[]
            {
                ElementTheme.Light,
                ElementTheme.Dark
            }

            )
                tests.Test($"compiled XAML screenshot: {kind}/{theme}", async () =>
                {
                    UserControl page = kind switch
                    {
                        "declarative" => new XamlDeclarativeWorkspace(),
                        "mvvm" => new XamlMvvmWorkspace(),
                        _ => new XamlTemplateWorkspace()
                    };
                    var manager = page switch
                    {
                        XamlDeclarativeWorkspace p => p.Manager,
                        XamlMvvmWorkspace p => p.Manager,
                        XamlTemplateWorkspace p => p.Manager,
                        _ => throw new InvalidOperationException()
                    };
                    using var host = new Host<UserControl>(page, manager);
                    page.RequestedTheme = theme;
                    await host.Show();
                    var document = manager.Layout.Descendents().OfType<LayoutDocument>().First();
                    document.IsActive = true;
                    manager.Refresh();
                    await Wait(() => manager.FindVisualChildren<TextBox>().Any());
                    await Task.Delay(70);
                    await VisualCapture.Save(page, Path.Combine(output, $"xaml-{kind}-{theme.ToString().ToLowerInvariant()}.png"));
                });
        return await tests.Run(output, "xaml-workspaces");
    }

    private static Host<XamlDeclarativeWorkspace> Declarative()
    {
        var page = new XamlDeclarativeWorkspace();
        return new(page, page.Manager);
    }

    private static Host<XamlMvvmWorkspace> Mvvm()
    {
        var page = new XamlMvvmWorkspace();
        return new(page, page.Manager);
    }

    private static Host<XamlTemplateWorkspace> Templates()
    {
        var page = new XamlTemplateWorkspace();
        return new(page, page.Manager);
    }

    private static double Metric(DockingManager manager, string name) => PaletteProperty<double>(manager, name);
    private static T PaletteProperty<T>(DockingManager manager, string name)
    {
        var palette = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
        return (T)palette.GetType().GetProperty(name)!.GetValue(palette)!;
    }

    private static void AssertOwnership(ILayoutElement node)
    {
        var seen = new HashSet<ILayoutElement>(ReferenceEqualityComparer.Instance);
        Visit(node);
        void Visit(ILayoutElement element)
        {
            Check.True(seen.Add(element));
            if (element is not ILayoutContainer container)
                return;
            foreach (var child in container.Children)
            {
                Check.Same(container, child.Parent);
                Visit(child);
            }
        }
    }

    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 150 && !ready(); i++)
            await Task.Delay(20);
        Check.True(ready(), "The compiled XAML workspace did not settle.");
    }

    private sealed class Host<T> : IDisposable where T : UserControl
    {
        private readonly Window _window;
        internal Host(T page, DockingManager manager)
        {
            Page = page;
            Manager = manager;
            _window = new Window
            {
                Content = page,
                Title = "UnoDock compiled XAML acceptance"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 1100,
                Height = 780
            });
            _window.Activate();
        }

        internal T Page
        {
            get;
        }
        internal DockingManager Manager
        {
            get;
        }

        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualHeight > 100);
            Manager.Refresh();
            await Wait(() => Manager.FindVisualChildren<LayoutDocumentPaneControl>().Any());
            await Task.Delay(30);
        }

        public void Dispose()
        {
            try
            {
                ((IDisposable)Page).Dispose();
            }
            finally
            {
                _window.Content = null;
                _window.Close();
            }
        }
    }
}
