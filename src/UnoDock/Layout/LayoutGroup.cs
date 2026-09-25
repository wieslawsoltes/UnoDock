using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

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
        LayoutMutation.Execute(mutation =>
        {
            mutation.Run(ComputeVisibility);
            mutation.Run(OnChildrenCollectionChanged);
            mutation.Run(() => Notify(nameof(ChildrenCount)));
            mutation.Run(() => NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged));
        });
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
