using System.Xml;

namespace UnoDock.Layout;

public abstract class LayoutPositionableGroup<T> : LayoutGroup<T>, ILayoutPositionableElement where T : class, ILayoutElement
{
    private GridLength _width = new(1, GridUnitType.Star), _height = new(1, GridUnitType.Star);
    private double _minWidth = 25, _minHeight = 25, _left, _top, _floatingWidth, _floatingHeight;
    private bool _maximized, _reposition = true, _duplicates = true;
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
