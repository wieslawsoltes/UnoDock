namespace UnoDock.Layout;
public static class Extensions
{
    public static IEnumerable<ILayoutElement> Descendents(this ILayoutElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is not ILayoutContainer container)
            yield break;
        var stack = new Stack<IEnumerator<ILayoutElement>>();
        stack.Push(container.Children.GetEnumerator());
        try
        {
            while (stack.Count > 0)
            {
                var cursor = stack.Peek();
                if (!cursor.MoveNext())
                {
                    cursor.Dispose();
                    stack.Pop();
                    continue;
                }

                var child = cursor.Current;
                yield return child;
                if (child is ILayoutContainer nested)
                    stack.Push(nested.Children.GetEnumerator());
            }
        }
        finally
        {
            foreach (var cursor in stack)
                cursor.Dispose();
        }
    }

    public static T? FindParent<T>(this ILayoutElement element)
    {
        for (var p = element.Parent; p != null; p = p.Parent)
            if (p is T result)
                return result;
        return default;
    }

    public static ILayoutRoot? GetRoot(this ILayoutElement element) => element.Root;
    public static bool ContainsChildOfType<T>(this ILayoutContainer element) => element.Descendents().Any(x => x is T);
    public static bool ContainsChildOfType<T, S>(this ILayoutContainer container) => container.Descendents().Any(x => x is T or S);
    public static bool IsOfType<T, S>(this ILayoutContainer container) => container is T or S;
    public static AnchorSide GetSide(this ILayoutElement element)
    {
        if (element is LayoutAnchorSide side)
            return side.Side;
        if (element.FindParent<LayoutAnchorSide>()is { } parent)
            return parent.Side;
        for (ILayoutElement current = element; current.Parent is { } group; current = group)
        {
            if (group is not ILayoutOrientableGroup oriented)
                continue;
            var children = group.Children.ToArray();
            var index = Array.IndexOf(children, current);
            var docIndex = Array.FindIndex(children, x => x is ILayoutDocumentPane || x is ILayoutContainer c && c.ContainsChildOfType<LayoutDocumentPane>());
            if (docIndex < 0 || docIndex == index)
                continue;
            return oriented.Orientation == Orientation.Horizontal ? (index < docIndex ? AnchorSide.Left : AnchorSide.Right) : (index < docIndex ? AnchorSide.Top : AnchorSide.Bottom);
        }

        return AnchorSide.Right;
    }
}
