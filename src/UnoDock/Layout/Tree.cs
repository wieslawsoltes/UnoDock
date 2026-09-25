using System.Collections.Specialized;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

public abstract partial class LayoutElement : DependencyObject, ILayoutElement
{
    /// <summary>Writes a bounded, cycle-safe diagnostic snapshot without evaluating user content.</summary>
    public virtual void ConsoleDump(int tab) => LayoutDiagnostics.Write(this, Console.Out, tab);
    private ILayoutContainer? _parent;
    internal long ParentVersion { get; private set; }
    internal string SerializationId { get; set; } = "";

    [System.Xml.Serialization.XmlIgnore]
    public ILayoutContainer? Parent
    {
        get => _parent;
        set
        {
            if (ReferenceEquals(_parent, value)) return;
            if (value == null) { _parent?.RemoveChild(this); return; }
            LayoutTree.Attach(value, this);
        }
    }

    public ILayoutRoot? Root => this is LayoutRoot root ? root : _parent?.Root;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;
    protected virtual void RaisePropertyChanging(string propertyName) => PropertyChanging?.Invoke(this, new(propertyName));

    protected virtual void RaisePropertyChanged(string propertyName)
    {
        using var notifications = new LayoutMutationScope();
        notifications.Run(() => PropertyChanged?.Invoke(this, new(propertyName)));
        notifications.Run(() => (Root as LayoutRoot)?.Invalidate());
    }

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        RaisePropertyChanging(name);
        field = value;
        RaisePropertyChanged(name);
        return true;
    }

    internal void Notify(string name) => RaisePropertyChanged(name);

    internal LayoutParentChange PrepareParentChange(ILayoutContainer? value)
    {
        var change = new LayoutParentChange(this, value);
        OnParentChanging(change.OldParent, value);
        if (change.IsPrepared) RaisePropertyChanging(nameof(Parent));
        return change;
    }

    // Only a container's callback-free commit section may call this. Membership
    // and every affected Parent are installed before publishing any notification.
    internal void CommitParentChange(LayoutParentChange change)
    {
        _parent = change.NewParent;
        ParentVersion++;
        change.NewRoot = Root;
    }

    internal void PublishParentChange(LayoutParentChange change, LayoutMutationScope notifications)
    {
        if (!change.IsCommitted) return;
        notifications.Run(() => OnParentChanged(change.OldParent, change.NewParent));
        if (!change.IsCommitted) return;
        notifications.Run(() => RaisePropertyChanged(nameof(Parent)));
        if (!change.IsCommitted) return;
        if (!ReferenceEquals(change.OldRoot, change.NewRoot))
        {
            notifications.Run(() => OnRootChanged(change.OldRoot, change.NewRoot));
            if (!change.IsCommitted) return;
            notifications.Run(() => RaisePropertyChanged(nameof(Root)));
            foreach (var child in change.Descendants)
            {
                if (!change.IsCommitted) return;
                if (!ReferenceEquals(child.Root, change.NewRoot) || !IsDescendant(child)) continue;
                var version = child.ParentVersion;
                notifications.Run(() => child.OnRootChanged(change.OldRoot, change.NewRoot));
                if (child.ParentVersion != version || !IsDescendant(child)) continue;
                notifications.Run(() => child.Notify(nameof(Root)));
                if (child is LayoutContent content && child.ParentVersion == version && IsDescendant(child))
                    notifications.Run(content.RefreshPlacement);
            }
        }
        if (change.IsCommitted && this is LayoutContent current) notifications.Run(current.RefreshPlacement);

        bool IsDescendant(LayoutElement child)
        {
            for (var parent = child.Parent; parent != null; parent = parent.Parent)
                if (ReferenceEquals(parent, this)) return true;
            return false;
        }
    }

    protected virtual void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue) { }
    protected virtual void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) { }
    protected virtual void OnRootChanged(ILayoutRoot? oldRoot, ILayoutRoot? newRoot) { }
}

