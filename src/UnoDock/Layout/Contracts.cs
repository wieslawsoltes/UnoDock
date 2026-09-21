namespace Xceed.Wpf.AvalonDock.Layout;

public enum AnchorSide { Left, Top, Right, Bottom }
[Flags] public enum AnchorableShowStrategy : byte { Most = 1, Left = 2, Right = 4, Top = 16, Bottom = 32 }
public enum ChildrenTreeChange { DirectChildrenChanged, TreeChanged }
public interface ILayoutElement : INotifyPropertyChanged, INotifyPropertyChanging
{
    ILayoutContainer? Parent { get; }
    ILayoutRoot? Root { get; }
}
public interface ILayoutContainer : ILayoutElement
{
    IEnumerable<ILayoutElement> Children { get; }
    int ChildrenCount { get; }
    void RemoveChild(ILayoutElement element);
    void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement);
}
public interface ILayoutGroup : ILayoutContainer
{
    event EventHandler? ChildrenCollectionChanged;
    int IndexOfChild(ILayoutElement element);
    void InsertChildAt(int index, ILayoutElement element);
    void RemoveChildAt(int index);
    void ReplaceChildAt(int index, ILayoutElement element);
}
public interface ILayoutElementWithVisibility { void ComputeVisibility(); }
public interface ILayoutPanelElement : ILayoutElement { bool IsVisible { get; } }
public interface ILayoutPane : ILayoutContainer, ILayoutElementWithVisibility { void MoveChild(int oldIndex, int newIndex); void RemoveChildAt(int childIndex); }
public interface ILayoutAnchorablePane : ILayoutPanelElement, ILayoutPane { }
public interface ILayoutDocumentPane : ILayoutPanelElement, ILayoutPane { }
public interface ILayoutOrientableGroup : ILayoutGroup { Orientation Orientation { get; set; } }
public interface ILayoutContentSelector
{
    LayoutContent? SelectedContent { get; }
    int SelectedContentIndex { get; set; }
    int IndexOf(LayoutContent content);
}
public interface ILayoutControl { ILayoutElement? Model { get; } }
public interface ILayoutRoot
{
    DockingManager? Manager { get; }
    LayoutPanel RootPanel { get; }
    LayoutAnchorSide LeftSide { get; }
    LayoutAnchorSide TopSide { get; }
    LayoutAnchorSide RightSide { get; }
    LayoutAnchorSide BottomSide { get; }
    LayoutContent? ActiveContent { get; set; }
    ObservableCollection<LayoutAnchorable> Hidden { get; }
    ObservableCollection<LayoutFloatingWindow> FloatingWindows { get; }
    void CollectGarbage();
}
public interface ILayoutUpdateStrategy
{
    bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer);
    void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown);
    bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument documentToShow, ILayoutContainer destinationContainer);
    void AfterInsertDocument(LayoutRoot layout, LayoutDocument documentShown);
}
// Public portability contracts extend the original internal contracts without emulating WPF.
public interface ILayoutPositionableElement
{
    GridLength DockWidth { get; set; }
    GridLength DockHeight { get; set; }
    double DockMinWidth { get; set; }
    double DockMinHeight { get; set; }
    double FloatingLeft { get; set; }
    double FloatingTop { get; set; }
    double FloatingWidth { get; set; }
    double FloatingHeight { get; set; }
    bool IsMaximized { get; set; }
    bool CanRepositionItems { get; set; }
    bool AllowDuplicateContent { get; set; }
}
public interface ILayoutPreviousContainer
{
    ILayoutContainer? PreviousContainer { get; }
    string? PreviousContainerId { get; }
    int PreviousContainerIndex { get; set; }
}
public class LayoutElementEventArgs(LayoutElement element) : EventArgs { public LayoutElement Element { get; private set; } = element; }
public class ChildrenTreeChangedEventArgs(ChildrenTreeChange change) : EventArgs { public ChildrenTreeChange Change { get; private set; } = change; }

public static class Extensions
{
    public static IEnumerable<ILayoutElement> Descendents(this ILayoutElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is not ILayoutContainer container) yield break;
        var stack = new Stack<IEnumerator<ILayoutElement>>();
        stack.Push(container.Children.GetEnumerator());
        try
        {
            while (stack.Count > 0)
            {
                var cursor = stack.Peek();
                if (!cursor.MoveNext()) { cursor.Dispose(); stack.Pop(); continue; }
                var child = cursor.Current;
                yield return child;
                if (child is ILayoutContainer nested) stack.Push(nested.Children.GetEnumerator());
            }
        }
        finally { foreach (var cursor in stack) cursor.Dispose(); }
    }
    public static T? FindParent<T>(this ILayoutElement element)
    {
        for (var p = element.Parent; p != null; p = p.Parent) if (p is T result) return result;
        return default;
    }
    public static ILayoutRoot? GetRoot(this ILayoutElement element) => element.Root;
    public static bool ContainsChildOfType<T>(this ILayoutContainer element) => element.Descendents().Any(x => x is T);
    public static bool ContainsChildOfType<T, S>(this ILayoutContainer container) => container.Descendents().Any(x => x is T or S);
    public static bool IsOfType<T, S>(this ILayoutContainer container) => container is T or S;
    public static AnchorSide GetSide(this ILayoutElement element)
    {
        if (element is LayoutAnchorSide side) return side.Side;
        if (element.FindParent<LayoutAnchorSide>() is { } parent) return parent.Side;
        for (ILayoutElement current = element; current.Parent is { } group; current = group)
        {
            if (group is not ILayoutOrientableGroup oriented) continue;
            var children = group.Children.ToArray();
            var index = Array.IndexOf(children, current);
            var docIndex = Array.FindIndex(children, x => x is ILayoutDocumentPane || x is ILayoutContainer c && c.ContainsChildOfType<LayoutDocumentPane>());
            if (docIndex < 0 || docIndex == index) continue;
            return oriented.Orientation == Orientation.Horizontal ? (index < docIndex ? AnchorSide.Left : AnchorSide.Right) : (index < docIndex ? AnchorSide.Top : AnchorSide.Bottom);
        }
        return AnchorSide.Right;
    }
}
