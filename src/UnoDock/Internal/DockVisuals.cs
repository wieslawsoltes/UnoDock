using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Layout;
using UnoDock.Controls;

namespace UnoDock.Internal;

internal static class DockVisuals
{
    internal static Brush Brush(FrameworkElement element, string key, string fallback)
    {
        for (FrameworkElement? cursor = element; cursor != null; cursor = VisualTreeHelper.GetParent(cursor) as FrameworkElement)
            if (cursor.Resources.TryGetValue(key, out var local) && local is Brush b)
                return b;
        if (Application.Current?.Resources.TryGetValue(fallback, out var app) == true && app is Brush value)
            return value;
        return new SolidColorBrush(fallback.Contains("Foreground", StringComparison.Ordinal) ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.Transparent);
    }

    internal static Button Button(string label, Action action, string? automationName = null)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(7, 3, 7, 3),
            MinWidth = 28,
            MinHeight = 28
        };
        AutomationProperties.SetName(button, automationName ?? label);
        ToolTipService.SetToolTip(button, automationName ?? label);
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>Open a menu from a ▾ chrome button: below the button, aligned to
        /// its leading edge, like a drop-down list.</summary>
        internal static void ShowBelow(FlyoutBase flyout, FrameworkElement anchor)
    {
        if (anchor is DockChromeButton button)
        {
            button.IsMenuOpen = true;
            void Closed(object? sender, object e)
            {
                flyout.Closed -= Closed;
                button.IsMenuOpen = false;
            }

            flyout.Closed += Closed;
        }

        flyout.ShowAt(anchor, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    internal static MenuFlyout Menu(DockingManager manager, LayoutContent model)
    {
        var item = manager.GetLayoutItemFromModel(model);
        var custom = model is LayoutAnchorable ? manager.AnchorableContextMenu : manager.DocumentContextMenu;
        if (custom != null)
        {
            item.SuspendDefaultContextMenu();
            MenuContext.PrepareShared(custom);
            return custom;
        }

        return item.GetDefaultContextMenu(manager);
    }

    internal static DockRect Bounds(FrameworkElement element, UIElement relative)
    {
        var rect = element.TransformToVisual(relative).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>The close button of a tab or title: closable content closes, other
        /// tools hide; both through the item's commands so application handlers run.</summary>
        internal static void CloseOrHide(LayoutContent content)
    {
        if (content is LayoutAnchorable tool && !tool.CanClose)
        {
            if (tool.CanHide)
                LayoutItem.Execute(tool, item => (item as LayoutAnchorableItem)?.HideCommand, () => tool.Hide());
        }
        else
            LayoutItem.Execute(content, item => item.CloseCommand, content.Close);
    }

    internal static void ToggleAutoHide(LayoutAnchorable tool) => LayoutItem.Execute(tool, item => (item as LayoutAnchorableItem)?.AutoHideCommand, tool.ToggleAutoHide);
    internal static void Float(LayoutContent content) => LayoutItem.Execute(content, item => item.FloatCommand, content.Float);
    internal static void Dock(LayoutContent content) => LayoutItem.Execute(content, item => item is LayoutAnchorableItem tool ? tool.DockCommand : item.DockAsDocumentCommand, content.Dock);
    internal static void SetName(DependencyObject element, string name) => AutomationProperties.SetName(element, name);
}
