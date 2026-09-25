using UnoDock.Controls;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private void ShowDockingGuidesLab()
    {
        var manager = new GalleryDockingManager
        {
            MinHeight = 420,
            DockingGuideMode = DockingGuideMode.GuidesOnly,
            RequestedTheme = ElementTheme.Light,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var panel = new Grid
        {
            RowSpacing = 6
        };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new(6)
        };
        var status = new TextBlock
        {
            Margin = new(10, 0, 10, 6),
            TextWrapping = TextWrapping.Wrap,
            Text = "Drag a header. Inner guides create document groups; optional extended guides keep tools as tools. Workspace edge guides dock outside all groups. Only valid targets are drawn. Escape cancels."
        };
        var modes = new ComboBox
        {
            ItemsSource = Enum.GetValues<DockingGuideMode>(),
            SelectedItem = manager.DockingGuideMode,
            MinWidth = 160
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(modes, "Docking guide input policy");
        modes.SelectionChanged += (_, _) =>
        {
            if (modes.SelectedItem is DockingGuideMode mode)
                manager.DockingGuideMode = mode;
        };
        controls.Children.Add(modes);
        Add("Reset", Populate);
        Add("Large guides", () =>
        {
            manager.Resources["UnoDock.GuideSize"] = 44d;
            manager.Refresh();
        });
        Add("Stock size", () =>
        {
            manager.Resources.Remove("UnoDock.GuideSize");
            manager.Refresh();
        });
        Add("Light / dark", () =>
        {
            manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
            manager.Refresh();
        });
        Add("RTL / LTR", () =>
        {
            manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            manager.Refresh();
        });
        Add("Tool → document permission", () =>
        {
            foreach (var tool in manager.Layout.Descendents().OfType<LayoutAnchorable>())
                tool.CanDockAsTabbedDocument = !tool.CanDockAsTabbedDocument;
            status.Text = "Tool-to-document permission toggled. Disallowed inner document guides disappear; workspace guides remain. Enable Extended tool guides for additional tool-as-tool placements.";
        });
        Add("Extended tool guides", () => manager.ShowDocumentPaneToolGuides = !manager.ShowDocumentPaneToolGuides);
        Add("Mixed orientations", () => manager.AllowMixedOrientation = !manager.AllowMixedOrientation);
        Add("Float active", () => manager.Layout.ActiveContent?.Float());
        var veto = new CheckBox
        {
            Content = "Veto drops"
        };
        controls.Children.Add(veto);
        manager.PreviewDock += (_, e) =>
        {
            var args = (DockEventArgs)e;
            args.Cancel = veto.IsChecked == true;
            status.Text = args.Cancel ? "Drop vetoed: layout remains unchanged." : "Validated docking transition accepted.";
        };
        var bar = new ScrollViewer
        {
            Content = controls,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        panel.Children.Add(bar);
        Grid.SetRow(status, 1);
        panel.Children.Add(status);
        Grid.SetRow(manager, 2);
        panel.Children.Add(manager);
        Populate();
        var document = new LayoutDocument
        {
            Title = "Docking guides",
            ContentId = "docking-guides-lab:" + Guid.NewGuid().ToString("N"),
            Content = panel
        };
        document.Closed += (_, _) => manager.Dispose();
        var root = Dock.Layout;
        EventHandler? changed = null;
        changed = (_, _) =>
        {
            if (!ReferenceEquals(Dock.Layout, root))
            {
                manager.Dispose();
                Dock.LayoutChanged -= changed;
            }
        };
        Dock.LayoutChanged += changed;
        document.Closed += (_, _) => Dock.LayoutChanged -= changed;
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null)
        {
            pane = new();
            Dock.Layout.RootPanel.Children.Add(pane);
        }

        pane.Children.Add(document);
        document.IsActive = true;
        void Populate()
        {
            var documents = new LayoutDocumentPane();
            foreach (var title in new[]
            {
                "Workspace.cs",
                "Readme.md",
                "Notes.txt"
            }

            )
                documents.Children.Add(new LayoutDocument { Title = title, ContentId = "guide-doc:" + title, Content = new TextBox { Text = "// " + title + "\n// Drag document or tool headers over the explicit docking guides.\n// The same plan validates the preview and final drop.", AcceptsReturn = true, Padding = new(12) } });
            var tools = new LayoutAnchorablePane
            {
                DockWidth = new(210)
            };
            foreach (var title in new[]
            {
                "Solution Explorer",
                "Toolbox"
            }

            )
                tools.Children.Add(new() { Title = title, ContentId = "guide-tool:" + title, Content = new TextBox { Text = title, AcceptsReturn = true } });
            var properties = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Properties", ContentId = "guide-properties", Content = new TextBox { Text = "CanDockAsTabbedDocument = true" } })
            {
                DockWidth = new(200)
            };
            var docGroup = new LayoutDocumentPaneGroup(documents);
            var workspace = new LayoutPanel(tools);
            workspace.Children.Add(docGroup);
            workspace.Children.Add(properties);
            manager.Layout = new()
            {
                RootPanel = workspace
            };
            documents.Children[0].IsActive = true;
            manager.Refresh();
        }

        void Add(string text, Action action)
        {
            var button = new Button
            {
                Content = text,
                FontSize = 12,
                Padding = new(8, 4, 8, 4)
            };
            button.Click += (_, _) => action();
            controls.Children.Add(button);
        }
    }
}
