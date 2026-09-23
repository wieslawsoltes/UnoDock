using Xceed.Wpf.AvalonDock.Controls;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowNavigatorLab()
    {
        var manager = new GalleryDockingManager { MinHeight = 400, RequestedTheme = ElementTheme.Light };
        var panel = new Grid { RowSpacing = 6 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6) };
        Add("Open navigator", manager.OpenNavigator);
        Add("3 documents", () => Populate(3)); Add("40 documents", () => Populate(40)); Add("200 documents", () => Populate(200));
        Add("Light / dark", () => { manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark; manager.Refresh(); });
        Add("RTL / LTR", () => { manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; manager.Refresh(); });
        Add("Large font", () => { manager.Resources["UnoDock.FontSize"] = 20d; manager.Refresh(); });
        Add("Stock density", () => { manager.Resources.Remove("UnoDock.FontSize"); manager.Refresh(); });
        var bar = new ScrollViewer { Content = commands, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var help = new TextBlock { Margin = new(10, 0, 10, 6), TextWrapping = TextWrapping.Wrap,
            Text = "Ctrl+Tab opens the compact navigator. Use Up/Down, Home/End and Left/Right to preview entries; Enter or Control release commits, Escape cancels. Off-screen selections are revealed without replacing the lists. Each editor keeps its content and focus." };
        panel.Children.Add(bar); Grid.SetRow(help, 1); panel.Children.Add(help); Grid.SetRow(manager, 2); panel.Children.Add(manager);
        Populate(3);
        var document = new LayoutDocument { Title = "Navigator quality", ContentId = "navigator-lab:" + Guid.NewGuid().ToString("N"), Content = panel };
        document.Closed += (_, _) => manager.Dispose();
        var root = Dock.Layout;
        EventHandler? changed = null;
        changed = (_, _) => { if (!ReferenceEquals(Dock.Layout, root)) { manager.Dispose(); Dock.LayoutChanged -= changed; } };
        Dock.LayoutChanged += changed; document.Closed += (_, _) => Dock.LayoutChanged -= changed;
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;
        void Populate(int count)
        {
            var documents = new LayoutDocumentPane();
            for (var i = 0; i < count; i++) documents.Children.Add(new LayoutDocument
            {
                ContentId = "navigator-editor:" + i, Title = $"Document {i:D3}.cs", Description = $"Project / Source / Document {i:D3}.cs",
                Content = new TextBox { Text = $"// Document {i:D3}\n// Edit, switch to another document, then return here.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(14) }
            });
            var tools = new LayoutAnchorablePane { DockWidth = new(200) };
            foreach (var title in new[] { "Solution Explorer", "Properties", "Output" })
                tools.Children.Add(new() { Title = title, ContentId = "navigator-tool:" + title, Content = new TextBox { Text = title, AcceptsReturn = true } });
            var workspace = new LayoutPanel(tools); workspace.Children.Add(documents); manager.Layout = new() { RootPanel = workspace };
            documents.Children[0].IsActive = true; manager.Refresh();
        }
        void Add(string title, Action action)
        { var button = new Button { Content = title, FontSize = 12, Padding = new(8, 4, 8, 4) }; button.Click += (_, _) => action(); commands.Children.Add(button); }
    }
}
