using System.Xml;

namespace UnoDock.Layout;

[ContentProperty(Name = "Children")]
public class LayoutDocumentPaneGroup : LayoutPositionableGroup<ILayoutDocumentPane>, ILayoutDocumentPane, ILayoutOrientableGroup
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private Orientation _orientation = Orientation.Horizontal;
    public LayoutDocumentPaneGroup()
    {
    }

    public LayoutDocumentPaneGroup(LayoutDocumentPane documentPane) => Children.Add(documentPane);
    public Orientation Orientation
    {
        get => _orientation;
        set => Set(ref _orientation, value);
    }

    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
