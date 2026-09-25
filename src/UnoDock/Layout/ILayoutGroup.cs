namespace UnoDock.Layout;
public interface ILayoutGroup : ILayoutContainer
{
    event EventHandler? ChildrenCollectionChanged;
    int IndexOfChild(ILayoutElement element);
    void InsertChildAt(int index, ILayoutElement element);
    void RemoveChildAt(int index);
    void ReplaceChildAt(int index, ILayoutElement element);
}
