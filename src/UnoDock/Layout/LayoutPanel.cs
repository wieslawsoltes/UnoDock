using System.Xml;

namespace UnoDock.Layout;
[ContentProperty(Name = "Children")]
public class LayoutPanel : LayoutPositionableGroup<ILayoutPanelElement>, ILayoutPanelElement, ILayoutOrientableGroup
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private Orientation _orientation = Orientation.Horizontal;
    public LayoutPanel()
    {
    }

    public LayoutPanel(ILayoutPanelElement firstChild) => Children.Add(firstChild);
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }

    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