internal static class LayoutTree
{
    internal static void Validate(ILayoutContainer owner, ILayoutElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is not LayoutElement) throw new ArgumentException("A child must derive from LayoutElement.", nameof(item));
        for (ILayoutElement? p = owner; p != null; p = p.Parent)
            if (ReferenceEquals(p, item)) throw new InvalidOperationException("A layout must be an acyclic ownership tree.");
    }

    internal static void Attach(ILayoutContainer parent, LayoutElement item)
    {
        Validate(parent, item);
        switch (parent)
        {
            case ILayoutGroup group: group.InsertChildAt(group.ChildrenCount, item); break;
            case LayoutRoot root when item is LayoutFloatingWindow f: root.FloatingWindows.Add(f); break;
            case LayoutRoot root when item is LayoutAnchorable a: root.Hidden.Add(a); break;
            case LayoutRoot root when item is LayoutPanel panel: root.RootPanel = panel; break;
            case LayoutDocumentFloatingWindow window when item is LayoutDocument doc: window.RootDocument = doc; break;
            case LayoutAnchorableFloatingWindow window when item is LayoutAnchorablePaneGroup panel: window.RootPanel = panel; break;
            default: throw new ArgumentException($"Cannot attach {item.GetType().Name} to {parent.GetType().Name}.");
        }
    }

    internal static bool DetachForTransfer(LayoutElement item)
    {
        var previous = item.Parent;
        var version = item.ParentVersion;
        previous?.RemoveChild(item);
        // A callback may have transferred the child, even away and back again.
        // Do not steal it or overwrite that callback's completed operation.
        return item.Parent == null && item.ParentVersion == version + (previous == null ? 0 : 1);
    }

    internal static void ReplaceSlot<T>(ILayoutContainer owner, ref T? field, T? value, string property) where T : LayoutElement
    {
        if (ReferenceEquals(field, value)) return;
        if (value != null) Validate(owner, value);
        var old = field;
        var root = owner.Root as LayoutRoot;
        using var notifications = new LayoutMutationScope(root, value?.Root as LayoutRoot);
        try
        {
            var oldVersion = old?.ParentVersion;
            if (value != null && !DetachForTransfer(value)) return;
            if (!ReferenceEquals(field, old) || old?.ParentVersion != oldVersion) return;
            var detach = old?.PrepareParentChange(null);
            if (!ReferenceEquals(field, old) || detach?.IsPrepared == false) return;
            var attach = value?.PrepareParentChange(owner);
            if (!ReferenceEquals(field, old) || detach?.IsPrepared == false || attach?.IsPrepared == false) return;
            if (value != null) Validate(owner, value);

            field = value;
            detach?.Commit();
            attach?.Commit();
            detach?.Publish(notifications);
            attach?.Publish(notifications);
            if (old != null) notifications.Run(() => root?.Removed(old));
            if (value != null && ReferenceEquals(value.Parent, owner)) notifications.Run(() => (owner.Root as LayoutRoot)?.Added(value));
            notifications.Run(() => ((LayoutElement)owner).Notify(property));
        }
        catch (Exception error)
        {
            notifications.Record(error);
        }
    }
}

internal sealed class OwnedCollection<T>(ILayoutContainer owner, Action changed) : ObservableCollection<T> where T : class, ILayoutElement
{
    private long _version;

    private void Check(int index, T item, bool replacing)
    {
        if (index < 0 || index > Count || replacing && index == Count) throw new ArgumentOutOfRangeException(nameof(index));
        LayoutTree.Validate(owner, item);
        if (this.Any(candidate => ReferenceEquals(candidate, item)) && (!replacing || !ReferenceEquals(this[index], item)))
            throw new InvalidOperationException("A child cannot occur twice in its container.");
    }

    protected override void InsertItem(int index, T item)
    {
        CheckReentrancy();
        Check(index, item, false);
        var root = owner.Root as LayoutRoot;
        var element = (LayoutElement)(ILayoutElement)item;
        using var notifications = new LayoutMutationScope(root, element.Root as LayoutRoot);
        try
        {
            var version = _version;
            if (!LayoutTree.DetachForTransfer(element) || version != _version) return;
            var attach = element.PrepareParentChange(owner);
            if (version != _version || !attach.IsPrepared) return;
            Check(index, item, false);
            Items.Insert(index, item);
            _version++;
            attach.Commit();
            element.PropertyChanged += ChildChanged;
            attach.Publish(notifications);
            Publish(new(NotifyCollectionChangedAction.Add, item, index), true, notifications);
            if (ReferenceEquals(element.Parent, owner)) notifications.Run(() => (owner.Root as LayoutRoot)?.Added(element));
            notifications.Run(changed);
        }
        catch (Exception error) { notifications.Record(error); }
    }

