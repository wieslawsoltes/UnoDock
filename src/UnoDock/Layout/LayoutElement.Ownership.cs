namespace UnoDock.Layout;

public abstract partial class LayoutElement
{
    internal long ParentVersion { get; private set; }
    internal long ChildrenVersion { get; private set; }

    internal void ChildrenMutated() => ChildrenVersion++;

    internal LayoutParentChange PrepareParentChange(ILayoutContainer? parent)
    {
        var change = new LayoutParentChange(this, parent);
        OnParentChanging(change.OldParent, parent);
        if (change.IsPrepared)
        {
            RaisePropertyChanging(nameof(Parent));
        }

        return change;
    }

    internal void CommitParent(ILayoutContainer? parent)
    {
        _parent = parent;
        ParentVersion++;
    }

    internal void PublishParentChange(LayoutParentChange change, LayoutMutation mutation)
    {
        void Publish(Action action)
        {
            if (change.IsCommitted)
            {
                mutation.Run(action);
            }
        }

        Publish(() => OnParentChanged(change.OldParent, change.NewParent));
        Publish(() => RaisePropertyChanged(nameof(Parent)));
        if (!ReferenceEquals(change.OldRoot, change.NewRoot))
        {
            Publish(() => OnRootChanged(change.OldRoot, change.NewRoot));
            Publish(() => RaisePropertyChanged(nameof(Root)));
            foreach (var (child, version) in change.Descendants)
            {
                bool Current() => change.IsCommitted && child.ParentVersion == version &&
                    ReferenceEquals(child.Root, change.NewRoot);
                if (Current())
                {
                    mutation.Run(() => child.OnRootChanged(change.OldRoot, change.NewRoot));
                }

                if (Current())
                {
                    mutation.Run(() => child.Notify(nameof(Root)));
                }

                if (Current() && child is LayoutContent content)
                {
                    mutation.Run(content.RefreshPlacement);
                }
            }
        }

        if (this is LayoutContent ownContent)
        {
            Publish(ownContent.RefreshPlacement);
        }
    }
}
