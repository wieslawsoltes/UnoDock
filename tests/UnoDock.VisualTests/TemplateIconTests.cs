using System.Reflection;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Controls;
using UnoDock.Gallery;

namespace UnoDock.Testing;
/// <summary>Acceptance for the manager's presentation templates: content icons
/// (IconContentTemplate/selector) and header, title and open-documents row
/// templates in tabs, the documents list, the navigator, auto-hide rail tabs
/// and floating window captions.</summary>
internal static class TemplateIconTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        tests.Test("tabs: default presentation is the icon followed by the title", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            foreach (var content in new LayoutContent[]
            {
                f.Document,
                f.Second,
                f.Explorer,
                f.Output
            }

            )
            {
                var tab = f.Tab(content);
                Check.True(ShowsIcon(tab, content.IconSource!), content.Title + " tab must show its icon.");
                Check.True(ShowsText(tab, content.Title!), content.Title + " tab must show its title.");
            }

            f.Second.IconSource = null;
            await f.Settle();
            Check.False(f.Tab(f.Second).FindVisualChildren<Image>().Any(Visible));
            Check.True(ShowsText(f.Tab(f.Second), f.Second.Title!));
            await VisualCapture.Save(f.Manager, Path.Combine(output, "templates-icons", "default-icons.png"));
        });
        tests.Test("IconContentTemplate presents the icon value in tabs, rails and captions", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.IconContentTemplate = IconMarker("icon-template");
            await f.Settle();
            foreach (var host in new FrameworkElement[]
            {
                f.Tab(f.Document),
                f.Tab(f.Explorer),
                f.Rail(f.RailTool)
            }

            )
            {
                Check.True(ShowsText(host, "icon-template"), "The icon template must replace the default image.");
                Check.False(host.FindVisualChildren<Image>().Any(Visible));
            }

            // The template's data context is the icon value itself.
            var presenter = f.Tab(f.Document).FindVisualChildren<ContentPresenter>().First(p => p.Name == "PART_HeaderIcon");
            Check.Same(f.Document.IconSource, presenter.Content);
            f.Manager.IconContentTemplate = null;
            await f.Settle();
            Check.True(ShowsIcon(f.Tab(f.Document), f.Document.IconSource!));
            Check.False(ShowsText(f.Tab(f.Document), "icon-template"));
        });
        tests.Test("IconContentTemplateSelector receives each icon and falls back to the default image", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var selector = new Selector(item => ReferenceEquals(item, f.Document.IconSource) ? IconMarker("selected-icon") : null);
            f.Manager.IconContentTemplate = IconMarker("unused-template");
            f.Manager.IconContentTemplateSelector = selector;
            f.Manager.Refresh();
            await f.Settle();
            Check.True(selector.Items.Contains(f.Document.IconSource!));
            Check.True(selector.Items.Contains(f.Second.IconSource!));
            Check.True(ShowsText(f.Tab(f.Document), "selected-icon"));
            // A null selection uses IconContentTemplate, as for every other selector pair.
            Check.True(ShowsText(f.Tab(f.Second), "unused-template"));
            f.Manager.IconContentTemplate = null;
            f.Manager.Refresh();
            await f.Settle();
            Check.True(ShowsIcon(f.Tab(f.Second), f.Second.IconSource!));
        });
        tests.Test("header templates own the whole tab header, including its icon", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.DocumentHeaderTemplate = BoundMarker("document-header");
            f.Manager.IconContentTemplate = IconMarker("icon-template");
            await f.Settle();
            var tab = f.Tab(f.Document);
            Check.True(ShowsText(tab, "document-header"));
            Check.True(ShowsText(tab, f.Document.ContentId!), "The header template data context must be the LayoutContent.");
            Check.False(ShowsText(tab, "icon-template"));
            Check.True(ShowsText(f.Tab(f.Explorer), "icon-template"), "Tool tabs keep the default presentation.");
        });
        tests.Test("auto-hide rail tabs show the icon and AnchorableHeaderTemplate or its selector", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var rail = f.Rail(f.RailTool);
            Check.True(ShowsIcon(rail, f.RailTool.IconSource!));
            Check.True(ShowsText(rail, f.RailTool.Title!));
            Check.Equal(1, rail.FindVisualChildren<Button>().Count());
            f.Manager.AnchorableHeaderTemplate = BoundMarker("rail-header");
            await f.Settle();
            rail = f.Rail(f.RailTool);
            Check.True(ShowsText(rail, "rail-header"));
            Check.True(ShowsText(rail, f.RailTool.ContentId!));
            Check.False(rail.FindVisualChildren<Image>().Any(Visible));
            var selector = new Selector(item => ReferenceEquals(item, f.RailTool) ? BoundMarker("rail-selected") : null);
            f.Manager.AnchorableHeaderTemplateSelector = selector;
            f.Manager.Refresh();
            await f.Settle();
            Check.True(selector.Items.Contains(f.RailTool));
            Check.True(ShowsText(f.Rail(f.RailTool), "rail-selected"));
            f.Manager.AnchorableHeaderTemplateSelector = null;
            f.Manager.AnchorableHeaderTemplate = null;
            f.Manager.Refresh();
            await f.Settle();
            Check.True(ShowsIcon(f.Rail(f.RailTool), f.RailTool.IconSource!));
        });
        tests.Test("documents list: default rows show icon and title with checked and enabled state", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Third.IsEnabled = false;
            await f.Settle();
            var (menu, rows) = await f.OpenDocuments();
            try
            {
                // Listed by title, like the pane's ChildrenSorted.
                Check.Equal("App.xaml,Locked.txt,Program.cs", string.Join(',', rows.Select(r => r.Text)));
                Check.Equal("Program.cs", Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(rows[2]));
                Check.True(rows[2].IsChecked);
                Check.False(rows[0].IsChecked);
                Check.True(rows[0].IsEnabled);
                Check.False(rows[1].IsEnabled);
                Check.True(ShowsIcon(rows[2], f.Document.IconSource!));
                Check.True(ShowsText(rows[0], "App.xaml"));
                Check.True(ShowsIcon(rows[0], f.Second.IconSource!));
                Check.True(rows.All(r => r.ActualHeight >= 20));
                // Rows stay keyboard-focusable menu items with a focus highlight.
                Check.True(rows[0].Focus(FocusState.Keyboard));
                await f.Settle();
                Check.False(ReferenceEquals(rows[0].Background, rows[2].Background));
                await VisualCapture.Save(rows[2], Path.Combine(output, "templates-icons", "document-row.png"));
            }
            finally
            {
                menu.Hide();
            }
        });
        tests.Test("documents list: DocumentPaneMenuItemHeaderTemplate and selector present rows", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.DocumentPaneMenuItemHeaderTemplate = BoundMarker("menu-row");
            var (menu, rows) = await f.OpenDocuments();
            try
            {
                foreach (var (row, content) in rows.Zip(new LayoutContent[] { f.Second, f.Third, f.Document }))
                {
                    Check.True(ShowsText(row, "menu-row"));
                    Check.True(ShowsText(row, content.ContentId!), "The row data context must be the listed LayoutDocument.");
                    Check.False(row.FindVisualChildren<Image>().Any(Visible));
                }

                Check.True(rows[2].IsChecked);
            }
            finally
            {
                menu.Hide();
            }

            var selector = new Selector(item => ReferenceEquals(item, f.Second) ? BoundMarker("second-row") : null);
            f.Manager.DocumentPaneMenuItemHeaderTemplate = null;
            f.Manager.DocumentPaneMenuItemHeaderTemplateSelector = selector;
            (menu, rows) = await f.OpenDocuments();
            try
            {
                Check.True(selector.Items.Contains(f.Document));
                Check.True(ShowsText(rows[0], "second-row"));
                Check.True(ShowsIcon(rows[2], f.Document.IconSource!), "A null selection keeps the default row.");
                Check.True(ShowsText(rows[2], f.Document.Title!));
            }
            finally
            {
                menu.Hide();
            }
        });
        tests.Test("documents list: invoking a templated row activates its document and revalidates", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.DocumentPaneMenuItemHeaderTemplate = BoundMarker("menu-row");
            var (menu, rows) = await f.OpenDocuments();
            try
            {
                var clicked = false;
                rows[0].Click += (_, _) => clicked = true;
                Invoke(rows[0]);
                var activeNow = f.Second.IsActive;
                await f.Settle();
                Check.True(clicked, "The row invocation must raise Click.");
                Check.True(activeNow, "Click must activate the document.");
                // Focus restoration after the programmatically opened menu may move
                // activation; the pane selection is the durable result.
                Check.True(f.Second.IsSelected);
            }
            finally
            {
                menu.Hide();
            }

            (menu, rows) = await f.OpenDocuments();
            try
            {
                // A row of a content that left the pane must not activate it.
                f.Document.Close();
                await f.Settle();
                Invoke(rows[2]);
                await f.Settle();
                Check.False(f.Document.IsActive);
                Check.True(f.Document.Parent == null);
            }
            finally
            {
                menu.Hide();
            }
        });
        tests.Test("navigator rows show icons, then document and tool header templates", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var nav = await f.ShowNavigator();
            try
            {
                Check.True(ShowsIcon(f.Row(nav, f.Document), f.Document.IconSource!));
                Check.True(ShowsText(f.Row(nav, f.Document), f.Document.Title!));
                Check.True(ShowsIcon(f.Row(nav, f.Explorer), f.Explorer.IconSource!));
            }
            finally
            {
                f.CloseNavigator();
            }

            f.Manager.DocumentHeaderTemplate = BoundMarker("navigator-document");
            f.Manager.AnchorableHeaderTemplate = BoundMarker("navigator-tool");
            nav = await f.ShowNavigator();
            try
            {
                var document = f.Row(nav, f.Document);
                Check.True(ShowsText(document, "navigator-document"));
                Check.True(ShowsText(document, f.Document.ContentId!), "Navigator header templates receive the LayoutContent.");
                Check.False(document.FindVisualChildren<Image>().Any(Visible));
                var tool = f.Row(nav, f.Explorer);
                Check.True(ShowsText(tool, "navigator-tool"));
                Check.True(ShowsText(tool, f.Explorer.ContentId!));
                await VisualCapture.Save(nav, Path.Combine(output, "templates-icons", "navigator-templates.png"));
            }
            finally
            {
                f.CloseNavigator();
            }
        });
        tests.Test("navigator rows follow IconContentTemplate and live title changes", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Manager.IconContentTemplate = IconMarker("navigator-icon");
            var nav = await f.ShowNavigator();
            try
            {
                var row = f.Row(nav, f.Second);
                Check.True(ShowsText(row, "navigator-icon"));
                f.Second.Title = "Renamed.xaml";
                await f.Settle();
                Check.True(ShowsText(f.Row(nav, f.Second), "Renamed.xaml"));
            }
            finally
            {
                f.CloseNavigator();
            }
        });
        tests.Test("floating captions show the icon and the document or tool title template", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            f.Second.Float();
            f.Manager.Refresh();
            await f.Settle();
            var floating = f.Manager.FloatingWindows.Single();
            var caption = Field<TextBlock>(floating, "_caption");
            Check.True(ShowsIcon(Caption(floating), f.Second.IconSource!));
            Check.Equal("App.xaml", caption.Text);
            Check.Equal(Visibility.Visible, caption.Visibility);
            Check.False(Caption(floating).IsHitTestVisible);
            f.Manager.DocumentTitleTemplate = BoundMarker("caption-document");
            await f.Settle();
            Check.True(ShowsText(Caption(floating), "caption-document"));
            Check.True(ShowsText(Caption(floating), f.Second.ContentId!));
            Check.Equal(Visibility.Collapsed, caption.Visibility);
            Check.Equal("App.xaml", caption.Text);
            Check.False(Caption(floating).FindVisualChildren<Image>().Any(Visible));
            f.Manager.DocumentTitleTemplate = null;
            f.Manager.IconContentTemplate = IconMarker("caption-icon");
            await f.Settle();
            Check.True(ShowsText(Caption(floating), "caption-icon"));
            Check.True(ShowsText(Caption(floating), "App.xaml"));
            f.Manager.IconContentTemplate = null;
            f.Output.Float();
            f.Manager.Refresh();
            await f.Settle();
            var tool = f.Manager.FloatingWindows.Single(w => w.Model is LayoutAnchorableFloatingWindow);
            Check.True(ShowsIcon(Caption(tool), f.Output.IconSource!));
            f.Manager.AnchorableTitleTemplateSelector = new Selector(item => ReferenceEquals(item, f.Output) ? BoundMarker("caption-tool") : null);
            f.Manager.Refresh();
            await f.Settle();
            Check.True(ShowsText(Caption(tool), "caption-tool"));
            Check.True(ShowsText(Caption(tool), f.Output.ContentId!));
            await VisualCapture.Save(tool, Path.Combine(output, "templates-icons", "floating-caption.png"));
        });
        tests.Test("template changes refresh without replacing tabs or content", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var tab = f.Tab(f.Document);
            var editor = f.Document.Content;
            f.Manager.DocumentHeaderTemplate = BoundMarker("live-header");
            f.Manager.Refresh();
            Check.True(ShowsText(tab, "live-header"), "Refresh() must apply a changed header template synchronously.");
            f.Manager.DocumentHeaderTemplate = null;
            await f.Settle();
            Check.True(ShowsIcon(tab, f.Document.IconSource!), "A template property change must refresh the next render.");
            Check.Same(tab, f.Tab(f.Document));
            Check.Same(editor, f.Document.Content);
            var icon = new WriteableBitmap(2, 2);
            f.Document.IconSource = icon;
            await f.Settle();
            Check.True(ShowsIcon(tab, icon));
        });
        tests.Test("default icon element accepts image sources, addresses and WinUI icon sources", () =>
        {
            var create = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockIcon", true)!.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.NonPublic)!;
            FrameworkElement? Create(object icon, object? existing = null) => (FrameworkElement?)create.Invoke(null, [icon, existing]);
            var bitmap = new WriteableBitmap(1, 1);
            var image = (Image)Create(bitmap)!;
            Check.Same(bitmap, image.Source);
            Check.Near(16, image.Width);
            Check.Near(16, image.Height);
            Check.Same(image, Create(new WriteableBitmap(1, 1), image));
            var uri = (Image)Create(new Uri("ms-appx:///Assets/Icons/document.png"))!;
            Check.Equal("ms-appx:///Assets/Icons/document.png", ((BitmapImage)uri.Source).UriSource.ToString());
            var relative = (Image)Create("Assets/Icons/tool.png")!;
            Check.Equal("ms-appx:///Assets/Icons/tool.png", ((BitmapImage)relative.Source).UriSource.ToString());
            var symbol = new SymbolIconSource
            {
                Symbol = Symbol.Document
            };
            var element = (IconSourceElement)Create(symbol)!;
            Check.Same(symbol, element.IconSource);
            Check.True(Create(42) == null);
        });
        tests.Test("gallery: XAML workbench declares tool icons and the documents list template", () =>
        {
            using var view = new XamlWorkbenchView();
            var manager = view.Manager;
            Check.True(manager.DocumentPaneMenuItemHeaderTemplate != null);
            var tools = manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
            Check.Equal(2, tools.Length);
            Check.True(tools.All(t => t.IconSource is BitmapImage { UriSource: not null }), "XAML string icon sources must convert to bitmap images.");
        });
        tests.Test("gallery: icons, runtime tool placement, tool menu and documents list template", async () =>
        {
            using var page = new GalleryPage
            {
                Width = 1000,
                Height = 700
            };
            var window = new Window
            {
                Content = page,
                Title = "UnoDock gallery templates"
            };
            window.AppWindow.Resize(new()
            {
                Width = 1100,
                Height = 800
            });
            window.Activate();
            try
            {
                await Until(() => page.IsLoaded && page.Dock.ActualWidth > 0);
                Check.True(page.Dock.Layout.Descendents().OfType<LayoutContent>().All(c => c.IconSource != null), "Every docking sample content has an icon.");
                Check.True(page.Dock.LayoutUpdateStrategy == null && page.Dock.AnchorableContextMenu == null, "The shared classic sample keeps the default tool placement and menu.");
                page.SwitchSample(SampleKind.Workspace);
                await Task.Delay(80);
                Check.True(page.Dock.LayoutUpdateStrategy is NamedPaneLayoutStrategy);
                Check.True(page.Dock.AnchorableContextMenu != null);
                Check.True(page.Dock.DocumentPaneMenuItemHeaderTemplate != null);
                page.ExecuteSampleCommand("new-tool");
                await Task.Delay(80);
                var added = page.Dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == "tool-1");
                Check.Equal(GalleryPage.WorkspaceToolsPane, ((LayoutAnchorablePane)added.Parent!).Name);
                Check.True(added.IconSource != null);
                var menu = (MenuFlyout)page.Dock.FindVisualChildren<LayoutAnchorableTabItem>().First(t => ReferenceEquals(t.Model, added)).ContextFlyout;
                Check.Same(page.Dock.AnchorableContextMenu, menu);
                page.SwitchSample(SampleKind.Classic);
                await Task.Delay(80);
                Check.True(page.Dock.LayoutUpdateStrategy == null && page.Dock.AnchorableContextMenu == null && page.Dock.DocumentPaneMenuItemHeaderTemplate == null);
                page.ExecuteSampleCommand("new-tool");
                await Task.Delay(80);
                var classic = page.Dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == "tool-2");
                Check.True(((LayoutAnchorablePane)classic.Parent!).Name != GalleryPage.WorkspaceToolsPane);
            }
            finally
            {
                window.Close();
            }
        });
        return await tests.Run(output, "templates-icons");
    }

    private sealed class Selector(Func<object, DataTemplate?> select) : DataTemplateSelector
    {
        internal readonly List<object> Items = [];
        protected override DataTemplate? SelectTemplateCore(object item)
        {
            Items.Add(item);
            return select(item);
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
    }

    private static DataTemplate IconMarker(string text) => (DataTemplate)XamlReader.Load($"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{text}' FontSize='9'/></DataTemplate>");
    private static DataTemplate BoundMarker(string text) => (DataTemplate)XamlReader.Load($"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal' Spacing='4'><TextBlock Text='{text}'/><TextBlock Text='{{Binding ContentId}}'/></StackPanel></DataTemplate>");
    private static bool Visible(UIElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed })
                return false;
        return true;
    }

    private static bool ShowsIcon(DependencyObject host, ImageSource icon) => host.FindVisualChildren<Image>().Any(i => ReferenceEquals(i.Source, icon) && Visible(i));
    private static bool ShowsText(DependencyObject host, string text) => host.FindVisualChildren<TextBlock>().Any(t => t.Text == text && Visible(t));
    private static FrameworkElement Caption(LayoutFloatingWindowControl window) => Field<FrameworkElement>(window, "_captionHeader");
    private static T Field<T>(object instance, string name)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
                return (T)field.GetValue(instance)!;
        throw new MissingFieldException(instance.GetType().Name, name);
    }

    /// <summary>The platform's own item invocation: toggle, Click and menu dismissal.</summary>
    private static void Invoke(ToggleMenuFlyoutItem row)
    {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(row);
        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
            return;
        }

        var method = typeof(MenuFlyoutItem).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, Type.EmptyTypes) ?? throw new MissingMethodException(nameof(MenuFlyoutItem), "Invoke");
        method.Invoke(row, null);
    }

    private static async Task Until(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(16);
        Check.True(condition(), "Asynchronous condition did not complete.");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            Width = 900,
            Height = 520,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        internal readonly LayoutDocument Document = Doc("Program.cs", "template-program");
        internal readonly LayoutDocument Second = Doc("App.xaml", "template-app");
        internal readonly LayoutDocument Third = Doc("Locked.txt", "template-locked");
        internal readonly LayoutAnchorable Explorer = Tool("Explorer", "template-explorer");
        internal readonly LayoutAnchorable Output = Tool("Output", "template-output");
        internal readonly LayoutAnchorable RailTool = Tool("Rail tool", "template-rail");
        private readonly LayoutDocumentPane _documents;
        private readonly Window _window;
        internal Fixture()
        {
            var tools = new LayoutAnchorablePane(Explorer)
            {
                DockWidth = new(220)
            };
            tools.Children.Add(Output);
            _documents = new LayoutDocumentPane(Document);
            _documents.Children.Add(Second);
            _documents.Children.Add(Third);
            var panel = new LayoutPanel(tools);
            panel.Children.Add(_documents);
            Manager.Layout = new()
            {
                RootPanel = panel
            };
            var rail = new LayoutAnchorGroup();
            rail.Children.Add(RailTool);
            Manager.Layout.LeftSide.Children.Add(rail);
            _window = new()
            {
                Content = Manager,
                Title = "UnoDock template and icon acceptance"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 960,
                Height = 600
            });
        }

        private static LayoutDocument Doc(string title, string id) => new()
        {
            Title = title,
            ContentId = id,
            IconSource = new WriteableBitmap(1, 1),
            Content = new TextBox
            {
                Text = title
            }
        };
        private static LayoutAnchorable Tool(string title, string id) => new()
        {
            Title = title,
            ContentId = id,
            IconSource = new WriteableBitmap(1, 1),
            Content = new TextBlock
            {
                Text = title
            }
        };
        internal async Task Show()
        {
            _window.Activate();
            Document.IsActive = true;
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
        internal LayoutAnchorControl Rail(LayoutAnchorable content) => Manager.FindVisualChildren<LayoutAnchorControl>().First(c => ReferenceEquals(c.Model, content));
        internal async Task<(MenuFlyout Menu, ToggleMenuFlyoutItem[] Rows)> OpenDocuments()
        {
            var pane = Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single();
            var menu = (MenuFlyout)typeof(LayoutCachePaneControl).GetMethod("CreateDocumentsMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pane, null)!;
            menu.ShowAt(Field<FrameworkElement>(pane, "_documentsButton"));
            var rows = menu.Items.OfType<ToggleMenuFlyoutItem>().ToArray();
            await Until(() => rows.All(r => r.ActualHeight > 0));
            await Settle();
            return (menu, rows);
        }

        internal async Task<NavigatorWindow> ShowNavigator()
        {
            var nav = new NavigatorWindow(Manager);
            Surface("ShowNavigator", nav);
            await Until(() => nav.ActualHeight > 0 && nav.Documents.Length > 0 && TryRow(nav, Document) is { ActualHeight: > 0 });
            await Settle();
            return nav;
        }

        internal void CloseNavigator() => Surface("CloseNavigator", false);
        internal FrameworkElement Row(NavigatorWindow nav, LayoutContent content) => TryRow(nav, content) ?? throw new InvalidOperationException("No navigator row for " + content.Title + ".");
        private static FrameworkElement? TryRow(NavigatorWindow nav, LayoutContent content)
        {
            var list = Field<ListBox>(nav, content is LayoutDocument ? "_documentsList" : "_anchorablesList");
            var item = list.Items.OfType<LayoutItem>().FirstOrDefault(i => ReferenceEquals(i.LayoutElement, content));
            return item == null ? null : list.ContainerFromItem(item) as FrameworkElement;
        }

        private void Surface(string name, params object[] args)
        {
            var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager)!;
            surface.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(surface, args);
        }

        public void Dispose()
        {
            Manager.Dispose();
            _window.Close();
        }
    }
}
