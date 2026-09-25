namespace UnoDock.Layout;
public interface ILayoutContainer : ILayoutElement
{
    IEnumerable<ILayoutElement> Children { get; }

    int ChildrenCount { get; }

    void RemoveChild(ILayoutElement element);
    void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement);
}
