namespace UnoDock.Layout;
public interface ILayoutRoot
{
    DockingManager? Manager { get; }

    LayoutPanel RootPanel { get; }

    LayoutAnchorSide LeftSide { get; }

    LayoutAnchorSide TopSide { get; }

    LayoutAnchorSide RightSide { get; }

    LayoutAnchorSide BottomSide { get; }

    LayoutContent? ActiveContent { get; set; }

    ObservableCollection<LayoutAnchorable> Hidden { get; }

    ObservableCollection<LayoutFloatingWindow> FloatingWindows { get; }

    void CollectGarbage();
}
