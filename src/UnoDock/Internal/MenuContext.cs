using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Internal;

/// <summary>Scopes a flyout's temporary context to one opening. Application-owned local
/// values and bindings are never replaced; values changed while open survive cleanup.</summary>
internal static class MenuContext
{
    private sealed class State
    {
        internal readonly List<(MenuFlyoutItemBase Item, object? Value)> Assigned = [];
        internal bool Hooked;
    }
    private static readonly ConditionalWeakTable<MenuFlyout, State> States = new();
    internal static readonly DependencyProperty ModelProperty = DependencyProperty.RegisterAttached(
        "ContextModel", typeof(LayoutContent), typeof(MenuContext), new PropertyMetadata(null));
    internal static void SetTarget(DependencyObject target, LayoutContent? model) => target.SetValue(ModelProperty, model);
    internal static void PrepareShared(MenuFlyout menu)
    {
        var state = States.GetOrCreateValue(menu);
        if (state.Hooked) return;
        state.Hooked = true;
        menu.Opening += (_, _) =>
        {
            Clear(menu);
            for (DependencyObject? target = menu.Target; target != null; target = VisualTreeHelper.GetParent(target))
            {
                if (target.GetValue(ModelProperty) is not LayoutContent model || model.Root?.Manager is not { } manager) continue;
                Apply(menu, manager.GetLayoutItemFromModel(model));
                break;
            }
        };
        menu.Closed += (_, _) => Clear(menu);
    }
    internal static void Apply(MenuFlyout menu, object? context)
    {
        Clear(menu);
        var state = States.GetOrCreateValue(menu);
        var stack = new Stack<MenuFlyoutItemBase>(menu.Items.Reverse());
        while (stack.TryPop(out var item))
        {
            if (ReferenceEquals(item.ReadLocalValue(FrameworkElement.DataContextProperty), DependencyProperty.UnsetValue))
            {
                item.DataContext = context;
                state.Assigned.Add((item, context));
            }
            if (item is MenuFlyoutSubItem sub)
                foreach (var child in sub.Items.Reverse()) stack.Push(child);
        }
    }
    internal static void Clear(MenuFlyout menu)
    {
        if (!States.TryGetValue(menu, out var state)) return;
        foreach (var (item, context) in state.Assigned)
            if (ReferenceEquals(item.ReadLocalValue(FrameworkElement.DataContextProperty), context)) item.ClearValue(FrameworkElement.DataContextProperty);
        state.Assigned.Clear();
    }
}
