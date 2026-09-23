using Xceed.Wpf.AvalonDock.Controls;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowSplitterLab()
    {
        var manager = new GalleryDockingManager { MinHeight = 360, RequestedTheme = ElementTheme.Light };
        var container = new Grid();
        container.RowDefinitions.Add(new() { Height = GridLength.Auto });
        container.RowDefinitions.Add(new() { Height = GridLength.Auto });
        container.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6) };
        var status = new TextBlock { Margin = new(10, 4, 10, 8), TextWrapping = TextWrapping.Wrap };
        var vertical = false;
        Add("Reset 1* / 2*", () => Populate(false));
        Add("Pixel / star", () => Populate(true));
        Add("Horizontal / vertical", () => { vertical = !vertical; Populate(false); });
        Add("RTL / LTR", () => manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
        Add("Light / dark", () => { manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light; manager.Refresh(); });
        Add("Cancel resize", () => { foreach (var splitter in manager.FindVisualChildren<LayoutGridResizerControl>()) splitter.CancelDrag(); });
        Add("Save XML", () =>
        {
            using var writer = new StringWriter(); new XmlLayoutSerializer(manager).Serialize(writer);
            status.Text = writer.ToString();
        });
        var bar = new ScrollViewer { Content = controls, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        container.Children.Add(bar); Grid.SetRow(status, 1); container.Children.Add(status); Grid.SetRow(manager, 2); container.Children.Add(manager);
        Populate(false);
        var document = new LayoutDocument { Title = "Splitter quality", ContentId = "splitters:" + Guid.NewGuid().ToString("N"), Content = container };
        var ownerRoot = Dock.Layout;
        EventHandler? ownerChanged = null;
        ownerChanged = (_, _) => { if (!ReferenceEquals(ownerRoot, Dock.Layout)) { manager.Dispose(); Dock.LayoutChanged -= ownerChanged; } };
        Dock.LayoutChanged += ownerChanged;
        document.Closed += (_, _) => { Dock.LayoutChanged -= ownerChanged; manager.Dispose(); };
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;

        void Populate(bool mixed)
        {
            var left = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Tools", ContentId = "resize-tools", Content = new TextBox {
                Text = "Drag the divider: only the translucent preview moves.\nRelease to commit; Escape cancels.\nMinimum size: 100 DIPs.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(12) } })
                { DockWidth = mixed ? new(220) : new(1, GridUnitType.Star), DockHeight = mixed ? new(220) : new(1, GridUnitType.Star), DockMinWidth = 100, DockMinHeight = 100 };
            var right = new LayoutDocumentPane(new LayoutDocument { Title = "Editor.cs", ContentId = "resize-editor", Content = new TextBox {
                Text = "// Editor controls do not resize during preview.\n// Star weights remain responsive after commit.\n// Focus the divider and use axis-aligned arrows.\n// Physical Left/Right is RTL-aware.", AcceptsReturn = true, Padding = new(12) } })
                { DockWidth = new(2, GridUnitType.Star), DockHeight = new(2, GridUnitType.Star), DockMinWidth = 100, DockMinHeight = 100 };
            var layout = new LayoutPanel(left) { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal }; layout.Children.Add(right);
            manager.Layout = new() { RootPanel = layout };
            left.PropertyChanged += (_, e) => { if (e.PropertyName is "DockWidth" or "DockHeight") UpdateStatus(); };
            right.PropertyChanged += (_, e) => { if (e.PropertyName is "DockWidth" or "DockHeight") UpdateStatus(); };
            UpdateStatus();
            void UpdateStatus() => status.Text = "Persisted sizes: " + (vertical ? left.DockHeight : left.DockWidth) + " / " + (vertical ? right.DockHeight : right.DockWidth) + ". Dragging leaves these unchanged until commit.";
        }
        void Add(string text, Action action)
        { var button = new Button { Content = text, Padding = new(8, 4, 8, 4), FontSize = 12 }; button.Click += (_, _) => action(); controls.Children.Add(button); }
    }
}
