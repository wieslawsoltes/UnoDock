namespace UnoDock.Layout;

internal static class LayoutTree
{
    internal static void Validate(ILayoutContainer owner, ILayoutElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is not LayoutElement)
        {
            throw new ArgumentException("A child must derive from LayoutElement.", nameof(item));
        }

        for (ILayoutElement? current = owner; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, item))
            {
                throw new InvalidOperationException("A layout must be an acyclic ownership tree.");
            }
        }
    }

    internal static void Attach(ILayoutContainer parent, LayoutElement item)
    {
        Validate(parent, item);
        switch (parent)
        {
            case ILayoutGroup group:
                group.InsertChildAt(group.ChildrenCount, item);
                break;
            case LayoutRoot root when item is LayoutFloatingWindow floating:
                root.FloatingWindows.Add(floating);
                break;
            case LayoutRoot root when item is LayoutAnchorable anchorable:
                root.Hidden.Add(anchorable);
                break;
            case LayoutRoot root when item is LayoutPanel panel:
                root.RootPanel = panel;
                break;
            case LayoutDocumentFloatingWindow window when item is LayoutDocument document:
                window.RootDocument = document;
                break;
            case LayoutAnchorableFloatingWindow window when item is LayoutAnchorablePaneGroup panel:
                window.RootPanel = panel;
                break;
            default:
                throw new ArgumentException($"Cannot attach {item.GetType().Name} to {parent.GetType().Name}.");
        }
    }

    internal static void ReplaceSlot<T>(ILayoutContainer owner, ref T? field, T? value, string property)
        where T : LayoutElement
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        if (value != null)
        {
            Validate(owner, value);
        }

        var ownerElement = (LayoutElement)owner;
        var root = owner.Root as LayoutRoot;
        using var mutation = new LayoutMutation(root, value?.Root as LayoutRoot);
        try
        {
            var old = field;
            var oldVersion = old?.ParentVersion;
            value?.Parent?.RemoveChild(value);
            // A detach callback may transfer the incoming child or replace this
            // destination slot. Neither operation authorizes reclaiming it.
            if (!ReferenceEquals(field, old) || old?.ParentVersion != oldVersion || value?.Parent != null)
            {
                return;
            }

            var version = ownerElement.ChildrenVersion;
            var remove = old?.PrepareParentChange(null);
            if (ownerElement.ChildrenVersion != version || !ReferenceEquals(field, old) ||
                remove is { IsPrepared: false })
            {
                return;
            }

            var insert = value?.PrepareParentChange(owner);
            if (ownerElement.ChildrenVersion != version || !ReferenceEquals(field, old) ||
                remove is { IsPrepared: false } || insert is { IsPrepared: false })
            {
                return;
            }

            if (value != null)
            {
                Validate(owner, value);
            }

            // Publish the slot and both parent fields as one callback-free commit.
            field = value;
            ownerElement.ChildrenMutated();
            remove?.Commit();
            insert?.Commit();

            mutation.Run(() => ownerElement.Notify(property));
            remove?.Publish(mutation);
            insert?.Publish(mutation);
            if (old != null && root != null)
            {
                mutation.Run(() => root.Removed(old));
            }

            if (value != null && root != null && insert!.IsCommitted)
            {
                mutation.Run(() => root.Added(value));
            }

            if (root != null)
            {
                mutation.Run(root.Invalidate);
            }
        }
        catch (Exception error)
        {
            mutation.Add(error);
        }
    }
}
