using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

[ContentProperty(Name = nameof(RootPanel))]
public class LayoutRoot : LayoutElement, ILayoutContainer, ILayoutRoot, IXmlSerializable
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
        set
        {
            if (value != null && (!ReferenceEquals(value.Root, this) || !value.IsEnabled || value is LayoutAnchorable { IsHidden: true }))
                throw new InvalidOperationException("Active content must be an enabled, non-hidden child of this layout.");
            if (ReferenceEquals(_active, value)) return;
            using var batch = BeginUpdate();
            var old = _active; _active = value;
            old?.SetActive(false); value?.SetActive(true);
            if (value is LayoutDocument || value?.Parent is LayoutDocumentPane)
            {
                if (LastFocusedDocument != null) LastFocusedDocument.IsLastFocusedDocument = false;
                LastFocusedDocument = value; value.IsLastFocusedDocument = true; Notify(nameof(LastFocusedDocument));
            }
            Notify(nameof(ActiveContent));
        }
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
    internal void Added(LayoutElement element)
    {
        ElementAdded?.Invoke(this, new(element));
        foreach (var child in element.Descendents().OfType<LayoutElement>()) ElementAdded?.Invoke(this, new(child));
        Invalidate();
    }
    internal void Removed(LayoutElement element)
    {
        ElementRemoved?.Invoke(this, new(element));
        foreach (var child in element.Descendents().OfType<LayoutElement>()) ElementRemoved?.Invoke(this, new(child));
        Invalidate();
    }
    private void RepairActivation()
    {
        if (_active != null && (!ReferenceEquals(_active.Root, this) || !_active.IsEnabled || _active is LayoutAnchorable { IsHidden: true }))
        {
            _active.SetActive(false); _active = null;
            var next = this.Descendents().OfType<LayoutContent>().Where(c => c.IsEnabled && c is not LayoutAnchorable { IsHidden: true })
                .OrderByDescending(c => c.LastActivationTimeStamp).FirstOrDefault();
            if (next != null) { _active = next; next.SetActive(true); }
            Notify(nameof(ActiveContent));
        }
        if (LastFocusedDocument != null && !ReferenceEquals(LastFocusedDocument.Root, this))
        { LastFocusedDocument.IsLastFocusedDocument = false; LastFocusedDocument = null; Notify(nameof(LastFocusedDocument)); }
    }
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

public abstract class LayoutFloatingWindow : LayoutElement, ILayoutContainer, IXmlSerializable
{
    public LayoutFloatingWindow() { }
    public abstract IEnumerable<ILayoutElement> Children { get; }
    public abstract int ChildrenCount { get; }
    public abstract bool IsValid { get; }
    public abstract void RemoveChild(ILayoutElement element);
    public abstract void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement);
    public XmlSchema? GetSchema() => null;
    public abstract void ReadXml(XmlReader reader);
    public virtual void WriteXml(XmlWriter writer) => LayoutXml.WriteBody(this, writer);
}
[ContentProperty(Name = nameof(RootDocument))]
public class LayoutDocumentFloatingWindow : LayoutFloatingWindow
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private LayoutDocument? _document;
    public LayoutDocumentFloatingWindow() { }
    public LayoutDocument? RootDocument
    {
        get => _document;
        set { if (ReferenceEquals(value, _document)) return; LayoutTree.ReplaceSlot(this, ref _document, value, nameof(RootDocument)); RootDocumentChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? RootDocumentChanged;
    public override bool IsValid => _document != null;
    public override int ChildrenCount => _document == null ? 0 : 1;
    public override IEnumerable<ILayoutElement> Children { get { if (_document != null) yield return _document; } }
    public override void RemoveChild(ILayoutElement element) { if (ReferenceEquals(element, _document)) RootDocument = null; }
    public override void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    { if (!ReferenceEquals(oldElement, _document) || newElement is not LayoutDocument doc) throw new ArgumentException("Invalid document replacement."); RootDocument = doc; }
    public override void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
}
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
