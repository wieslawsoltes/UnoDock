using System.Xml;

namespace UnoDock.Layout;

[ContentProperty(Name = "Children")]
public class LayoutAnchorSide : LayoutGroup<LayoutAnchorGroup>
{
    private AnchorSide _side;
    public LayoutAnchorSide()
    {
    }

    public AnchorSide Side
    {
        get => _side;
        private set => Set(ref _side, value);
    }

    internal void SetSide(AnchorSide side) => Side = side;
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        base.OnParentChanged(oldValue, newValue);
        if (newValue is LayoutRoot root)
            Side = root.SideOf(this);
    }
}
