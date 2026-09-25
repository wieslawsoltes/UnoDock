namespace UnoDock.Layout;

/// <summary>Captures a pre-notification generation; an ABA parent write revokes it.</summary>
internal sealed class LayoutParentChange(LayoutElement element, ILayoutContainer? parent)
{
    internal readonly LayoutElement Element = element;
    internal readonly ILayoutContainer? OldParent = element.Parent;
    internal readonly ILayoutContainer? NewParent = parent;
    internal readonly ILayoutRoot? OldRoot = element.Root;
    internal readonly long Version = element.ParentVersion;
    internal readonly LayoutElement[] Descendants = element.Descendents().OfType<LayoutElement>().ToArray();
    internal ILayoutRoot? NewRoot;
    internal bool IsPrepared => Element.ParentVersion == Version && ReferenceEquals(Element.Parent, OldParent);
    internal bool IsCommitted => Element.ParentVersion == Version + 1 && ReferenceEquals(Element.Parent, NewParent) && ReferenceEquals(Element.Root, NewRoot);

    internal void Commit() => Element.CommitParentChange(this);
    internal void Publish(LayoutMutationScope notifications) => Element.PublishParentChange(this, notifications);
}
