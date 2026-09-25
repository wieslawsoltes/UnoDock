using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

[ContentProperty(Name = nameof(RootDocument))]
public class LayoutDocumentFloatingWindow : LayoutFloatingWindow
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private LayoutDocument? _document;
    public LayoutDocumentFloatingWindow()
    {
    }

    public LayoutDocument? RootDocument
    {
        get => _document;
        set
        {
            if (ReferenceEquals(value, _document))
                return;
            LayoutTree.ReplaceSlot(this, ref _document, value, nameof(RootDocument));
            RootDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? RootDocumentChanged;
    public override bool IsValid => _document != null;
    public override int ChildrenCount => _document == null ? 0 : 1;

    public override IEnumerable<ILayoutElement> Children
    {
        get
        {
            if (_document != null)
                yield return _document;
        }
    }

    public override void RemoveChild(ILayoutElement element)
    {
        if (ReferenceEquals(element, _document))
            RootDocument = null;
    }

    public override void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        if (!ReferenceEquals(oldElement, _document) || newElement is not LayoutDocument doc)
            throw new ArgumentException("Invalid document replacement.");
        RootDocument = doc;
    }

    public override void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
}
