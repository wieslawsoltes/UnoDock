using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;
/// <summary>Scopes a flyout's temporary context to one opening. Application-owned local
/// values and bindings are never replaced; values changed while open survive cleanup.</summary>
internal static class MenuContext
{
    private sealed class State
    {
        internal readonly Dictionary<MenuFlyoutItemBase, object?> Assigned = new(ReferenceEqualityComparer.Instance);
        internal bool Hooked, Updating, Pending, ApplyPending, CleaningAfterFailure;
        internal object? Context;
    }

    private static readonly ConditionalWeakTable<MenuFlyout, State> States = new();
    internal static readonly DependencyProperty ModelProperty = DependencyProperty.RegisterAttached("ContextModel", typeof(LayoutContent), typeof(MenuContext), new PropertyMetadata(null));
    internal static void SetTarget(DependencyObject target, LayoutContent? model) => target.SetValue(ModelProperty, model);
    internal static void PrepareShared(MenuFlyout menu)
    {
        var state = States.GetOrCreateValue(menu);
        if (state.Hooked)
            return;
        state.Hooked = true;
        menu.Opening += (_, _) =>
        {
            Clear(menu);
            for (DependencyObject? target = menu.Target; target != null; target = VisualTreeHelper.GetParent(target))
            {
                if (target.GetValue(ModelProperty)is not LayoutContent model || model.Root?.Manager is not { } manager)
                    continue;
                Apply(menu, manager.GetLayoutItemFromModel(model));
                break;
            }
        };
        menu.Closed += (_, _) => Clear(menu);
    }

    internal static void Apply(MenuFlyout menu, object? context) => Request(menu, States.GetOrCreateValue(menu), true, context);
    internal static void Clear(MenuFlyout menu)
    {
        if (States.TryGetValue(menu, out var state))
            Request(menu, state, false, null);
    }

    private static void Request(MenuFlyout menu, State state, bool apply, object? context)
    {
        // A callback failure terminates this scope. Cleanup must not recursively
        // reestablish the same failing scope through DataContextChanged.
        if (state.CleaningAfterFailure)
            return;
        state.Pending = true;
        state.ApplyPending = apply;
        state.Context = context;
        if (state.Updating)
            return;
        state.Updating = true;
        try
        {
            var budget = 64;
            while (state.Pending)
            {
                if (--budget == 0)
                    throw new InvalidOperationException("Menu context callbacks did not converge.");
                var assign = state.ApplyPending;
                var value = state.Context;
                state.Pending = false;
                state.Context = null;
                // Remove ownership before invoking user code. A reentrant request
                // is drained next, not allowed to mutate an enumerated collection.
                foreach (var(item, assigned)in state.Assigned.ToArray())
                {
                    state.Assigned.Remove(item);
                    if (ReferenceEquals(item.ReadLocalValue(FrameworkElement.DataContextProperty), assigned))
                        item.ClearValue(FrameworkElement.DataContextProperty);
                    if (state.Pending)
                        break;
                }

                if (state.Pending || !assign)
                    continue;
                var stack = new Stack<MenuFlyoutItemBase>(menu.Items.Reverse());
                while (stack.TryPop(out var item))
                {
                    if (ReferenceEquals(item.ReadLocalValue(FrameworkElement.DataContextProperty), DependencyProperty.UnsetValue))
                    {
                        // Register ownership before SetValue: it can reenter or
                        // throw after committing the dependency property's value.
                        state.Assigned[item] = value;
                        item.DataContext = value;
                        if (state.Pending)
                            break;
                    }

                    if (item is MenuFlyoutSubItem sub)
                        foreach (var child in sub.Items.Reverse())
                            stack.Push(child);
                }
            }
        }
        catch (Exception original)
        {
            // Best-effort cleanup of every assignment we still own. Never erase
            // an application's local value/binding, even on the exception path.
            state.CleaningAfterFailure = true;
            List<Exception>? failures = null;
            foreach (var(item, assigned)in state.Assigned.ToArray())
            {
                state.Assigned.Remove(item);
                try
                {
                    if (ReferenceEquals(item.ReadLocalValue(FrameworkElement.DataContextProperty), assigned))
                        item.ClearValue(FrameworkElement.DataContextProperty);
                }
                catch (Exception cleanup)
                {
                    (failures ??= []).Add(cleanup);
                }
            }

            if (failures != null)
            {
                failures.Insert(0, original);
                throw new AggregateException("Menu context assignment and cleanup failed.", failures);
            }

            throw;
        }
        finally
        {
            state.Pending = false;
            state.Context = null;
            state.CleaningAfterFailure = false;
            state.Updating = false;
        }
    }
}
