using System.Xml;

namespace UnoDock.Layout;

[ContentProperty(Name = "Children")]
public class LayoutAnchorablePane : LayoutPositionableGroup<LayoutAnchorable>, ILayoutAnchorablePane, ILayoutContentSelector
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private readonly PaneSelection _selection;
    private string? _name;
    public LayoutAnchorablePane() => _selection = new(this, Notify);
    public LayoutAnchorablePane(LayoutAnchorable anchorable) : this() => Children.Add(anchorable);
    public LayoutContent? SelectedContent => _selection.Content;
    public int SelectedContentIndex
    {
        get => _selection.Index; set => _selection.Index = value;
    }

    public int IndexOf(LayoutContent content) => content is LayoutAnchorable a ? Children.IndexOf(a) : -1;
    public string? Name
    {
        get => _name; set => Set(ref _name, value);
    }
    public bool CanClose => Children.All(c => c.CanClose);
    public bool CanHide => Children.All(c => c.CanHide);
    public bool IsHostedInFloatingWindow => this.FindParent<LayoutFloatingWindow>() != null;
    public bool IsDirectlyHostedInFloatingWindow => this.FindParent<LayoutAnchorableFloatingWindow>() is { IsSinglePane: true };

    protected override bool GetVisibility() => Children.Count > 0;
    protected override void OnChildrenCollectionChanged()
    {
        _selection.CollectionChanged();
        base.OnChildrenCollectionChanged();
        Notify(nameof(CanClose));
        Notify(nameof(CanHide));
    }

    protected override void ChildMoved(int oldIndex, int newIndex)
    {
        _selection.CollectionChanged();
        base.ChildMoved(oldIndex, newIndex);
    }

    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        base.OnParentChanged(oldValue, newValue);
        Notify(nameof(IsHostedInFloatingWindow));
        Notify(nameof(IsDirectlyHostedInFloatingWindow));
    }

    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
