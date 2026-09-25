namespace UnoDock.Controls;
/// <summary>Retains the original public spelling. Logical traversal follows ownership,
/// including unrealized content; visual traversal follows only realized visuals.</summary>
public static class Extentions
{
    public static T? FindVisualAncestor<T>(this DependencyObject dependencyObject)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(dependencyObject);
        for (var parent = VisualTreeHelper.GetParent(dependencyObject); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T value)
                return value;
        return null;
    }

    public static T? FindLogicalAncestor<T>(this DependencyObject dependencyObject)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(dependencyObject);
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        for (var parent = LogicalParent(dependencyObject); parent != null && seen.Add(parent); parent = LogicalParent(parent))
            if (parent is T value)
                return value;
        return null;
    }

    public static IEnumerable<DependencyObject> FindLogicalAncestorsAndSelf(this DependencyObject self)
    {
        ArgumentNullException.ThrowIfNull(self);
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        for (DependencyObject? item = self; item != null && seen.Add(item); item = LogicalParent(item))
            yield return item;
    }

    public static DependencyObject FindVisualTreeRoot(this DependencyObject initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        while (VisualTreeHelper.GetParent(initial)is { } parent)
            initial = parent;
        return initial;
    }

    public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj)
        where T : DependencyObject => Descendants<T>(depObj, false);
    public static IEnumerable<T> FindLogicalChildren<T>(this DependencyObject depObj)
        where T : DependencyObject => Descendants<T>(depObj, true);
    private static DependencyObject? LogicalParent(DependencyObject item) => (item as FrameworkElement)?.Parent ?? VisualTreeHelper.GetParent(item);
    private static IEnumerable<T> Descendants<T>(DependencyObject root, bool logical)
        where T : DependencyObject
    {
        ArgumentNullException.ThrowIfNull(root);
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance)
        {
            root
        };
        var stack = new Stack<DependencyObject>();
        PushChildren(root);
        while (stack.TryPop(out var item))
        {
            if (!seen.Add(item))
                continue;
            if (item is T value)
                yield return value;
            PushChildren(item);
        }

        void PushChildren(DependencyObject item)
        {
            if (logical)
            {
                switch (item)
                {
                    case Panel panel:
                        for (var i = panel.Children.Count - 1; i >= 0; i--)
                            stack.Push(panel.Children[i]);
                        return;
                    case Border { Child: { } child }:
                        stack.Push(child);
                        return;
                    case ContentControl { Content: DependencyObject child }:
                        stack.Push(child);
                        return;
                    case ContentPresenter { Content: DependencyObject child }:
                        stack.Push(child);
                        return;
                    case ItemsControl items:
                        for (var i = items.Items.Count - 1; i >= 0; i--)
                            if (items.Items[i] is DependencyObject child)
                                stack.Push(child);
                        return;
                }
            }

            for (var i = VisualTreeHelper.GetChildrenCount(item) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(item, i));
        }
    }
}
