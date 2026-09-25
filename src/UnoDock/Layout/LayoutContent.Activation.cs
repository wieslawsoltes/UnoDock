namespace UnoDock.Layout;

public abstract partial class LayoutContent
{
    internal long ActiveVersion { get; private set; }

    internal LayoutActiveChange PrepareActiveChange(bool value)
    {
        var change = new LayoutActiveChange(this, value);
        if (change.OldValue != value) RaisePropertyChanging(nameof(IsActive));
        return change;
    }

    internal void CommitActiveChange(LayoutActiveChange change)
    {
        _active = change.NewValue;
        ActiveVersion++;
    }

    internal void PublishActiveChange(LayoutActiveChange change, Func<bool> ownsRequest, LayoutMutationScope notifications)
    {
        bool Current() => change.IsCommitted && ownsRequest();
        if (!Current()) return;
        notifications.Run(() => RaisePropertyChanged(nameof(IsActive)));
        if (!Current()) return;
        if (change.NewValue)
        {
            notifications.Run(() => IsSelected = true);
            if (!Current()) return;
            notifications.Run(() => LastActivationTimeStamp = DateTime.UtcNow);
            if (!Current()) return;
        }
        notifications.Run(() => OnIsActiveChanged(change.OldValue, change.NewValue));
        if (Current()) notifications.Run(() => IsActiveChanged?.Invoke(this, EventArgs.Empty));
    }

    internal void SetActive(bool value)
    {
        if (Root is LayoutRoot root && (value || ReferenceEquals(root.ActiveContent, this)))
        {
            root.ActiveContent = value ? this : null;
            return;
        }
        if (_active == value) return;
        using var notifications = new LayoutMutationScope(Root as LayoutRoot);
        try
        {
            var parentVersion = ParentVersion;
            var change = PrepareActiveChange(value);
            if (!change.IsPrepared || ParentVersion != parentVersion) return;
            change.Commit();
            change.Publish(() => ParentVersion == parentVersion, notifications);
        }
        catch (Exception error) { notifications.Record(error); }
    }
}
