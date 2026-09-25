namespace UnoDock.Layout;

/// <summary>Prepared while the old ownership tree is intact. Commit changes only
/// fields; all potentially throwing application notifications happen separately.</summary>
internal sealed class LayoutParentChange
{
    private readonly long _parentVersion;
    private readonly long _childrenVersion;
    private long _committedVersion = -1;

    internal LayoutParentChange(LayoutElement element, ILayoutContainer? parent)
    {
        Element = element;
        OldParent = element.Parent;
        NewParent = parent;
        OldRoot = element.Root;
        _parentVersion = element.ParentVersion;
        _childrenVersion = element.ChildrenVersion;
        Descendants = element.Descendents().OfType<LayoutElement>()
            .Select(child => (Child: child, Version: child.ParentVersion)).ToArray();
    }

    internal LayoutElement Element { get; }
    internal ILayoutContainer? OldParent { get; }
    internal ILayoutContainer? NewParent { get; }
    internal ILayoutRoot? OldRoot { get; }
    internal ILayoutRoot? NewRoot { get; private set; }
    internal (LayoutElement Child, long Version)[] Descendants { get; }

    internal bool IsPrepared => Element.ParentVersion == _parentVersion &&
        Element.ChildrenVersion == _childrenVersion && ReferenceEquals(Element.Parent, OldParent);

    internal bool IsCommitted => Element.ParentVersion == _committedVersion &&
        ReferenceEquals(Element.Parent, NewParent);

    internal void Commit()
    {
        // The caller has just verified its collection/slot and this preparation.
        // No callback, enumeration, allocation or virtual method is used here.
        Element.CommitParent(NewParent);
        _committedVersion = Element.ParentVersion;
        NewRoot = Element.Root;
    }

    internal void Publish(LayoutMutation mutation) => Element.PublishParentChange(this, mutation);
}
