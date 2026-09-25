namespace UnoDock.Layout;

internal sealed class LayoutActiveChange(LayoutContent content, bool value)
{
    internal readonly LayoutContent Content = content;
    internal readonly bool OldValue = content.IsActive;
    internal readonly bool NewValue = value;
    internal readonly long Version = content.ActiveVersion;
    private readonly long _parentVersion = content.ParentVersion;
    private readonly ILayoutRoot? _root = content.Root;
    private bool SameOwner => Content.ParentVersion == _parentVersion && ReferenceEquals(Content.Root, _root);
    internal bool IsPrepared => SameOwner && Content.ActiveVersion == Version && Content.IsActive == OldValue;
    internal bool IsCommitted => SameOwner && Content.ActiveVersion == Version + 1 && Content.IsActive == NewValue;
    internal void Commit() => Content.CommitActiveChange(this);
    internal void Publish(Func<bool> current, LayoutMutationScope notifications) => Content.PublishActiveChange(this, current, notifications);
}
