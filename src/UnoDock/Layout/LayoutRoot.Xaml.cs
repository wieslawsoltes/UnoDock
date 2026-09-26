namespace UnoDock.Layout;

public partial class LayoutRoot
{
    static LayoutRoot()
    {
    }

    public static readonly DependencyProperty RootPanelProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutPanel?>(nameof(RootPanel), null, owner => owner.RootPanel, (owner, value) => owner.RootPanel = value!);
    public static readonly DependencyProperty TopSideProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutAnchorSide?>(nameof(TopSide), null, owner => owner.TopSide, (owner, value) => owner.TopSide = value!);
    public static readonly DependencyProperty RightSideProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutAnchorSide?>(nameof(RightSide), null, owner => owner.RightSide, (owner, value) => owner.RightSide = value!);
    public static readonly DependencyProperty BottomSideProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutAnchorSide?>(nameof(BottomSide), null, owner => owner.BottomSide, (owner, value) => owner.BottomSide = value!);
    public static readonly DependencyProperty LeftSideProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutAnchorSide?>(nameof(LeftSide), null, owner => owner.LeftSide, (owner, value) => owner.LeftSide = value!);
    public static readonly DependencyProperty ActiveContentProperty = LayoutXamlProperty.Register<LayoutRoot, LayoutContent?>(nameof(ActiveContent), null, owner => owner.ActiveContent, (owner, value) => owner.ActiveContent = value!);
}
