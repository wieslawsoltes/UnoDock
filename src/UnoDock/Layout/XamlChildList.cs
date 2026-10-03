#if WINDOWS
using System.Collections;

namespace UnoDock.Layout;
/// <summary>An object-typed view of a group's children for the WinUI XAML compiler, which
/// checks markup items against a collection's item type through base classes only and
/// therefore rejects children of interface-typed groups. Items are type-checked on insert.</summary>
internal sealed class XamlChildList<T>(IList<T> children) : IList<object> where T : class
{
    public object this[int index]
    {
        get => children[index];
        set => children[index] = Child(value);
    }

    public int Count => children.Count;
    public bool IsReadOnly => false;

    public void Add(object item) => children.Add(Child(item));
    public void Clear() => children.Clear();
    public bool Contains(object item) => item is T child && children.Contains(child);
    public void CopyTo(object[] array, int arrayIndex)
    {
        foreach (var child in children)
            array[arrayIndex++] = child;
    }

    public IEnumerator<object> GetEnumerator() => children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int IndexOf(object item) => item is T child ? children.IndexOf(child) : -1;
    public void Insert(int index, object item) => children.Insert(index, Child(item));
    public bool Remove(object item) => item is T child && children.Remove(child);
    public void RemoveAt(int index) => children.RemoveAt(index);
    private static T Child(object item) => item as T ?? throw new ArgumentException($"A {item?.GetType().Name ?? "null"} cannot be a child of this layout group.", nameof(item));
}
#endif
