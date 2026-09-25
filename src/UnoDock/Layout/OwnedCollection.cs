namespace UnoDock.Layout;
/// <summary>Storage and parent fields are committed before notifications. An
/// observer exception never leaves half an ownership relationship published.</summary>
internal sealed class OwnedCollection<T>(ILayoutContainer owner, Action changed) : ObservableCollection<T> where T : class, ILayoutElement
{
    private LayoutElement Owner => (LayoutElement)owner;

    private void Check(int index, T item, bool replacing)
    {
        if (index < 0 || index > Count || replacing && index == Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        LayoutTree.Validate(owner, item);
        if (this.Any(child => ReferenceEquals(child, item)) && (!replacing || !ReferenceEquals(this[index], item)))
        {
            throw new InvalidOperationException("A child cannot occur twice in its container.");
        }
    }

    protected override void InsertItem(int index, T item)
    {
        CheckReentrancy();
        Check(index, item, false);
        var root = owner.Root as LayoutRoot;
        LayoutMutation.Execute(mutation =>
        {
            var version = Owner.ChildrenVersion;
            if (!LayoutTree.DetachForTransfer((LayoutElement)(ILayoutElement)item) || Owner.ChildrenVersion != version)
            {
                return;
            }

            Check(index, item, false);
            var element = (LayoutElement)(ILayoutElement)item;
            var change = element.PrepareParentChange(owner);
            if (!change.IsPrepared || Owner.ChildrenVersion != version)
            {
                return;
            }

            LayoutTree.Validate(owner, item);
            Items.Insert(index, item);
            change.Commit();
            Owner.ChildrenMutated();
            item.PropertyChanged += ChildChanged;
            PublishCollection(mutation, new(NotifyCollectionChangedAction.Add, item, index), true);
            change.Publish(mutation);
            if (root != null && change.IsCommitted)
            {
                mutation.Run(() => root.Added(element));
            }

            Complete(mutation, root);
        }, root, item.Root as LayoutRoot);
    }

    protected override void RemoveItem(int index)
    {
        CheckReentrancy();
        var item = this[index];
        var root = owner.Root as LayoutRoot;
        LayoutMutation.Execute(mutation =>
        {
            var version = Owner.ChildrenVersion;
            var element = (LayoutElement)(ILayoutElement)item;
            var change = element.PrepareParentChange(null);
            if (!change.IsPrepared || Owner.ChildrenVersion != version)
            {
                return;
            }

            Items.RemoveAt(index);
            change.Commit();
            Owner.ChildrenMutated();
            item.PropertyChanged -= ChildChanged;
            PublishCollection(mutation, new(NotifyCollectionChangedAction.Remove, item, index), true);
            change.Publish(mutation);
            if (root != null)
            {
                mutation.Run(() => root.Removed(element));
            }

            Complete(mutation, root);
        }, root);
    }

    protected override void SetItem(int index, T item)
    {
        CheckReentrancy();
        Check(index, item, true);
        var old = this[index];
        if (ReferenceEquals(old, item))
        {
            return;
        }

        var root = owner.Root as LayoutRoot;
        LayoutMutation.Execute(mutation =>
        {
            var version = Owner.ChildrenVersion;
            if (!LayoutTree.DetachForTransfer((LayoutElement)(ILayoutElement)item) || Owner.ChildrenVersion != version)
            {
                return;
            }

            Check(index, item, true);
            var previous = (LayoutElement)(ILayoutElement)old;
            var element = (LayoutElement)(ILayoutElement)item;
            var remove = previous.PrepareParentChange(null);
            if (!remove.IsPrepared || Owner.ChildrenVersion != version)
            {
                return;
            }

            var insert = element.PrepareParentChange(owner);
            if (!remove.IsPrepared || !insert.IsPrepared || Owner.ChildrenVersion != version)
            {
                return;
            }

            LayoutTree.Validate(owner, item);
            Items[index] = item;
            remove.Commit();
            insert.Commit();
            Owner.ChildrenMutated();
            old.PropertyChanged -= ChildChanged;
            item.PropertyChanged += ChildChanged;
            PublishCollection(mutation, new(NotifyCollectionChangedAction.Replace, item, old, index), false);
            remove.Publish(mutation);
            insert.Publish(mutation);
            if (root != null)
            {
                mutation.Run(() => root.Removed(previous));
                if (insert.IsCommitted)
                {
                    mutation.Run(() => root.Added(element));
                }
            }

            Complete(mutation, root);
        }, root, item.Root as LayoutRoot);
    }

    protected override void ClearItems()
    {
        CheckReentrancy();
        var root = owner.Root as LayoutRoot;
        LayoutMutation.Execute(mutation =>
        {
            var version = Owner.ChildrenVersion;
            var old = this.ToArray();
            var changes = new List<LayoutParentChange>(old.Length);
            foreach (var item in old)
            {
                var change = ((LayoutElement)(ILayoutElement)item).PrepareParentChange(null);
                changes.Add(change);
                if (!change.IsPrepared || Owner.ChildrenVersion != version)
                {
                    return;
                }
            }

            if (changes.Any(change => !change.IsPrepared))
            {
                return;
            }

            Items.Clear();
            foreach (var change in changes)
            {
                change.Commit();
            }

            Owner.ChildrenMutated();
            foreach (var item in old)
            {
                item.PropertyChanged -= ChildChanged;
            }

            PublishCollection(mutation, new(NotifyCollectionChangedAction.Reset), true);
            foreach (var change in changes)
            {
                change.Publish(mutation);
                if (root != null)
                {
                    mutation.Run(() => root.Removed(change.Element));
                }
            }

            Complete(mutation, root);
        }, root);
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        CheckReentrancy();
        if ((uint)oldIndex >= (uint)Count || (uint)newIndex >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(oldIndex < 0 || oldIndex >= Count ? nameof(oldIndex) : nameof(newIndex));
        }

        var root = owner.Root as LayoutRoot;
        LayoutMutation.Execute(mutation =>
        {
            var item = this[oldIndex];
            Items.RemoveAt(oldIndex);
            Items.Insert(newIndex, item);
            Owner.ChildrenMutated();
            PublishCollection(mutation, new(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex), false);
            Complete(mutation, root);
        }, root);
    }

    private void PublishCollection(LayoutMutation mutation, NotifyCollectionChangedEventArgs args, bool countChanged)
    {
        if (countChanged)
        {
            mutation.Run(() => OnPropertyChanged(new(nameof(Count))));
        }

        mutation.Run(() => OnPropertyChanged(new("Item[]")));
        mutation.Run(() => OnCollectionChanged(args));
    }

    private void Complete(LayoutMutation mutation, LayoutRoot? root)
    {
        mutation.Run(changed);
        if (root != null)
        {
            mutation.Run(root.Invalidate);
        }
    }

    private void ChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutContent.IsSelected) && sender is LayoutContent { IsSelected: true } selected && owner is ILayoutContentSelector selector && !ReferenceEquals(selector.SelectedContent, selected))
        {
            selector.SelectedContentIndex = selector.IndexOf(selected);
        }

        if (e.PropertyName is "IsVisible" or "IsHidden" or "IsAutoHidden")
        {
            (owner as ILayoutElementWithVisibility)?.ComputeVisibility();
        }

        (owner.Root as LayoutRoot)?.Invalidate();
    }
}
