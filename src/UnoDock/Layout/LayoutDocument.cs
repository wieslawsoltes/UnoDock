using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;
public class LayoutDocument : LayoutContent
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private bool _canMove = true, _visible = true;
    private string? _description;
    public bool CanMove { get => _canMove; set => Set(ref _canMove, value); }
    public bool IsVisible { get => _visible; internal set => Set(ref _visible, value); }
    public string? Description { get => _description; set => Set(ref _description, value); }

    public override void Close() => CloseCore();
    protected override void InternalDock() => DockOperations.Restore(this);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