    protected override void RemoveItem(int index)
    {
        CheckReentrancy();
        var item = this[index];
        var element = (LayoutElement)(ILayoutElement)item;
        var root = owner.Root as LayoutRoot;
        using var notifications = new LayoutMutationScope(root);
        try
        {
            var version = _version;
            var detach = element.PrepareParentChange(null);
            if (version != _version || !detach.IsPrepared) return;
            Items.RemoveAt(index);
            _version++;
            detach.Commit();
            element.PropertyChanged -= ChildChanged;
            detach.Publish(notifications);
            Publish(new(NotifyCollectionChangedAction.Remove, item, index), true, notifications);
            notifications.Run(() => root?.Removed(element));
            notifications.Run(changed);
        }
        catch (Exception error) { notifications.Record(error); }
    }

    protected override void SetItem(int index, T item)
    {
        CheckReentrancy();
        Check(index, item, true);
        var oldItem = this[index];
        if (ReferenceEquals(oldItem, item)) return;
        var old = (LayoutElement)(ILayoutElement)oldItem;
        var element = (LayoutElement)(ILayoutElement)item;
        var root = owner.Root as LayoutRoot;
        using var notifications = new LayoutMutationScope(root, element.Root as LayoutRoot);
        try
        {
            var version = _version;
            if (!LayoutTree.DetachForTransfer(element) || version != _version) return;
            var detach = old.PrepareParentChange(null);
            if (version != _version || !detach.IsPrepared) return;
            var attach = element.PrepareParentChange(owner);
            if (version != _version || !detach.IsPrepared || !attach.IsPrepared) return;
            Check(index, item, true);
            Items[index] = item;
            _version++;
            detach.Commit();
            attach.Commit();
            old.PropertyChanged -= ChildChanged;
            element.PropertyChanged += ChildChanged;
            detach.Publish(notifications);
            attach.Publish(notifications);
            Publish(new(NotifyCollectionChangedAction.Replace, item, oldItem, index), false, notifications);
            notifications.Run(() => root?.Removed(old));
            if (ReferenceEquals(element.Parent, owner)) notifications.Run(() => (owner.Root as LayoutRoot)?.Added(element));
            notifications.Run(changed);
        }
        catch (Exception error) { notifications.Record(error); }
    }

    protected override void ClearItems()
    {
        CheckReentrancy();
        var old = this.Cast<LayoutElement>().ToArray();
        var root = owner.Root as LayoutRoot;
        using var notifications = new LayoutMutationScope(root);
        try
        {
            var version = _version;
            var changes = new List<LayoutParentChange>(old.Length);
            foreach (var element in old)
            {
                changes.Add(element.PrepareParentChange(null));
                if (version != _version || changes.Any(change => !change.IsPrepared)) return;
            }
            Items.Clear();
            _version++;
            foreach (var change in changes)
            {
                change.Commit();
                change.Element.PropertyChanged -= ChildChanged;
            }
            foreach (var change in changes) change.Publish(notifications);
            Publish(new(NotifyCollectionChangedAction.Reset), true, notifications);
            foreach (var element in old) notifications.Run(() => root?.Removed(element));
            notifications.Run(changed);
        }
        catch (Exception error) { notifications.Record(error); }
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        CheckReentrancy();
        if (oldIndex < 0 || oldIndex >= Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        var item = this[oldIndex];
        using var notifications = new LayoutMutationScope(owner.Root as LayoutRoot);
        Items.RemoveAt(oldIndex);
        Items.Insert(newIndex, item);
        _version++;
        Publish(new(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex), false, notifications);
        notifications.Run(changed);
    }

    private void Publish(NotifyCollectionChangedEventArgs args, bool countChanged, LayoutMutationScope notifications)
    {
        if (countChanged) notifications.Run(() => OnPropertyChanged(new(nameof(Count))));
        notifications.Run(() => OnPropertyChanged(new("Item[]")));
        notifications.Run(() => OnCollectionChanged(args));
    }

    private void ChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutContent.IsSelected) && sender is LayoutContent { IsSelected: true } selected && owner is ILayoutContentSelector selector && !ReferenceEquals(selector.SelectedContent, selected))
            selector.SelectedContentIndex = selector.IndexOf(selected);
        if (e.PropertyName is "IsVisible" or "IsHidden" or "IsAutoHidden") (owner as ILayoutElementWithVisibility)?.ComputeVisibility();
        (owner.Root as LayoutRoot)?.Invalidate();
    }
}

