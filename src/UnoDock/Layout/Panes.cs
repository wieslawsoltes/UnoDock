using System.Xml;

namespace Xceed.Wpf.AvalonDock.Layout;

public abstract class LayoutPositionableGroup<T> : LayoutGroup<T>, ILayoutPositionableElement where T : class, ILayoutElement
{
    private GridLength _width = new(1, GridUnitType.Star), _height = new(1, GridUnitType.Star);
    private double _minWidth = 25, _minHeight = 25, _left, _top, _floatingWidth = 640, _floatingHeight = 480;
    private bool _maximized, _reposition = true, _duplicates;
    public LayoutPositionableGroup() { }
    public GridLength DockWidth { get => _width; set { if (Set(ref _width, value)) OnDockWidthChanged(); } }
    public GridLength DockHeight { get => _height; set { if (Set(ref _height, value)) OnDockHeightChanged(); } }
    public double DockMinWidth { get => _minWidth; set => Set(ref _minWidth, Dimension(value)); }
    public double DockMinHeight { get => _minHeight; set => Set(ref _minHeight, Dimension(value)); }
    public double FloatingLeft { get => _left; set => Set(ref _left, Coordinate(value)); }
    public double FloatingTop { get => _top; set => Set(ref _top, Coordinate(value)); }
    public double FloatingWidth { get => _floatingWidth; set => Set(ref _floatingWidth, Dimension(value)); }
    public double FloatingHeight { get => _floatingHeight; set => Set(ref _floatingHeight, Dimension(value)); }
    public bool IsMaximized { get => _maximized; set => Set(ref _maximized, value); }
    public bool CanRepositionItems { get => _reposition; set => Set(ref _reposition, value); }
    public bool AllowDuplicateContent { get => _duplicates; set => Set(ref _duplicates, value); }
    protected virtual void OnDockWidthChanged() { }
    protected virtual void OnDockHeightChanged() { }
    internal static double Dimension(double value) => double.IsFinite(value) && value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    internal static double Coordinate(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}

/// <summary>Selection is tracked by identity, not by an index that becomes stale during collection edits.</summary>
internal sealed class PaneSelection(ILayoutGroup pane, ILayoutContentSelector selector, Action<string> notify)
{
    private LayoutContent? _selected;
    private bool _changing;
    public LayoutContent? Content => _selected;
    public int Index
    {
        get => _selected == null ? -1 : pane.IndexOfChild(_selected);
        set
        {
            if (value < -1 || value >= pane.ChildrenCount) throw new ArgumentOutOfRangeException(nameof(value));
            Select(value < 0 ? null : (LayoutContent)pane.Children.ElementAt(value));
        }
    }
    private void Select(LayoutContent? value)
    {
        if (_changing || ReferenceEquals(_selected, value)) return;
        _changing = true;
        try
        {
            var previous = _selected; _selected = value;
            if (previous != null) previous.IsSelected = false;
            foreach (var c in pane.Children.OfType<LayoutContent>()) if (!ReferenceEquals(c, value) && c.IsSelected) c.IsSelected = false;
            if (value != null) value.IsSelected = true;
            notify(nameof(ILayoutContentSelector.SelectedContent)); notify(nameof(ILayoutContentSelector.SelectedContentIndex));
        }
        finally { _changing = false; }
    }
    public void CollectionChanged()
    {
        if (_selected != null && pane.IndexOfChild(_selected) >= 0)
        { notify(nameof(ILayoutContentSelector.SelectedContentIndex)); return; }
        Select(pane.Children.OfType<LayoutContent>().FirstOrDefault(c => c.IsSelected && c.IsEnabled)
            ?? pane.Children.OfType<LayoutContent>().FirstOrDefault(c => c.IsEnabled));
    }
}

public class LayoutDocumentPane : LayoutPositionableGroup<LayoutContent>, ILayoutDocumentPane, ILayoutContentSelector
{
    private readonly PaneSelection _selection;
    private bool _showHeader = true;
    public LayoutDocumentPane() => _selection = new(this, this, Notify);
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

public class LayoutAnchorablePane : LayoutPositionableGroup<LayoutAnchorable>, ILayoutAnchorablePane, ILayoutContentSelector
{
    private readonly PaneSelection _selection;
    private string? _name;
    public LayoutAnchorablePane() => _selection = new(this, this, Notify);
    public LayoutAnchorablePane(LayoutAnchorable anchorable) : this() => Children.Add(anchorable);
    public LayoutContent? SelectedContent => _selection.Content;
    public int SelectedContentIndex { get => _selection.Index; set => _selection.Index = value; }
    public int IndexOf(LayoutContent content) => content is LayoutAnchorable a ? Children.IndexOf(a) : -1;
    public string? Name { get => _name; set => Set(ref _name, value); }
    public bool CanClose => Children.Count > 0 && Children.All(c => c.CanClose);
    public bool CanHide => Children.Count > 0 && Children.All(c => c.CanHide);
    public bool IsHostedInFloatingWindow => this.FindParent<LayoutFloatingWindow>() != null;
    public bool IsDirectlyHostedInFloatingWindow => this.FindParent<LayoutAnchorableFloatingWindow>() is { IsSinglePane: true };
    protected override bool GetVisibility() => Children.Count > 0;
    protected override void OnChildrenCollectionChanged() { _selection.CollectionChanged(); base.OnChildrenCollectionChanged(); Notify(nameof(CanClose)); Notify(nameof(CanHide)); }
    protected override void ChildMoved(int oldIndex, int newIndex) { _selection.CollectionChanged(); base.ChildMoved(oldIndex, newIndex); }
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    { base.OnParentChanged(oldValue, newValue); Notify(nameof(IsHostedInFloatingWindow)); Notify(nameof(IsDirectlyHostedInFloatingWindow)); }
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}

public class LayoutPanel : LayoutPositionableGroup<ILayoutPanelElement>, ILayoutPanelElement, ILayoutOrientableGroup
{
    private Orientation _orientation;
    public LayoutPanel() { }
    public LayoutPanel(ILayoutPanelElement firstChild) => Children.Add(firstChild);
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
public class LayoutDocumentPaneGroup : LayoutPositionableGroup<ILayoutDocumentPane>, ILayoutDocumentPane, ILayoutOrientableGroup
{
    private Orientation _orientation;
    public LayoutDocumentPaneGroup() { }
    public LayoutDocumentPaneGroup(LayoutDocumentPane documentPane) => Children.Add(documentPane);
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
public class LayoutAnchorablePaneGroup : LayoutPositionableGroup<ILayoutAnchorablePane>, ILayoutAnchorablePane, ILayoutOrientableGroup
{
    private Orientation _orientation;
    public LayoutAnchorablePaneGroup() { }
    public LayoutAnchorablePaneGroup(LayoutAnchorablePane firstChild) => Children.Add(firstChild);
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    protected override void OnIsVisibleChanged() { base.OnIsVisibleChanged(); (Parent as LayoutAnchorableFloatingWindow)?.RefreshVisibility(); }
    protected override void OnDockWidthChanged() => base.OnDockWidthChanged();
    protected override void OnDockHeightChanged() => base.OnDockHeightChanged();
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
public class LayoutAnchorGroup : LayoutGroup<LayoutAnchorable>, ILayoutPreviousContainer
{
    public LayoutAnchorGroup() { }
    public ILayoutContainer? PreviousContainer { get; internal set; }
    public string? PreviousContainerId { get; internal set; }
    public int PreviousContainerIndex { get; set; }
    protected override bool GetVisibility() => Children.Count > 0;
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
public class LayoutAnchorSide : LayoutGroup<LayoutAnchorGroup>
{
    private AnchorSide _side;
    public LayoutAnchorSide() { }
    public AnchorSide Side { get => _side; private set => Set(ref _side, value); }
    internal void SetSide(AnchorSide side) => Side = side;
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    { base.OnParentChanged(oldValue, newValue); if (newValue is LayoutRoot root) Side = root.SideOf(this); }
}
