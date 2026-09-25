using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;
public abstract class LayoutFloatingWindow : LayoutElement, ILayoutContainer, IXmlSerializable
{
    public LayoutFloatingWindow()
    {
    }

    public abstract IEnumerable<ILayoutElement> Children { get; }
    public abstract int ChildrenCount { get; }
    public abstract bool IsValid { get; }

    public abstract void RemoveChild(ILayoutElement element);
    public abstract void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement);
    public XmlSchema? GetSchema() => null;
    public abstract void ReadXml(XmlReader reader);
    public virtual void WriteXml(XmlWriter writer) => LayoutXml.WriteBody(this, writer);
}
