using System.Reflection;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.Themes;
using UnoDock.VisualValidation;

namespace UnoDock.Testing;

internal static class XamlWorkbenchTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        XamlStructureTests.Add(tests);
        foreach (var type in new[]
        {
            typeof(LayoutDocument),
            typeof(LayoutAnchorable),
            typeof(LayoutDocumentPane),
            typeof(LayoutAnchorablePane),
            typeof(LayoutPanel),
            typeof(LayoutDocumentPaneGroup),
            typeof(LayoutAnchorablePaneGroup)
        }

        )
        {
            tests.Test("XAML endpoints: native defaults and CLR values agree for " + type.Name, () =>
            {
                var model = (LayoutElement)Activator.CreateInstance(type)!;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).Where(f => f.FieldType == typeof(DependencyProperty)))
                {
                    var property = type.GetProperty(field.Name[..^8]);
                    if (property == null)
                        continue;
                    Check.Equal(property.GetValue(model), model.GetValue((DependencyProperty)field.GetValue(null)!));
                }
            });
        }

        tests.Test("XAML endpoints: dimensions reject invalid direct DP values without stale state", () =>
        {
            var pane = new LayoutDocumentPane
            {
                DockMinWidth = 80
            };
            Check.Throws<ArgumentOutOfRangeException>(() => pane.SetValue(LayoutDocumentPane.DockMinWidthProperty, -1d));
            Check.Equal(80d, pane.DockMinWidth);
            Check.Equal(80d, pane.GetValue(LayoutDocumentPane.DockMinWidthProperty));
            Check.Throws<ArgumentOutOfRangeException>(() => pane.SetValue(LayoutDocumentPane.FloatingLeftProperty, double.NaN));
            Check.Equal(0d, pane.FloatingLeft);
            pane.ClearValue(LayoutDocumentPane.DockMinWidthProperty);
            Check.Equal(25d, pane.DockMinWidth);
        });
        tests.Test("XAML endpoints: Binding is retained across model-originated target writes", async () =>
        {
            var state = new XamlDocument();
            var model = new LayoutDocument();
            var binding = new Binding
            {
                Source = state,
                Path = new(nameof(state.CanClose)),
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(model, LayoutContent.CanCloseProperty, binding);
            state.CanClose = false;
            await Wait(() => !model.CanClose);
            model.CanClose = true;
            await Wait(() => state.CanClose);
            state.CanClose = false;
            await Wait(() => !model.CanClose);
            Check.False((bool)model.GetValue(LayoutContent.CanCloseProperty));
        });
        tests.Test("XAML endpoints: activation writes use the existing reentrant coordinator", () =>
        {
            var a = new LayoutDocument();
            var b = new LayoutDocument();
            var c = new LayoutDocument();
            var pane = new LayoutDocumentPane(a);
            pane.Children.Add(b);
            pane.Children.Add(c);
            var root = new LayoutRoot
            {
                RootPanel = new(pane)
            };
            a.IsActive = true;
            a.IsActiveChanged += (_, _) =>
            {
                if (!a.IsActive)
                    c.SetValue(LayoutContent.IsActiveProperty, true);
            };
            b.SetValue(LayoutContent.IsActiveProperty, true);
            Check.Same(c, root.ActiveContent);
            Check.False(a.IsActive);
            Check.False(b.IsActive);
            Check.True(c.IsActive);
            Check.Equal(false, b.GetValue(LayoutContent.IsActiveProperty));
            Check.Equal(true, c.GetValue(LayoutContent.IsActiveProperty));
        });
        tests.Test("XAML theme: requested mode rejects invalid DP writes and preserves explicit overrides", () =>
        {
            var theme = new FluentTheme(ElementTheme.Light);
            var brush = new SolidColorBrush(Microsoft.UI.Colors.Crimson);
            theme.ThemeResourceDictionary["UnoDock.AccentBrush"] = brush;
            theme.RequestedTheme = ElementTheme.Dark;
            Check.Same(brush, theme.ThemeResourceDictionary["UnoDock.AccentBrush"]);
            Check.Throws<ArgumentOutOfRangeException>(() => theme.SetValue(FluentTheme.RequestedThemeProperty, (ElementTheme)93));
            Check.Equal(ElementTheme.Dark, theme.RequestedTheme);
            theme.RequestedTheme = ElementTheme.Default;
            Check.Same(brush, theme.ThemeResourceDictionary["UnoDock.AccentBrush"]);
        });
        tests.Test("XAML sample: compiled layout and x:Bind retain live model endpoints", async () =>
        {
            var view = new XamlWorkbenchView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                Check.Same(view.Document, document.Content);
                Check.Equal(2, manager.Layout.Descendents().OfType<LayoutAnchorable>().Count());
                view.Document.Title = "Renamed.xaml";
                view.Document.CanClose = false;
                await Wait(() => document.Title == "Renamed.xaml" && !document.CanClose);
                Check.Equal("Renamed.xaml", manager.GetLayoutItemFromModel(document).Title);
                Check.False(manager.GetLayoutItemFromModel(document).CanClose);
                var pane = (LayoutDocumentPane)document.Parent!;
                pane.SetValue(LayoutDocumentPane.DockMinWidthProperty, 130d);
                Check.Equal(130d, pane.DockMinWidth);
            });
        });
        foreach (var native in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("XAML sample: " + (native ? "native" : "surface") + " float/dock and light/dark preserve realized editors", async () =>
            {
                var view = new XamlWorkbenchView();
                view.Manager.FloatingWindowMode = native ? FloatingWindowMode.Native : FloatingWindowMode.InSurface;
                await Host(view, async () =>
                {
                    var manager = view.Manager;
                    var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                    var item = manager.GetLayoutItemFromModel(document);
                    var presenter = item.View;
                    await Wait(() => presenter.FindVisualChildren<XamlDocumentEditor>().Any());
                    var editor = presenter.FindVisualChildren<XamlDocumentEditor>().Single();
                    var box = editor.FindVisualChildren<TextBox>().Single(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(b) == "XamlEditorText");
                    box.Text = "Unsaved draft";
                    await Wait(() => view.Document.Text == "Unsaved draft");
                    document.Float();
                    manager.Refresh();
                    await Wait(() => manager.FloatingWindows.Any(w => w.IsLoaded));
                    var floating = manager.FloatingWindows.Single();
                    var window = floating.NativeWindow;
                    foreach (var mode in new[]
                    {
                        ElementTheme.Dark,
                        ElementTheme.Light
                    }

                    )
                    {
                        view.RequestedTheme = mode;
                        await Wait(() => manager.ActualTheme == mode);
                        manager.Refresh();
                        Check.Same(window, floating.NativeWindow);
                        Check.Same(presenter, item.View);
                        Check.Same(editor, presenter.FindVisualChildren<XamlDocumentEditor>().Single());
                    }

                    document.Dock();
                    manager.Refresh();
                    await Wait(() => !document.IsFloating && editor.IsLoaded);
                    Check.Same(editor, presenter.FindVisualChildren<XamlDocumentEditor>().Single());
                    Check.Equal("Unsaved draft", view.Document.Text);
                });
            });
        }

        tests.Test("XAML sample: live theme mutation and scoped metrics update without replacing owner", async () =>
        {
            var view = new XamlWorkbenchView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                var root = manager.Layout;
                Check.Equal(26d, Metric(manager, "TitleHeight"));
                Check.Equal(28d, Metric(manager, "TabHeight"));
                Check.Equal(3d, Metric(manager, "ButtonCornerRadius"));
                var theme = (FluentTheme)manager.Theme!;
                theme.RequestedTheme = ElementTheme.Dark;
                await Task.Delay(80);
                manager.Refresh();
                Check.Same(root, manager.Layout);
                view.Resources["UnoDock.TitleHeight"] = 32d;
                manager.Refresh();
                Check.Equal(32d, Metric(manager, "TitleHeight"));
            });
        });
        tests.Test("XAML sample: TemplateBinding and retemplate release old host but retain content", async () =>
        {
            var view = new XamlWorkbenchView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                var presenter = manager.GetLayoutItemFromModel(document).View;
                var root = manager.Layout;
                var oldHost = manager.FindVisualChildren<ContentPresenter>().Single(p => p.Name == "PART_LayoutHost");
                manager.Template = (ControlTemplate)XamlReader.Load("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                      <Border x:Name="CustomBorder" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                        <Grid x:Name="PART_AutoHideArea"><ContentPresenter x:Name="PART_LayoutHost" HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch"/></Grid>
                      </Border>
                    </ControlTemplate>
                    """);
                manager.Padding = new(7);
                manager.BorderThickness = new(2);
                manager.ApplyTemplate();
                manager.UpdateLayout();
                manager.Refresh();
                await Task.Delay(60);
                Check.True(oldHost.Content == null);
                Check.Same(root, manager.Layout);
                Check.Same(presenter, manager.GetLayoutItemFromModel(document).View);
                var border = manager.FindVisualChildren<Border>().Single(b => b.Name == "CustomBorder");
                Check.Equal(new Thickness(7), border.Padding);
                Check.Equal(new Thickness(2), border.BorderThickness);
            });
        });
        tests.Test("XAML MVVM: conventional item style binds source title, identity and close policy", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                await Wait(() => manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2);
                var state = view.Documents[0];
                var model = manager.Layout.Descendents().OfType<LayoutDocument>().Single(d => ReferenceEquals(d.Content, state));
                var item = manager.GetLayoutItemFromModel(model);
                state.Title = "MVVM rename";
                state.CanClose = false;
                await Wait(() => model.Title == state.Title && !model.CanClose);
                Check.Equal(state.ContentId, model.ContentId);
                state.Title = "Second rename";
                await Wait(() => model.Title == state.Title);
                view.Documents.Add(new()
                {
                    ContentId = "third",
                    Title = "Third.cs"
                });
                await Wait(() => manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 3);
                view.Documents.RemoveAt(2);
                await Wait(() => manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2);
            });
        });
        tests.Test("XAML reader: trusted runtime markup creates the native layout hierarchy", async () =>
        {
            var dock = (DockingManager)XamlReader.Load("""
                <dock:DockingManager xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:dock="using:UnoDock" xmlns:layout="using:UnoDock.Layout" xmlns:themes="using:UnoDock.Themes">
                  <dock:DockingManager.Theme><themes:FluentTheme RequestedTheme="Dark"/></dock:DockingManager.Theme>
                  <layout:LayoutRoot><layout:LayoutPanel Orientation="Vertical">
                    <layout:LayoutDocumentPane DockMinWidth="130"><layout:LayoutDocument Title="Runtime.xaml" ContentId="runtime">
                      <TextBox Text="Runtime editor"/>
                    </layout:LayoutDocument></layout:LayoutDocumentPane>
                  </layout:LayoutPanel></layout:LayoutRoot>
                </dock:DockingManager>
                """);
            var window = new Window
            {
                Content = dock
            };
            try
            {
                window.Activate();
                await Wait(() => dock.IsLoaded);
                dock.Refresh();
                Check.Equal(Orientation.Vertical, dock.Layout.RootPanel.Orientation);
                var document = dock.Layout.Descendents().OfType<LayoutDocument>().Single();
                Check.Equal("Runtime.xaml", document.Title);
                Check.Equal("Runtime editor", ((TextBox)document.Content!).Text);
                Check.Equal(130d, ((LayoutDocumentPane)document.Parent!).DockMinWidth);
            }
            finally
            {
                dock.Dispose();
                window.Content = null;
                window.Close();
            }
        });
        tests.Test("XAML bindings: each item owns a distinct expression and replacement releases old definitions", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var models = view.Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
                var a = view.Manager.GetLayoutItemFromModel(models[0]);
                var b = view.Manager.GetLayoutItemFromModel(models[1]);
                Check.False(ReferenceEquals(a.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding, b.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding));
                var external = new XamlDocument
                {
                    Title = "External binding"
                };
                var replacement = new LayoutItemBindingCollection
                {
                    new()
                    {
                        Property = "Title",
                        Path = "Title",
                        Source = external
                    }
                };
                LayoutItemBindings.SetBindings(a, replacement);
                await Wait(() => models[0].Title == "External binding");
                view.Documents[0].Title = "Old source changed";
                Check.Equal("External binding", models[0].Title);
                LayoutItemBindings.SetBindings(a, null);
                external.Title = "Retired binding";
                Check.Equal("External binding", models[0].Title);
                a.Title = "Retained default";
                Check.Equal("Retained default", models[0].Title);
            });
        });
        tests.Test("XAML bindings: local binding wins and duplicate targets reject before replacement", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var model = view.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
                var item = view.Manager.GetLayoutItemFromModel(model);
                var local = new XamlDocument
                {
                    Title = "Local"
                };
                var expression = new Binding
                {
                    Source = local,
                    Path = new("Title")
                };
                item.SetBinding(LayoutItem.TitleProperty, expression);
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title" } });
                local.Title = "Still local";
                await Wait(() => model.Title == "Still local");
                Check.Same(expression, item.GetBindingExpression(LayoutItem.TitleProperty)!.ParentBinding);
                Check.Throws<ArgumentException>(() => LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title" }, new() { Property = "Title", Path = "Text" } }));
                Check.Same(expression, item.GetBindingExpression(LayoutItem.TitleProperty)!.ParentBinding);
            });
        });
        tests.Test("XAML bindings: content replacement updates inherited DataContext without retaining the old source", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var model = view.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
                var old = (XamlDocument)model.Content!;
                var next = new XamlDocument
                {
                    ContentId = old.ContentId,
                    Title = "New content",
                    CanClose = false
                };
                model.Content = next;
                await Wait(() => model.Title == "New content" && !model.CanClose);
                old.Title = "Stale content";
                Check.Equal("New content", model.Title);
                next.Title = "Updated replacement";
                await Wait(() => model.Title == next.Title);
            });
        });
        tests.Test("XAML style: literal capability setters reach the model and default commands", () =>
        {
            using var manager = new DockingManager();
            var document = new LayoutDocument
            {
                Title = "Original"
            };
            manager.Layout.RootPanel = new(new LayoutDocumentPane(document));
            manager.LayoutItemContainerStyle = new Style(typeof(LayoutItem))
            {
                Setters =
                {
                    new Setter(LayoutItem.CanCloseProperty, false),
                    new Setter(LayoutItem.TitleProperty, "Styled title")
                }
            };
            var item = manager.GetLayoutItemFromModel(document);
            Check.False(document.CanClose);
            Check.Equal("Styled title", document.Title);
            Check.False(item.CloseCommand!.CanExecute(null));
        });
        tests.Test("XAML bindings: reentrant collection replacement cannot complete obsolete bindings", () =>
        {
            using var manager = new DockingManager();
            var state = new XamlDocument
            {
                Title = "First",
                Text = "Obsolete"
            };
            var final = new XamlDocument
            {
                Title = "Final"
            };
            var document = new LayoutDocument
            {
                Title = "Initial",
                Content = state
            };
            manager.Layout.RootPanel = new(new LayoutDocumentPane(document));
            var item = manager.GetLayoutItemFromModel(document);
            var once = false;
            document.PropertyChanged += (_, args) =>
            {
                if (once || args.PropertyName != "Title" || document.Title != "First")
                    return;
                once = true;
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Source = final } });
            };
            LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title" }, new() { Property = "ContentId", Path = "Text" } });
            Check.True(once);
            Check.Equal("Final", document.Title);
            Check.True(document.ContentId != "Obsolete");
            state.Title = "Stale";
            Check.Equal("Final", document.Title);
        });
        foreach (var dark in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("XAML visual capture: " + (dark ? "dark" : "light") + " workbench", async () =>
            {
                var view = new XamlWorkbenchView
                {
                    RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light
                };
                await Host(view, async () =>
                {
                    await Wait(() => view.Manager.ActualWidth > 900);
                    view.UpdateLayout();
                    await Task.Delay(100);
                    var path = System.IO.Path.Combine(output, "visuals", "xaml-workbench-" + (dark ? "dark" : "light") + ".png");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                    await VisualCapture.Save(view, path);
                });
            });
        }

        return await tests.Run(output, "xaml-workbench");
    }

    private static double Metric(DockingManager manager, string name)
    {
        var chrome = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!;
        var palette = chrome.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
        return (double)palette.GetType().GetProperty(name)!.GetValue(palette)!;
    }

    private static async Task Host(UserControl view, Func<Task> body)
    {
        var window = new Window
        {
            Content = view,
            Title = "UnoDock compiled XAML acceptance"
        };
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        window.AppWindow.Resize(new()
        {
            Width = 1200,
            Height = 800
        });
        window.Activate();
        try
        {
            await Wait(() => view.IsLoaded && view.ActualWidth > 0);
            view.UpdateLayout();
            await Task.Delay(80);
            await body();
        }
        finally
        {
            ((IDisposable)view).Dispose();
            window.Content = null;
            window.Close();
        }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
            await Task.Delay(20);
        Check.True(predicate(), "Compiled XAML binding or view did not converge.");
    }
}
