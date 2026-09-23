using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Themes;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowAutoHideLab()
    {
        var manager = new GalleryDockingManager { MinHeight = 360, RequestedTheme = ElementTheme.Light };
        var panel = new Grid(); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6) };
        var status = new TextBlock { Margin = new(10, 4, 10, 8), TextWrapping = TextWrapping.Wrap };
        LayoutAnchorable[] tools = [];
        Add("Reset four sides", Populate);
        Add("Minimum defaults", () => { foreach (var tool in tools) tool.AutoHideWidth = tool.AutoHideHeight = 0; });
        Add("RTL / LTR", () => manager.FlowDirection = manager.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft);
        Add("Light / dark", () => { manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark; manager.Theme = new FluentTheme(manager.RequestedTheme); });
        Add("Cancel resize", () => { foreach (var resizer in manager.FindVisualChildren<LayoutGridResizerControl>()) resizer.CancelDrag(); });
        Add("XML", () => { using var xml = new StringWriter(); new XmlLayoutSerializer(manager).Serialize(xml); status.Text = xml.ToString(); });
        panel.Children.Add(new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var statusView = new ScrollViewer { Content = status, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(statusView, 1); panel.Children.Add(statusView); Grid.SetRow(manager, 2); panel.Children.Add(manager); Populate();
        var document = new LayoutDocument { Title = "Auto-hide quality", ContentId = "auto-hide:" + Guid.NewGuid().ToString("N"), Content = panel };
        var ownerRoot = Dock.Layout;
        EventHandler? ownerChanged = null;
        ownerChanged = (_, _) => { if (!ReferenceEquals(ownerRoot, Dock.Layout)) { manager.Dispose(); Dock.LayoutChanged -= ownerChanged; } };
        Dock.LayoutChanged += ownerChanged;
        document.Closed += (_, _) => { Dock.LayoutChanged -= ownerChanged; manager.Dispose(); };
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;

        void Populate()
        {
            var editor = new LayoutDocument { Title = "Workspace.cs", ContentId = "editor", Content = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(16), Text = "Hover a side label: the flyout appears without activating its tool. Click or focus the editor to activate.\n\nResize from the flyout's separate gutter: only the preview moves until release. Escape cancels. The saved content dimension excludes the six-DIP gutter.\n\nFocus inside the tool or keep its context menu open: the flyout stays visible. Move focus and the pointer out to exercise delayed closing." } };
            manager.Layout = new() { RootPanel = new(new LayoutDocumentPane(editor)) };
            tools = new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom }.Select(side =>
            {
                var tool = new LayoutAnchorable { ContentId = "tool-" + side, Title = side + " tool", AutoHideWidth = 280, AutoHideHeight = 180,
                    Content = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(12), Text = side + " auto-hidden editor.\nRetains content, focus and saved size across hide/show.\nUse the pin to restore the pane." } };
                tool.AddToLayout(manager, side | AnchorableShowStrategy.Most); tool.ToggleAutoHide();
                tool.PropertyChanged += (_, e) => { if (e.PropertyName is "AutoHideWidth" or "AutoHideHeight") UpdateStatus(); };
                return tool;
            }).ToArray();
            editor.IsActive = true; UpdateStatus();
        }
        void UpdateStatus() => status.Text = string.Join("  |  ", tools.Select(t => $"{t.Title}: {t.AutoHideWidth:0.##} × {t.AutoHideHeight:0.##}"));
        void Add(string title, Action action)
        { var button = new Button { Content = title, Padding = new(8, 4, 8, 4), FontSize = 12 }; button.Click += (_, _) => action(); actions.Children.Add(button); }
    }
}
