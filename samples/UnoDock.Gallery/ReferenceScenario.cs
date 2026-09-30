using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Gallery;
/// <summary>Side-by-side review scenes: the same layout, sizes and actions as the
/// reference review harness, selected with UNODOCK_SCENARIO=&lt;mode&gt; and themed
/// with UNODOCK_GALLERY_THEME. Modes: docked, active-doc, active-tool, floating,
/// float-both, autohide, context-doc, dropdown, tool-menu, navigator.</summary>
internal static class ReferenceScenario
{
    internal static string? Mode => Environment.GetEnvironmentVariable("UNODOCK_SCENARIO") is { Length: > 0 } mode ? mode : null;

    internal static DockingManager Create(Window window, string mode)
    {
        var manager = new ScenarioManager
        {
            Theme = Environment.GetEnvironmentVariable("UNODOCK_GALLERY_THEME")?.ToLowerInvariant() switch
            {
                "aero" => new AeroTheme(),
                "metro" => new MetroTheme(),
                "vs2010" => new VS2010Theme(),
                "light" or "fluent" => new FluentTheme(ElementTheme.Light),
                "dark" => new FluentTheme(ElementTheme.Dark),
                _ => new GenericTheme()
            }
        };
        var root = new LayoutRoot();
        var panel = new LayoutPanel
        {
            Orientation = Orientation.Horizontal
        };
        var left = new LayoutAnchorablePane
        {
            DockWidth = new(240)
        };
        left.Children.Add(Tool("Solution Explorer", "explorer", List("Program.cs", "App.xaml", "MainWindow.xaml")));
        left.Children.Add(Tool("Toolbox", "toolbox", List("Button", "TextBox", "Grid")));
        var vertical = new LayoutPanel
        {
            Orientation = Orientation.Vertical
        };
        var documents = new LayoutDocumentPane();
        documents.Children.Add(Document("Document 1.cs", "d1", "Document 1 content"));
        documents.Children.Add(Document("Document 2.cs", "d2", "Document 2 content"));
        documents.Children.Add(Document("Very Long Document Name 3.xaml", "d3", "Document 3 content"));
        var bottom = new LayoutAnchorablePane
        {
            DockHeight = new(180)
        };
        bottom.Children.Add(Tool("Output", "output", Text("Build succeeded.")));
        bottom.Children.Add(Tool("Error List", "errors", List("CS0001", "CS0002")));
        vertical.Children.Add(new LayoutDocumentPaneGroup(documents));
        vertical.Children.Add(bottom);
        var right = new LayoutAnchorablePane
        {
            DockWidth = new(260)
        };
        right.Children.Add(Tool("Properties", "props", List("Name", "Width", "Height")));
        panel.Children.Add(left);
        panel.Children.Add(vertical);
        panel.Children.Add(right);
        root.RootPanel = panel;
        var side = new LayoutAnchorGroup();
        side.Children.Add(Tool("Server Explorer", "servers", List("localhost")));
        side.Children.Add(Tool("Data Sources", "data", List("Db1")));
        root.LeftSide.Children.Add(side);
        var bottomSide = new LayoutAnchorGroup();
        bottomSide.Children.Add(Tool("Task List", "tasks", List("TODO")));
        root.BottomSide.Children.Add(bottomSide);
        manager.Layout = root;
        var started = false;
        manager.Loaded += (_, _) =>
        {
            if (started)
                return;
            started = true;
            if (manager.XamlRoot is { } xamlRoot && (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
                DesktopWindowCoordinates.SetWindowBounds(window, new(40, 40, 1280, 800), xamlRoot.RasterizationScale);
            // Let the first layout and native placement settle, like the reference harness.
            var timer = manager.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1200);
            timer.IsRepeating = false;
            timer.Tick += (_, _) => Run(manager, root, mode);
            timer.Start();
        };
        return manager;
    }

    private static void Run(ScenarioManager manager, LayoutRoot root, string mode)
    {
        LayoutContent Content(string id) => root.Descendents().OfType<LayoutContent>().First(c => c.ContentId == id);
        var output = Content("output");
        var second = Content("d2");
        switch (mode)
        {
            case "floating":
                output.Float();
                second.Float();
                break;
            case "float-tool":
            case "float-doc":
            case "float-both":
                var index = 0;
                foreach (var content in mode switch
                {
                    "float-tool" => new[]
                    {
                        output
                    },
                    "float-doc" => new[]
                    {
                        second
                    },
                    _ => new[]
                    {
                        output,
                        second
                    }
                })
                {
                    content.FloatingLeft = 700 + index * 380;
                    content.FloatingTop = 300 + index * 60;
                    content.FloatingWidth = 360;
                    content.FloatingHeight = 260;
                    content.Float();
                    index++;
                }

                break;
            case "autohide":
                var servers = Content("servers");
                servers.IsActive = true;
                servers.IsSelected = true;
                break;
            case "active-tool":
                Content("props").IsActive = true;
                break;
            case "active-doc":
                Content("d3").IsActive = true;
                break;
            case "context-doc":
                var tab = manager.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, second));
                tab.ContextFlyout?.ShowAt(tab, new FlyoutShowOptions { Position = new(tab.ActualWidth / 2, tab.ActualHeight / 2) });
                break;
            case "dropdown":
                Invoke(manager, Properties.Resources.Pane_OpenDocuments);
                break;
            case "tool-menu":
                Invoke(manager, Properties.Resources.Anchorable_CxMenu_Hint);
                break;
            case "navigator":
                Content("d3").IsActive = true;
                manager.OpenNavigator();
                break;
        }
    }

    private static void Invoke(DockingManager manager, string name)
    {
        var button = manager.FindVisualChildren<Button>().First(b => AutomationProperties.GetName(b) == name && b.ActualWidth > 0);
        ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
    }

    private static LayoutAnchorable Tool(string title, string id, object content) => new()
    {
        Title = title,
        ContentId = id,
        Content = content
    };
    private static LayoutDocument Document(string title, string id, string text) => new()
    {
        Title = title,
        ContentId = id,
        Content = Text(text)
    };
    private static ListBox List(params string[] items)
    {
        var list = new ListBox();
        foreach (var item in items)
            list.Items.Add(item);
        return list;
    }

    private static TextBox Text(string text) => new()
    {
        Text = text,
        AcceptsReturn = true
    };
    private sealed class ScenarioManager : DockingManager
    {
        internal void OpenNavigator() => ShowNavigatorWindow();
    }
}
