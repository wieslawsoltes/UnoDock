using System.Xml;

namespace UnoDock.Layout;

[ContentProperty(Name = "Children")]
public class LayoutDocumentPane : LayoutPositionableGroup<LayoutContent>, ILayoutDocumentPane, ILayoutContentSelector
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private readonly PaneSelection _selection;
    private bool _showHeader = true;
    public LayoutDocumentPane() => _selection = new(this, Notify);
    public LayoutDocumentPane(LayoutContent firstChild) : this() => Children.Add(firstChild);
    public LayoutContent? SelectedContent => _selection.Content;
    public int SelectedContentIndex { get => _selection.Index; set => _selection.Index = value; }
    public int IndexOf(LayoutContent content) => Children.IndexOf(content);
    public bool ShowHeader { get => _showHeader; set => Set(ref _showHeader, value); }
    public IEnumerable<LayoutContent> ChildrenSorted => Children.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase);
    protected override bool GetVisibility() => true; // An empty document well is a valid drop target.
    protected override void OnChildrenCollectionChanged() { _selection.CollectionChanged(); base.OnChildrenCollectionChanged(); Notify(nameof(ChildrenSorted)); }
    protected override void ChildMoved(int oldIndex, int newIndex) { _selection.CollectionChanged(); base.ChildMoved(oldIndex, newIndex); }
    protected override void OnIsVisibleChanged() => base.OnIsVisibleChanged();
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
