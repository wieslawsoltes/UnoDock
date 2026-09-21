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
            // A native WinUI MenuFlyout is not a FrameworkElement. DataContext
            // belongs to its item tree rather than the flyout itself.
            SetMenuContext(custom.Items, item);
            return custom;
        }
        var menu = new MenuFlyout();
        Add("Activate", item.ActivateCommand); Add("Float", item.FloatCommand); Add("Dock as document", item.DockAsDocumentCommand);
        if (item is LayoutAnchorableItem tool)
        { Add("Dock", tool.DockCommand); Add("Auto-hide / Pin", tool.AutoHideCommand); Add("Hide", tool.HideCommand); }
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("Close", item.CloseCommand); Add("Close other documents", item.CloseAllButThisCommand); Add("Close all documents", item.CloseAllCommand);
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("New horizontal tab group", item.NewHorizontalTabGroupCommand); Add("New vertical tab group", item.NewVerticalTabGroupCommand);
        Add("Move to previous group", item.MoveToPreviousTabGroupCommand); Add("Move to next group", item.MoveToNextTabGroupCommand);
        return menu;
        void Add(string text, ICommand? command) { if (command != null) menu.Items.Add(new MenuFlyoutItem { Text = text, Command = command }); }
    }
    private static void SetMenuContext(IEnumerable<MenuFlyoutItemBase> items, LayoutItem context)
    {
        foreach (var item in items)
        {
            item.DataContext = context;
            if (item is MenuFlyoutSubItem submenu) SetMenuContext(submenu.Items, context);
        }
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