public abstract class LayoutGroupBase : LayoutElement
{
    public event EventHandler? ChildrenCollectionChanged;
    public event EventHandler<ChildrenTreeChangedEventArgs>? ChildrenTreeChanged;
    protected virtual void OnChildrenCollectionChanged() => ChildrenCollectionChanged?.Invoke(this, EventArgs.Empty);
    protected virtual void OnChildrenTreeChanged(ChildrenTreeChange change) => ChildrenTreeChanged?.Invoke(this, new(change));
    protected void NotifyChildrenTreeChanged(ChildrenTreeChange change)
    {
        using var notifications = new LayoutMutationScope();
        notifications.Run(() => OnChildrenTreeChanged(change));
        notifications.Run(() => { if (Parent is LayoutGroupBase group) group.NotifyChildrenTreeChanged(ChildrenTreeChange.TreeChanged); });
        notifications.Run(() => (Root as LayoutRoot)?.Invalidate());
    }
}

public abstract class LayoutGroup<T> : LayoutGroupBase, ILayoutGroup, IXmlSerializable, ILayoutElementWithVisibility where T : class, ILayoutElement
{
    private bool _visible = true;
    protected LayoutGroup() => Children = new OwnedCollection<T>(this, Changed);
    public ObservableCollection<T> Children { get; }
    IEnumerable<ILayoutElement> ILayoutContainer.Children => Children;
    public int ChildrenCount => Children.Count;
    public bool IsVisible { get => _visible; protected set { if (Set(ref _visible, value)) OnIsVisibleChanged(); } }
    protected abstract bool GetVisibility();
    public void ComputeVisibility() => IsVisible = GetVisibility();
    protected virtual void OnIsVisibleChanged() => (Parent as ILayoutElementWithVisibility)?.ComputeVisibility();
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) { base.OnParentChanged(oldValue, newValue); ComputeVisibility(); }
    private void Changed()
    {
        using var notifications = new LayoutMutationScope();
        notifications.Run(ComputeVisibility);
        notifications.Run(OnChildrenCollectionChanged);
        notifications.Run(() => Notify(nameof(ChildrenCount)));
        notifications.Run(() => NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged));
    }
    public int IndexOfChild(ILayoutElement element) => element is T child ? Children.IndexOf(child) : -1;
    public void InsertChildAt(int index, ILayoutElement element) => Children.Insert(index, element as T ?? throw new ArgumentException("Incompatible child type.", nameof(element)));
    public void RemoveChild(ILayoutElement element) { if (element is T child) Children.Remove(child); }
    public void RemoveChildAt(int childIndex) => Children.RemoveAt(childIndex);
    public void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        var index = IndexOfChild(oldElement); if (index < 0) throw new ArgumentException("The old element is not a child.", nameof(oldElement));
        ReplaceChildAt(index, newElement);
    }
    public void ReplaceChildAt(int index, ILayoutElement element) => Children[index] = element as T ?? throw new ArgumentException("Incompatible child type.", nameof(element));
    public void MoveChild(int oldIndex, int newIndex)
    {
        if (this is ILayoutPositionableElement { CanRepositionItems: false }) return;
        Children.Move(oldIndex, newIndex); ChildMoved(oldIndex, newIndex);
    }
    protected virtual void ChildMoved(int oldIndex, int newIndex) => NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged);
    public XmlSchema? GetSchema() => null;
    public virtual void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
    public virtual void WriteXml(XmlWriter writer) => LayoutXml.WriteBody(this, writer);
}
