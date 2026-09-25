using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

[ContentProperty(Name = nameof(RootPanel))]
public partial class LayoutRoot : LayoutElement, ILayoutContainer, ILayoutRoot, IXmlSerializable
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private readonly UpdateBatch _updates;
    private LayoutPanel? _panel;
    private LayoutAnchorSide? _top, _right, _bottom, _left;
    private LayoutContent? _active;
    private DockingManager? _manager;
    private bool _initializing = true;
    public LayoutRoot()
    {
        _updates = new(() => { RepairActivation(); Updated?.Invoke(this, EventArgs.Empty); });
        FloatingWindows = new OwnedCollection<LayoutFloatingWindow>(this, Invalidate);
        Hidden = new OwnedCollection<LayoutAnchorable>(this, Invalidate);
        using var batch = BeginUpdate();
        RootPanel = new(new LayoutDocumentPane());
        TopSide = new(); RightSide = new(); BottomSide = new(); LeftSide = new();
        _initializing = false;
    }
    [System.Xml.Serialization.XmlIgnore]
    public DockingManager? Manager { get => _manager; internal set => Set(ref _manager, value); }
    public LayoutPanel RootPanel { get => _panel!; set { ArgumentNullException.ThrowIfNull(value); LayoutTree.ReplaceSlot(this, ref _panel, value, nameof(RootPanel)); } }
    public LayoutAnchorSide TopSide { get => _top!; set { ArgumentNullException.ThrowIfNull(value); LayoutTree.ReplaceSlot(this, ref _top, value, nameof(TopSide)); value.SetSide(AnchorSide.Top); } }
    public LayoutAnchorSide RightSide { get => _right!; set { ArgumentNullException.ThrowIfNull(value); LayoutTree.ReplaceSlot(this, ref _right, value, nameof(RightSide)); value.SetSide(AnchorSide.Right); } }
    public LayoutAnchorSide BottomSide { get => _bottom!; set { ArgumentNullException.ThrowIfNull(value); LayoutTree.ReplaceSlot(this, ref _bottom, value, nameof(BottomSide)); value.SetSide(AnchorSide.Bottom); } }
    public LayoutAnchorSide LeftSide { get => _left!; set { ArgumentNullException.ThrowIfNull(value); LayoutTree.ReplaceSlot(this, ref _left, value, nameof(LeftSide)); value.SetSide(AnchorSide.Left); } }
    public ObservableCollection<LayoutFloatingWindow> FloatingWindows { get; }
    public ObservableCollection<LayoutAnchorable> Hidden { get; }
    [System.Xml.Serialization.XmlIgnore]
    public LayoutContent? LastFocusedDocument { get; internal set; }
    [System.Xml.Serialization.XmlIgnore]
    public LayoutContent? ActiveContent
    {
        get => _active;
        set => ChangeActiveContent(value);
    }
    public IEnumerable<ILayoutElement> Children
    {
        get
        {
            if (_panel != null) yield return _panel;
            if (_top != null) yield return _top; if (_right != null) yield return _right;
            if (_bottom != null) yield return _bottom; if (_left != null) yield return _left;
            foreach (var f in FloatingWindows) yield return f;
            foreach (var h in Hidden) yield return h;
        }
    }
    public int ChildrenCount => Children.Count();
    public event EventHandler? Updated;
    public event EventHandler<LayoutElementEventArgs>? ElementAdded;
    public event EventHandler<LayoutElementEventArgs>? ElementRemoved;
    public IDisposable BeginUpdate() => _updates.Begin();
    internal void Invalidate() { if (!_initializing) _updates.Invalidate(); }
    internal void Added(LayoutElement element) => PublishElementChange(element, true);
    internal void Removed(LayoutElement element) => PublishElementChange(element, false);
    private void RepairActivation() => RepairActiveContent();
    internal AnchorSide SideOf(LayoutAnchorSide side) => ReferenceEquals(side, _top) ? AnchorSide.Top : ReferenceEquals(side, _left) ? AnchorSide.Left : ReferenceEquals(side, _bottom) ? AnchorSide.Bottom : AnchorSide.Right;
    internal LayoutAnchorSide GetSide(AnchorSide side) => side switch { AnchorSide.Top => TopSide, AnchorSide.Left => LeftSide, AnchorSide.Bottom => BottomSide, _ => RightSide };
    public void RemoveChild(ILayoutElement element)
    {
        switch (element)
        {
            case LayoutFloatingWindow window: FloatingWindows.Remove(window); break;
            case LayoutAnchorable anchorable: Hidden.Remove(anchorable); break;
            case LayoutPanel when ReferenceEquals(element, _panel): RootPanel = new(new LayoutDocumentPane()); break;
            case LayoutAnchorSide side when ReferenceEquals(side, _top): TopSide = new(); break;
            case LayoutAnchorSide side when ReferenceEquals(side, _right): RightSide = new(); break;
            case LayoutAnchorSide side when ReferenceEquals(side, _bottom): BottomSide = new(); break;
            case LayoutAnchorSide side when ReferenceEquals(side, _left): LeftSide = new(); break;
        }
    }
    public void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        if (!ReferenceEquals(oldElement.Parent, this)) throw new ArgumentException("The element is not a direct child.", nameof(oldElement));
        switch (oldElement)
        {
            case LayoutPanel when newElement is LayoutPanel p: RootPanel = p; break;
            case LayoutFloatingWindow f when newElement is LayoutFloatingWindow n: FloatingWindows[FloatingWindows.IndexOf(f)] = n; break;
            case LayoutAnchorable a when newElement is LayoutAnchorable n: Hidden[Hidden.IndexOf(a)] = n; break;
            case LayoutAnchorSide s when newElement is LayoutAnchorSide n:
                switch (SideOf(s)) { case AnchorSide.Top: TopSide = n; break; case AnchorSide.Right: RightSide = n; break; case AnchorSide.Bottom: BottomSide = n; break; default: LeftSide = n; break; }
                break;
            default: throw new ArgumentException("Incompatible replacement type.", nameof(newElement));
        }
    }
    public void CollectGarbage()
    {
        using var batch = BeginUpdate();
        var referenced = this.Descendents().OfType<ILayoutPreviousContainer>().Select(c => c.PreviousContainer).Where(c => c != null).ToHashSet();
        var documentPane = RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        foreach (var item in this.Descendents().OfType<ILayoutGroup>().Reverse().ToArray())
        {
            if (item.ChildrenCount == 0 && item is not LayoutAnchorSide && !ReferenceEquals(item, RootPanel) && !ReferenceEquals(item, documentPane) && !referenced.Contains(item))
                item.Parent?.RemoveChild(item);
        }
        foreach (var f in FloatingWindows.Where(f => !f.IsValid).ToArray()) FloatingWindows.Remove(f);
        if (!RootPanel.Descendents().OfType<LayoutDocumentPane>().Any()) RootPanel.Children.Add(new LayoutDocumentPane());
    }
    public XmlSchema? GetSchema() => null;
    public void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
    public void WriteXml(XmlWriter writer) => LayoutXml.WriteBody(this, writer);
}
