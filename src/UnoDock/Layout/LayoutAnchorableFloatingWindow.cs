using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;
[ContentProperty(Name = nameof(RootPanel))]
public class LayoutAnchorableFloatingWindow : LayoutFloatingWindow, ILayoutElementWithVisibility
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private LayoutAnchorablePaneGroup? _panel;
    private bool _visible = true;
    public LayoutAnchorableFloatingWindow() { }
    public LayoutAnchorablePaneGroup? RootPanel
    {
        get => _panel;
        set { if (ReferenceEquals(value, _panel)) return; LayoutTree.ReplaceSlot(this, ref _panel, value, nameof(RootPanel)); RefreshVisibility(); }
    }
    [System.Xml.Serialization.XmlIgnore]
    public bool IsVisible { get => _visible; private set { if (Set(ref _visible, value)) IsVisibleChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? IsVisibleChanged;
    public bool IsSinglePane => this.Descendents().OfType<LayoutAnchorablePane>().Count(p => p.ChildrenCount > 0) == 1;
    public ILayoutAnchorablePane? SinglePane => IsSinglePane ? this.Descendents().OfType<LayoutAnchorablePane>().First(p => p.ChildrenCount > 0) : null;
    public override bool IsValid => this.Descendents().OfType<LayoutAnchorable>().Any();
    public override int ChildrenCount => _panel == null ? 0 : 1;
    public override IEnumerable<ILayoutElement> Children { get { if (_panel != null) yield return _panel; } }
    internal void RefreshVisibility() { IsVisible = _panel?.IsVisible == true; Notify(nameof(IsSinglePane)); Notify(nameof(SinglePane)); }
    public void ComputeVisibility() => RefreshVisibility();
    public override void RemoveChild(ILayoutElement element) { if (ReferenceEquals(element, _panel)) RootPanel = null; }
    public override void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    { if (!ReferenceEquals(oldElement, _panel) || newElement is not LayoutAnchorablePaneGroup panel) throw new ArgumentException("Invalid anchorable group replacement."); RootPanel = panel; }
    public override void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
}
