using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    /// <summary>The IDE workspace's bottom tool pane; its NamedPaneLayoutStrategy places new tools there.</summary>
    internal const string WorkspaceToolsPane = "WorkspaceTools";
    private int _nextTool = 1;
    /// <summary>Adds a tool through the public AddToLayout path. The requested side
        /// is Right; the IDE workspace's layout update strategy redirects it to the
        /// named bottom pane, while the other samples use the default placement.</summary>
        private void AddTool()
    {
        if (Dock.AnchorablesSource != null)
        {
            Log("Tools of the MVVM sample come from its observable tool collection.");
            return;
        }

        var number = _nextTool++;
        var tool = ToolModel("tool-" + number, "Tool " + number, Editor("Tool " + number + "\n\nAdded with AnchorableShowStrategy.Right."));
        tool.AddToLayout(Dock, AnchorableShowStrategy.Right);
        tool.IsActive = true;
        Log($"Added {tool.Title} to {(tool.Parent as LayoutAnchorablePane)?.Name ?? "the default pane"}.");
    }

    /// <summary>A custom AnchorableContextMenu. Each opening sets the item
        /// DataContext to the tool's LayoutAnchorableItem, so the rows bind its commands.</summary>
        private MenuFlyout WorkspaceToolMenu()
    {
        var menu = new MenuFlyout();
        Add("Float", nameof(LayoutItem.FloatCommand));
        Add("Dock as document", nameof(LayoutItem.DockAsDocumentCommand));
        Add("Auto-hide", nameof(LayoutAnchorableItem.AutoHideCommand));
        Add("Hide", nameof(LayoutAnchorableItem.HideCommand));
        menu.Items.Add(new MenuFlyoutSeparator());
        var details = new MenuFlyoutItem
        {
            Text = "Log tool details"
        };
        details.Click += (sender, _) =>
        {
            if ((sender as FrameworkElement)?.DataContext is LayoutAnchorableItem { LayoutElement: { } tool })
                Log($"{tool.Title}: ContentId={tool.ContentId}, pane={(tool.Parent as LayoutAnchorablePane)?.Name ?? "(unnamed)"}");
        };
        menu.Items.Add(details);
        return menu;
        void Add(string text, string command)
        {
            var item = new MenuFlyoutItem
            {
                Text = text
            };
            item.SetBinding(MenuFlyoutItem.CommandProperty, new Binding { Path = new PropertyPath(command) });
            menu.Items.Add(item);
        }
    }
}
