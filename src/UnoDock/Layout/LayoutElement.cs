using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

public abstract partial class LayoutElement : DependencyObject, ILayoutElement
{
    /// <summary>Writes a bounded, cycle-safe diagnostic snapshot without evaluating user content.</summary>
    public virtual void ConsoleDump(int tab) => LayoutDiagnostics.Write(this, Console.Out, tab);
    private ILayoutContainer? _parent;
    internal string SerializationId
    {
        get;
        set;
    } = "";

    [System.Xml.Serialization.XmlIgnore]
    public ILayoutContainer? Parent
    {
        get => _parent;
        set
        {
            if (ReferenceEquals(_parent, value))
                return;
            if (value == null)
            {
                _parent?.RemoveChild(this);
                return;
            }

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
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        RaisePropertyChanging(name);
        field = value;
        RaisePropertyChanged(name);
        return true;
    }

    internal void Notify(string name) => RaisePropertyChanged(name);
    protected virtual void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
    }

    protected virtual void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
    }

    protected virtual void OnRootChanged(ILayoutRoot? oldRoot, ILayoutRoot? newRoot)
    {
    }
}
