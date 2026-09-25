using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

public abstract class LayoutGroupBase : LayoutElement
{
    public event EventHandler? ChildrenCollectionChanged;
    public event EventHandler<ChildrenTreeChangedEventArgs>? ChildrenTreeChanged;
    protected virtual void OnChildrenCollectionChanged() => ChildrenCollectionChanged?.Invoke(this, EventArgs.Empty);
    protected virtual void OnChildrenTreeChanged(ChildrenTreeChange change) => ChildrenTreeChanged?.Invoke(this, new(change));
    protected void NotifyChildrenTreeChanged(ChildrenTreeChange change)
    {
        OnChildrenTreeChanged(change);
        if (Parent is LayoutGroupBase group)
            group.NotifyChildrenTreeChanged(ChildrenTreeChange.TreeChanged);
        (Root as LayoutRoot)?.Invalidate();
    }
}
