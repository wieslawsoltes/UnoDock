using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Controls;

namespace Xceed.Wpf.AvalonDock.Internal;

internal static class DockVisuals
{
    internal static Brush Brush(FrameworkElement element, string key, string fallback)
    {
        for (FrameworkElement? cursor = element; cursor != null; cursor = VisualTreeHelper.GetParent(cursor) as FrameworkElement)
            if (cursor.Resources.TryGetValue(key, out var local) && local is Brush b) return b;
        if (Application.Current?.Resources.TryGetValue(fallback, out var app) == true && app is Brush value) return value;
        return new SolidColorBrush(fallback.Contains("Foreground", StringComparison.Ordinal) ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.Transparent);
    }
    internal static Button Button(string label, Action action, string? automationName = null)
    {
        var button = new Button { Content = label, Padding = new Thickness(7, 3, 7, 3), MinWidth = 28, MinHeight = 28 };
        AutomationProperties.SetName(button, automationName ?? label); ToolTipService.SetToolTip(button, automationName ?? label);
        button.Click += (_, _) => action(); return button;
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
    internal static void CloseOrHide(LayoutContent content)
    { if (content is LayoutAnchorable a && a.CanHide) a.Hide(); else content.Close(); }
    internal static void SetName(DependencyObject element, string name) => AutomationProperties.SetName(element, name);
}

internal interface IRefreshableLayoutControl : ILayoutControl { void Update(DockSurface surface); }
