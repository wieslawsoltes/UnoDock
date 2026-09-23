using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Compatibility;
using Microsoft.UI.Xaml.Automation;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowDropDownLab()
    {
        var panel = new StackPanel { Spacing = 12, Padding = new(18) };
        var log = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var entries = new Queue<string>();
        void Record(string text)
        {
            entries.Enqueue(text); while (entries.Count > 16) entries.Dequeue();
            log.Text = string.Join(Environment.NewLine, entries);
        }
        var menu = new MenuFlyout();
        var first = new Xceed.Wpf.AvalonDock.Controls.DropDownButton { Content = "Primary  ▾", Padding = new(8, 2, 8, 2), MinHeight = 22, FontSize = 12 };
        var second = new Xceed.Wpf.AvalonDock.Controls.DropDownButton { Content = "Secondary  ▾", Padding = new(8, 2, 8, 2), MinHeight = 22, FontSize = 12 };
        AutomationProperties.SetName(first, "Primary document actions");
        AutomationProperties.SetName(second, "Secondary document actions");
        first.DropDownContextMenu = second.DropDownContextMenu = menu;
        first.DropDownContextMenuDataContext = "Primary trigger";
        second.DropDownContextMenuDataContext = "Secondary trigger";
        var triggers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        triggers.Children.Add(first); triggers.Children.Add(second);
        var area = new GalleryDropDownArea(Record)
        {
            IsTabStop = true, MinHeight = 75, DropDownContextMenu = menu,
            DropDownContextMenuDataContext = "Right-click area",
            Content = new Border
            {
                BorderThickness = new(1), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray), Padding = new(12),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Child = new TextBlock { Text = "Right-click here, or focus this area and press the context-menu key / Shift+F10.\nThe protected right-button hooks run before the opening.", TextWrapping = TextWrapping.Wrap }
            }
        };
        AutomationProperties.SetName(area, "Context menu extension area");
        var document = new LayoutDocument { Title = "Dropdown contracts", ContentId = "dropdown-lab:" + Guid.NewGuid().ToString("N"), Content = new ScrollViewer { Content = panel } };
        AddMenu("Float document", () => document.Float());
        AddMenu("Dock document", () => document.Dock());
        AddMenu("Change opening context", () => { first.DropDownContextMenuDataContext = "Updated primary"; Record("Changed primary context while keeping the shared menu."); });
        AddMenu("Close document", () => document.Close());
        var policies = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var veto = new CheckBox { Content = "Veto right-button opening" };
        veto.Checked += (_, _) => area.Veto = true; veto.Unchecked += (_, _) => area.Veto = false;
        var disabled = new CheckBox { Content = "Disable primary trigger" };
        disabled.Checked += (_, _) => first.IsEnabled = false; disabled.Unchecked += (_, _) => first.IsEnabled = true;
        var rtl = new CheckBox { Content = "Right-to-left" };
        rtl.Checked += (_, _) => panel.FlowDirection = FlowDirection.RightToLeft;
        rtl.Unchecked += (_, _) => panel.FlowDirection = FlowDirection.LeftToRight;
        policies.Children.Add(veto); policies.Children.Add(disabled); policies.Children.Add(rtl);
        panel.Children.Add(new TextBlock { Text = "Dropdown ownership and input contracts", FontSize = 20 });
        panel.Children.Add(new TextBlock { Text = "These triggers share one real MenuFlyout. Opening it from another trigger transfers the temporary context. Disabling or unloading its owner releases the scope. Application-owned row contexts remain untouched.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(triggers); panel.Children.Add(policies); panel.Children.Add(area);
        panel.Children.Add(new TextBox { AcceptsReturn = true, MinHeight = 100, Text = "Retained application editor.\nFloat and dock this document from either menu without replacing its content." });
        panel.Children.Add(log);
        menu.Opened += (_, _) => Record("Menu opened at " + (menu.Target == first ? "primary" : menu.Target == second ? "secondary" : "area"));
        menu.Closed += (_, _) => Record("Menu closed; temporary context released.");
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;
        void AddMenu(string label, Action action)
        {
            var item = new MenuFlyoutItem { Text = label, FontSize = 12, MinHeight = 22, Padding = new(8, 2, 8, 2) };
            item.Click += (_, _) => { Record(label + " via " + (item.DataContext?.ToString() ?? "no context")); action(); };
            menu.Items.Add(item);
        }
    }
    private sealed class GalleryDropDownArea(Action<string> record) : DropDownControlArea
    {
        internal bool Veto;
        protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
        { record("Right press: pointer " + e.PointerId); base.OnMouseRightButtonDown(e); }
        protected override void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e)
        {
            record(Veto ? "Right release vetoed." : "Right release accepted.");
            if (Veto) e.Handled = true;
            base.OnPreviewMouseRightButtonUp(e);
        }
    }
}
