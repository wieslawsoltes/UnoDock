using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

public abstract partial class LayoutElement : DependencyObject, ILayoutElement
{
    /// <summary>Writes a bounded, cycle-safe diagnostic snapshot without evaluating user content.</summary>
    public virtual void ConsoleDump(int tab) => LayoutDiagnostics.Write(this, Console.Out, tab);
    private ILayoutContainer? _parent;
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
        PropertyChanged?.Invoke(this, new(propertyName));
        (Root as LayoutRoot)?.Invalidate();
    }
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        RaisePropertyChanging(name); field = value; RaisePropertyChanged(name); return true;
    }
    internal void Notify(string name) => RaisePropertyChanged(name);
    internal void AssignParent(ILayoutContainer? value)
    {
        if (ReferenceEquals(_parent, value)) return;
        var old = _parent; var oldRoot = Root;
        OnParentChanging(old, value); RaisePropertyChanging(nameof(Parent));
        _parent = value;
        OnParentChanged(old, value); RaisePropertyChanged(nameof(Parent));
        var newRoot = Root;
        if (!ReferenceEquals(oldRoot, newRoot))
        {
            OnRootChanged(oldRoot, newRoot); RaisePropertyChanged(nameof(Root));
            foreach (var child in this.Descendents().OfType<LayoutElement>())
            {
                child.OnRootChanged(oldRoot, newRoot); child.Notify(nameof(Root));
                if (child is LayoutContent content) content.RefreshPlacement();
            }
        }
        if (this is LayoutContent c) c.RefreshPlacement();
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
    internal static void ReplaceSlot<T>(ILayoutContainer owner, ref T? field, T? value, string property) where T : LayoutElement
    {
        if (ReferenceEquals(field, value)) return;
        if (value != null) Validate(owner, value);
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        var old = field;
        if (old != null) { old.AssignParent(null); (owner.Root as LayoutRoot)?.Removed(old); }
        if (value?.Parent != null) value.Parent.RemoveChild(value);
        field = value;
        value?.AssignParent(owner);
        if (value != null) (owner.Root as LayoutRoot)?.Added(value);
        ((LayoutElement)owner).Notify(property);
    }
}

internal sealed class OwnedCollection<T>(ILayoutContainer owner, Action changed) : ObservableCollection<T> where T : class, ILayoutElement
{
    private void Check(int index, T item, bool replacing)
    {
        if (index < 0 || index > Count || replacing && index == Count) throw new ArgumentOutOfRangeException(nameof(index));
        LayoutTree.Validate(owner, item);
        if (Contains(item) && (!replacing || !ReferenceEquals(this[index], item))) throw new InvalidOperationException("A child cannot occur twice in its container.");
    }
    protected override void InsertItem(int index, T item)
    {
        CheckReentrancy(); Check(index, item, false);
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        using var oldBatch = (item.Root as LayoutRoot)?.BeginUpdate();
        item.Parent?.RemoveChild(item);
        ((LayoutElement)(ILayoutElement)item).AssignParent(owner);
        item.PropertyChanged += ChildChanged;
        base.InsertItem(index, item);
        if (item is LayoutContent content) content.RefreshPlacement();
        (owner.Root as LayoutRoot)?.Added((LayoutElement)(ILayoutElement)item);
        changed();
    }
    protected override void RemoveItem(int index)
    {
        CheckReentrancy();
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        var item = this[index]; item.PropertyChanged -= ChildChanged;
        ((LayoutElement)(ILayoutElement)item).AssignParent(null);
        base.RemoveItem(index);
        (owner.Root as LayoutRoot)?.Removed((LayoutElement)(ILayoutElement)item);
        changed();
    }
    protected override void SetItem(int index, T item)
    {
        CheckReentrancy(); Check(index, item, true);
        if (ReferenceEquals(this[index], item)) return;
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        var old = this[index]; old.PropertyChanged -= ChildChanged;
        item.Parent?.RemoveChild(item);
        ((LayoutElement)(ILayoutElement)old).AssignParent(null); ((LayoutElement)(ILayoutElement)item).AssignParent(owner);
        item.PropertyChanged += ChildChanged;
        base.SetItem(index, item);
        if (item is LayoutContent content) content.RefreshPlacement();
        (owner.Root as LayoutRoot)?.Removed((LayoutElement)(ILayoutElement)old); (owner.Root as LayoutRoot)?.Added((LayoutElement)(ILayoutElement)item);
        changed();
    }
    protected override void ClearItems()
    {
        CheckReentrancy();
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        var old = this.ToArray();
        foreach (var item in old) { item.PropertyChanged -= ChildChanged; ((LayoutElement)(ILayoutElement)item).AssignParent(null); }
        base.ClearItems();
        foreach (var item in old) (owner.Root as LayoutRoot)?.Removed((LayoutElement)(ILayoutElement)item);
        changed();
    }
    protected override void MoveItem(int oldIndex, int newIndex)
    {
        CheckReentrancy();
        using var batch = (owner.Root as LayoutRoot)?.BeginUpdate();
        base.MoveItem(oldIndex, newIndex); changed();
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
        OnChildrenTreeChanged(change);
        if (Parent is LayoutGroupBase group) group.NotifyChildrenTreeChanged(ChildrenTreeChange.TreeChanged);
        (Root as LayoutRoot)?.Invalidate();
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
        ComputeVisibility(); OnChildrenCollectionChanged(); Notify(nameof(ChildrenCount));
        NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged);
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
