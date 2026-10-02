using System.Xml;

namespace UnoDock.Layout;
#if WINDOWS
// Native WinUI markup adds children through XamlChildren (see the .WinUI.cs part).
[ContentProperty(Name = "XamlChildren")]
#else
[ContentProperty(Name = "Children")]
#endif
public partial class LayoutAnchorGroup : LayoutGroup<LayoutAnchorable>, ILayoutPreviousContainer
{
    public LayoutAnchorGroup()
    {
    }

    public ILayoutContainer? PreviousContainer
    {
        get;
        internal set;
    }
    public string? PreviousContainerId
    {
        get;
        internal set;
    }
    public int PreviousContainerIndex
    {
        get;
        set;
    }

    protected override bool GetVisibility() => Children.Count > 0;
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
