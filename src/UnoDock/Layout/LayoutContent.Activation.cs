namespace UnoDock.Layout;

public abstract partial class LayoutContent
{
    private long _activationVersion;
    internal void PrepareActivation(bool value)
    {
        if (_active != value)
        {
            RaisePropertyChanging(nameof(IsActive));
        }
    }

    internal bool CommitActivation(bool value)
    {
        var old = _active;
        _active = value;
        _activationVersion++;
        return old;
    }

    internal void PublishActivation(bool old, bool value, LayoutMutation mutation)
    {
        if (old == value)
        {
            return;
        }

        var version = _activationVersion;
        var root = Root;
        bool Current() => _activationVersion == version && _active == value && ReferenceEquals(Root, root) && (!value || root is not LayoutRoot layout || ReferenceEquals(layout.ActiveContent, this));
        void Publish(Action action)
        {
            if (Current())
            {
                mutation.Run(action);
            }
        }

        Publish(() => RaisePropertyChanged(nameof(IsActive)));
        if (value)
        {
            Publish(() => IsSelected = true);
            Publish(() => LastActivationTimeStamp = DateTime.UtcNow);
        }

        Publish(() => OnIsActiveChanged(old, value));
        Publish(() => IsActiveChanged?.Invoke(this, EventArgs.Empty));
    }

    internal void SetActive(bool value)
    {
        if (Root is LayoutRoot root && (value || ReferenceEquals(root.ActiveContent, this)))
        {
            root.ActiveContent = value ? this : null;
            return;
        }

        if (_active == value)
        {
            return;
        }

        LayoutMutation.Execute(mutation =>
        {
            var version = _activationVersion;
            var parentVersion = ParentVersion;
            PrepareActivation(value);
            if (version != _activationVersion || parentVersion != ParentVersion)
            {
                return;
            }

            var old = CommitActivation(value);
            PublishActivation(old, value, mutation);
        }, Root as LayoutRoot);
    }

    internal void CommitLastFocused(bool value) => _lastFocused = value;
}
