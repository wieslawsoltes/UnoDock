using System.Xml;

namespace UnoDock.Layout;
[ContentProperty(Name = "Children")]
public class LayoutAnchorablePaneGroup : LayoutPositionableGroup<ILayoutAnchorablePane>, ILayoutAnchorablePane, ILayoutOrientableGroup
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private Orientation _orientation = Orientation.Horizontal;
    public LayoutAnchorablePaneGroup()
    {
    }

    public LayoutAnchorablePaneGroup(LayoutAnchorablePane firstChild) => Children.Add(firstChild);
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }

    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    protected override void OnIsVisibleChanged()
    {
        base.OnIsVisibleChanged();
        (Parent as LayoutAnchorableFloatingWindow)?.RefreshVisibility();
    }

    protected override void OnDockWidthChanged() => base.OnDockWidthChanged();
    protected override void OnDockHeightChanged() => base.OnDockHeightChanged();
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
