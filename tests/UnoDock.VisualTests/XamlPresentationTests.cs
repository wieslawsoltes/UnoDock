using System.Reflection;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.Themes;
using UnoDock.VisualValidation;

namespace UnoDock.Testing;

internal static class XamlPresentationTests
{
    internal static void Add(TestRunner tests, string output)
    {
        foreach (var (density, title, tab, tool, rail, button) in new[]
        {
            (DockChromeDensity.Default, 18d, 20d, 23d, 26d, 16d),
            (DockChromeDensity.Compact, 18d, 20d, 23d, 26d, 16d),
            (DockChromeDensity.Comfortable, 26d, 28d, 26d, 30d, 20d),
            (DockChromeDensity.Spacious, 32d, 36d, 32d, 38d, 28d)
        }

        )
        {
            tests.Test("XAML density: profile defaults " + density, () =>
            {
                using var manager = new DockingManager
                {
                    ChromeDensity = density,
                    Theme = new FluentTheme()
                };
                Check.Equal(title, Metric(manager, "TitleHeight"));
                Check.Equal(tab, Metric(manager, "TabHeight"));
                Check.Equal(tool, Metric(manager, "ToolTabHeight"));
                Check.Equal(rail, Metric(manager, "RailThickness"));
                Check.Equal(button, Metric(manager, "ChromeButtonSize"));
            });
        }

        tests.Test("XAML density: invalid DP and CLR assignments preserve the previous value", () =>
        {
            using var manager = new DockingManager
            {
                ChromeDensity = DockChromeDensity.Compact
            };
            Check.Throws<ArgumentOutOfRangeException>(() => manager.ChromeDensity = (DockChromeDensity)99);
            Check.Equal(DockChromeDensity.Compact, manager.ChromeDensity);
            Check.Throws<ArgumentOutOfRangeException>(() => manager.SetValue(DockingManager.ChromeDensityProperty, (DockChromeDensity)99));
            Check.Equal(DockChromeDensity.Compact, manager.ChromeDensity);
            manager.ChromeDensity = DockChromeDensity.Spacious;
            Check.Equal(36d, Metric(manager, "TabHeight"));
        });
        tests.Test("XAML density: nearest generic resource precedes an ancestor profile", async () =>
        {
            var view = new XamlWorkbenchView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                manager.ChromeDensity = DockChromeDensity.Spacious;
                manager.Resources["UnoDock.TitleHeight"] = 29d;
                manager.Refresh();
                Check.Equal(29d, Metric(manager, "TitleHeight"));
                Check.Equal(36d, Metric(manager, "TabHeight"));
                manager.Resources["UnoDock.Spacious.TitleHeight"] = 33d;
                Check.Equal(33d, Metric(manager, "TitleHeight"));
                manager.Resources.Remove("UnoDock.Spacious.TitleHeight");
                manager.Resources.Remove("UnoDock.TitleHeight");
                Check.Equal(32d, Metric(manager, "TitleHeight"));
                await Task.CompletedTask;
            });
        });
        tests.Test("XAML density: dictionary direct override precedes merged profile", () =>
        {
            using var manager = new DockingManager
            {
                Theme = new FluentTheme(),
                ChromeDensity = DockChromeDensity.Spacious
            };
            manager.Resources["UnoDock.TabHeight"] = 31d;
            manager.Resources.MergedDictionaries.Add(new ResourceDictionary { ["UnoDock.Spacious.TabHeight"] = 50d });
            Check.Equal(31d, Metric(manager, "TabHeight"));
        });
        tests.Test("XAML density: font floors and button extents cannot clip compact captions", () =>
        {
            using var manager = new DockingManager
            {
                Theme = new FluentTheme(),
                ChromeDensity = DockChromeDensity.Spacious
            };
            manager.Resources["UnoDock.TitleHeight"] = 18d;
            manager.Resources["UnoDock.ChromeButtonSize"] = 40d;
            Check.Equal(16d, Metric(manager, "ChromeButtonSize"));
            manager.Resources["UnoDock.FontSize"] = 24d;
            Check.True(Metric(manager, "TitleHeight") >= 36d);
            Check.True(Metric(manager, "TabHeight") >= 40d);
            Check.True(Metric(manager, "ChromeButtonSize") <= Metric(manager, "TitleHeight") - 2);
        });
        tests.Test("XAML density: compiled element-name picker updates retained manager chrome", async () =>
        {
            var view = new XamlWorkbenchView();
            await Host(view, async () =>
            {
                var picker = view.FindVisualChildren<XamlDensityPicker>().Single();
                var choices = picker.FindVisualChildren<ComboBox>().Single();
                var binding = view.Manager.GetBindingExpression(DockingManager.ChromeDensityProperty)?.ParentBinding;
                Check.True(binding != null);
                choices.SelectedIndex = (int)DockChromeDensity.Spacious;
                await Wait(() => view.Manager.ChromeDensity == DockChromeDensity.Spacious);
                Check.Equal(36d, Metric(view.Manager, "TabHeight"));
                picker.Density = DockChromeDensity.Compact;
                await Wait(() => choices.SelectedIndex == (int)DockChromeDensity.Compact && view.Manager.ChromeDensity == DockChromeDensity.Compact);
                Check.Same(binding, view.Manager.GetBindingExpression(DockingManager.ChromeDensityProperty)?.ParentBinding);
            });
        });
        foreach (var native in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("XAML density: " + (native ? "native" : "surface") + " changes preserve floating windows and editor state", async () =>
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
                    view.Document.Text = "Density must not replace this draft.";
                    document.Float();
                    manager.Refresh();
                    await Wait(() => manager.FloatingWindows.Any(w => w.IsLoaded));
                    var floating = manager.FloatingWindows.Single();
                    var window = floating.NativeWindow;
                    foreach (var density in Enum.GetValues<DockChromeDensity>())
                    {
                        manager.ChromeDensity = density;
                        manager.Refresh();
                        await Task.Delay(40);
                        Check.Same(floating, manager.FloatingWindows.Single());
                        Check.Same(window, floating.NativeWindow);
                        Check.Same(presenter, item.View);
                        Check.Same(editor, presenter.FindVisualChildren<XamlDocumentEditor>().Single());
                        Check.Equal("Density must not replace this draft.", view.Document.Text);
                        var caption = (Grid)typeof(LayoutFloatingWindowControl).GetField("_title", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(floating)!;
                        Check.True(caption.ActualHeight >= Metric(manager, "TitleHeight") - .1);
                    }

                    document.Dock();
                    manager.Refresh();
                    await Wait(() => editor.IsLoaded && !document.IsFloating);
                    Check.Same(editor, presenter.FindVisualChildren<XamlDocumentEditor>().Single());
                });
            });
        }

        tests.Test("XAML selection: active and inactive indicators follow model state without capturing input", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var manager = view.Manager;
                var documents = manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
                documents[0].IsActive = true;
                manager.Refresh();
                var tab = manager.FindVisualChildren<LayoutDocumentTabItem>().Single(t => ReferenceEquals(t.Model, documents[0]));
                var indicator = tab.FindVisualChildren<Border>().Single(b => b.Name == "PART_SelectedTabIndicator");
                Check.Equal(Visibility.Visible, indicator.Visibility);
                Check.False(indicator.IsHitTestVisible);
                Check.Same(PaletteProperty<Brush>(manager, "Accent"), indicator.Background);
                manager.Layout.ActiveContent = null;
                manager.Refresh();
                Check.Equal(Visibility.Visible, indicator.Visibility);
                Check.Same(PaletteProperty<Brush>(manager, "Border"), indicator.Background);
                documents[1].IsActive = true;
                manager.Refresh();
                Check.Equal(Visibility.Collapsed, indicator.Visibility);
                manager.Resources["UnoDock.ActiveTabIndicatorThickness"] = 0d;
                manager.Refresh();
                Check.True(manager.FindVisualChildren<Border>().Where(b => b.Name == "PART_SelectedTabIndicator").All(b => b.Visibility == Visibility.Collapsed));
                await Task.CompletedTask;
            });
        });
        foreach (var property in new[]
        {
            "CanMove",
            "CanAutoHide",
            "CanDockAsTabbedDocument"
        }

        )
        {
            tests.Test("XAML policies: literal item style updates real policy and defaults for " + property, () =>
            {
                using var manager = new DockingManager();
                var pane = new LayoutDocumentPane();
                manager.Layout.RootPanel.Children.Add(pane);
                LayoutContent content = property == "CanMove" ? new LayoutDocument() : new LayoutAnchorable();
                if (content is LayoutDocument doc)
                    pane.Children.Add(doc);
                else
                    manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane((LayoutAnchorable)content));
                var item = manager.GetLayoutItemFromModel(content);
                var dp = property switch
                {
                    "CanMove" => LayoutDocumentItem.CanMoveProperty,
                    "CanAutoHide" => LayoutAnchorableItem.CanAutoHideProperty,
                    _ => LayoutAnchorableItem.CanDockAsTabbedDocumentProperty
                };
                var style = new Style(typeof(LayoutItem));
                style.Setters.Add(new Setter { Property = dp, Value = false });
                manager.LayoutItemContainerStyle = style;
                Check.Equal(false, item.GetValue(dp));
                Check.Equal(false, content.GetType().GetProperty(property)!.GetValue(content));
                var command = property switch
                {
                    "CanMove" => item.FloatCommand,
                    "CanAutoHide" => ((LayoutAnchorableItem)item).AutoHideCommand,
                    _ => item.DockAsDocumentCommand
                };
                Check.True(command != null);
                Check.False(command!.CanExecute(null));
            });
        }

        tests.Test("XAML policies: compiled AnchorablesSource binds metadata and each tool policy", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var state = view.Tools[0];
                await Wait(() => view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Any(a => ReferenceEquals(a.Content, state)));
                var model = view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => ReferenceEquals(a.Content, state));
                var item = (LayoutAnchorableItem)view.Manager.GetLayoutItemFromModel(model);
                var autoHide = item.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty)?.ParentBinding;
                Check.True(autoHide != null);
                state.Title = "Source-bound inspector";
                state.CanHide = state.CanAutoHide = state.CanDockAsTabbedDocument = false;
                await Wait(() => model.Title == state.Title && !model.CanHide && !model.CanAutoHide && !model.CanDockAsTabbedDocument);
                Check.False(item.HideCommand!.CanExecute(null));
                Check.False(item.AutoHideCommand!.CanExecute(null));
                Check.False(item.DockAsDocumentCommand!.CanExecute(null));
                model.CanAutoHide = true;
                await Wait(() => state.CanAutoHide && item.CanAutoHide);
                Check.Same(autoHide, item.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty)?.ParentBinding);
                state.CanHide = true;
                model.Hide();
                view.Manager.Refresh();
                Check.True(model.IsHidden);
                state.Title = "Renamed while hidden";
                await Wait(() => model.Title == state.Title);
                model.Show();
                view.Manager.Refresh();
                Check.False(model.IsHidden);
                Check.Same(state, model.Content);
            });
        });
        tests.Test("XAML policies: tools have independent expressions and source removal releases the adapter", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var first = view.Tools[0];
                var second = new XamlTool
                {
                    ContentId = "second-tool",
                    Title = "Second tool"
                };
                view.Tools.Add(second);
                await Wait(() => view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 2);
                LayoutAnchorable Model(XamlTool state) => view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => ReferenceEquals(a.Content, state));
                var a = (LayoutAnchorableItem)view.Manager.GetLayoutItemFromModel(Model(first));
                var modelB = Model(second);
                var b = (LayoutAnchorableItem)view.Manager.GetLayoutItemFromModel(modelB);
                Check.False(ReferenceEquals(a.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty)?.ParentBinding, b.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty)?.ParentBinding));
                first.CanAutoHide = false;
                await Wait(() => !a.CanAutoHide);
                Check.True(b.CanAutoHide);
                view.Tools.Remove(second);
                await Wait(() => modelB.Root == null && b.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty) == null);
                var old = modelB.Title;
                second.Title = "Must not update removed model";
                Check.Equal(old, modelB.Title);
                Check.True(b.GetBindingExpression(LayoutAnchorableItem.CanAutoHideProperty) == null);
            });
        });
        tests.Test("XAML policies: compiled document movement binding disables native float command", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                var source = view.Documents[0];
                var document = view.Manager.Layout.Descendents().OfType<LayoutDocument>().Single(d => ReferenceEquals(d.Content, source));
                var item = (LayoutDocumentItem)view.Manager.GetLayoutItemFromModel(document);
                source.CanMove = false;
                await Wait(() => !document.CanMove && !item.CanMove);
                Check.False(item.FloatCommand!.CanExecute(null));
                source.CanMove = true;
                await Wait(() => document.CanMove && item.FloatCommand!.CanExecute(null));
            });
        });
        foreach (var invalid in new[]
        {
            "source-name",
            "source-relative",
            "name-relative",
            "mode",
            "trigger",
            "empty-target",
            "wrong-kind"
        }

        )
        {
            tests.Test("XAML binding preflight: invalid " + invalid + " preserves an existing expression", () =>
            {
                var state = new XamlDocument();
                var document = new LayoutDocument
                {
                    Content = state
                };
                using var manager = new DockingManager
                {
                    Layout = new()
                    {
                        RootPanel = new(new LayoutDocumentPane(document))
                    }
                };
                var item = manager.GetLayoutItemFromModel(document);
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title" } });
                var previous = item.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding;
                var definition = new LayoutItemBinding
                {
                    Property = "Title",
                    Path = "Title"
                };
                switch (invalid)
                {
                    case "source-name":
                        definition.Source = state;
                        definition.ElementName = "source";
                        break;
                    case "source-relative":
                        definition.Source = state;
                        definition.RelativeSource = new()
                        {
                            Mode = RelativeSourceMode.TemplatedParent
                        };
                        break;
                    case "name-relative":
                        definition.ElementName = "source";
                        definition.RelativeSource = new()
                        {
                            Mode = RelativeSourceMode.TemplatedParent
                        };
                        break;
                    case "mode":
                        definition.Mode = (BindingMode)99;
                        break;
                    case "trigger":
                        definition.UpdateSourceTrigger = (UpdateSourceTrigger)99;
                        break;
                    case "empty-target":
                        definition.Property = " ";
                        break;
                    default:
                        definition.Property = "CanAutoHide";
                        break;
                }

                Check.Throws<ArgumentException>(() => LayoutItemBindings.SetBindings(item, new() { definition }));
                Check.Same(previous, item.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding);
                state.Title = "The previous source remains connected";
                Check.Equal(state.Title, document.Title);
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title" } });
                state.Title = "Recovery";
                Check.Equal("Recovery", document.Title);
            });
        }

        tests.Test("XAML sample commands: native Invoke floats and restores the payload-selected document", async () =>
        {
            var view = new XamlWorkbenchView();
            view.Manager.FloatingWindowMode = FloatingWindowMode.InSurface;
            await Host(view, async () =>
            {
                var manager = view.Manager;
                var document = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                Check.Same(view.Document, document.Content);
                Check.Equal(view.Document.ContentId, document.ContentId);
                Check.Same(document, manager.Layout.ActiveContent);
                var presenter = manager.GetLayoutItemFromModel(document).View;
                Invoke(view, "XamlFloatDocument");
                await Wait(() => document.IsFloating);
                Invoke(view, "XamlFloatDocument");
                await Wait(() => !document.IsFloating);
                Check.Same(presenter, manager.GetLayoutItemFromModel(document).View);
                Invoke(view, "XamlSaveRestoreLayout");
                Invoke(view, "XamlFloatDocument");
                await Wait(() => document.IsFloating);
                Invoke(view, "XamlSaveRestoreLayout");
                await Wait(() => manager.Layout.Descendents().OfType<LayoutDocument>().Any(d => ReferenceEquals(d.Content, view.Document) && !d.IsFloating));
                Invoke(view, "XamlFloatDocument");
                await Wait(() => manager.Layout.Descendents().OfType<LayoutDocument>().Any(d => ReferenceEquals(d.Content, view.Document) && d.IsFloating));
            });
        });
        tests.Test("XAML sample commands: native Invoke adds tools and restores hidden source items", async () =>
        {
            var view = new XamlMvvmView();
            await Host(view, async () =>
            {
                Invoke(view, "XamlAddTool");
                await Wait(() => view.Tools.Count == 2 && view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 2);
                var tool = view.Manager.Layout.Descendents().OfType<LayoutAnchorable>().First();
                tool.Hide();
                view.Manager.Refresh();
                Check.True(tool.IsHidden);
                Invoke(view, "XamlShowTools");
                await Wait(() => !tool.IsHidden);
                Check.True(view.Tools.Any(source => ReferenceEquals(source, tool.Content)));
            });
        });
        foreach (var dark in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("XAML visual capture: source-backed policies and spacious chrome " + (dark ? "dark" : "light"), async () =>
            {
                var view = new XamlMvvmView
                {
                    RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light
                };
                await Host(view, async () =>
                {
                    view.FindVisualChildren<XamlDensityPicker>().Single().Density = DockChromeDensity.Spacious;
                    await Wait(() => view.Manager.ChromeDensity == DockChromeDensity.Spacious);
                    view.Manager.Refresh();
                    view.UpdateLayout();
                    await Task.Delay(80);
                    var file = Path.Combine(output, "visuals", "xaml-policies-" + (dark ? "dark" : "light") + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    await VisualCapture.Save(view, file);
                });
            });
        }

        tests.Test("XAML visual capture: narrow workbench keeps commands horizontally reachable", async () =>
        {
            var view = new XamlWorkbenchView
            {
                Width = 600,
                Height = 650
            };
            await Host(view, async () =>
            {
                await Wait(() => view.ActualWidth == 600);
                var scroller = view.FindVisualChildren<ScrollViewer>().First(s => s.Content is StackPanel p && p.Children.OfType<XamlDensityPicker>().Any());
                Check.True(scroller.ScrollableWidth > 0);
                Check.True(scroller.ChangeView(scroller.ScrollableWidth, null, null, true));
                await Wait(() => scroller.HorizontalOffset > 0);
                var file = Path.Combine(output, "visuals", "xaml-workbench-narrow.png");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await VisualCapture.Save(view, file);
            });
        });
    }

    private static void Invoke(UserControl view, string id)
    {
        var button = view.FindVisualChildren<Button>().Single(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(b) == id);
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(button);
        var provider = peer?.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke) as Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider;
        Check.True(provider != null, "Sample command has no native Invoke provider.");
        provider!.Invoke();
    }

    private static T PaletteProperty<T>(DockingManager manager, string name)
    {
        var palette = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [manager])!;
        return (T)palette.GetType().GetProperty(name)!.GetValue(palette)!;
    }

    private static double Metric(DockingManager manager, string name) => PaletteProperty<double>(manager, name);
    private static async Task Host(UserControl view, Func<Task> action)
    {
        var window = new Window
        {
            Content = view,
            Title = "UnoDock XAML presentation acceptance"
        };
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        try
        {
            window.AppWindow.Resize(new()
            {
                Width = 1200,
                Height = 800
            });
            window.Activate();
            await Wait(() => view.IsLoaded && view.ActualWidth > 0);
            view.UpdateLayout();
            await Task.Delay(60);
            await action();
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
        Check.True(predicate(), "XAML presentation state did not settle.");
    }
}
