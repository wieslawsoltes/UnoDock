using UnoDock.Testing;
using Xceed.Wpf.AvalonDock.Themes;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowVisualParityLab()
    {
        // This fixture uses application-owned content shared with the independent
        // reference probe; no original templates or graphics are embedded here.
        var manager = new DockingManager { MinHeight = 480, RequestedTheme = ElementTheme.Light };
        VisualScene.Populate(manager);
        var panel = new Grid { RowSpacing = 6 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6) };
        Add("Classic", () => { manager.Theme = null; manager.RequestedTheme = ElementTheme.Light; });
        Add("Dark", () => { manager.Theme = null; manager.RequestedTheme = ElementTheme.Dark; });
        Add("Right-to-left", () => manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
        Add("Auto-hide Explorer", () => manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == "explorer")?.ToggleAutoHide());
        Add("32 DIP tabs", () => { manager.Resources["UnoDock.TabHeight"] = 32d; manager.Refresh(); });
        Add("Reset", () => { manager.Resources.Remove("UnoDock.TabHeight"); VisualScene.Populate(manager); manager.Refresh(); });
        var bar = new ScrollViewer { Content = commands, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.Children.Add(bar); Grid.SetRow(manager, 1); panel.Children.Add(manager);
        var document = new LayoutDocument { Title = "Visual parity", ContentId = "visual-parity:" + Guid.NewGuid().ToString("N"), Content = panel };
        document.Closed += (_, _) => manager.Dispose();
        // Root replacement can bypass the document's close path; dispose then as well.
        var root = Dock.Layout;
        EventHandler? changed = null;
        changed = (_, _) => { if (!ReferenceEquals(Dock.Layout, root)) { manager.Dispose(); Dock.LayoutChanged -= changed; } };
        Dock.LayoutChanged += changed;
        document.Closed += (_, _) => Dock.LayoutChanged -= changed;
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;
        void Add(string title, Action action)
        { var button = new Button { Content = title, FontSize = 12, Padding = new(8, 4, 8, 4) }; button.Click += (_, _) => action(); commands.Children.Add(button); }
    }
}
