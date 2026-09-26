namespace UnoDock.Layout;

public partial class LayoutAnchorablePaneGroup
{
    static LayoutAnchorablePaneGroup()
    {
    }

    public static readonly DependencyProperty DockWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, GridLength>(nameof(DockWidth), new GridLength(1, GridUnitType.Star), owner => owner.DockWidth, (owner, value) => owner.DockWidth = value);
    public static readonly DependencyProperty DockHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, GridLength>(nameof(DockHeight), new GridLength(1, GridUnitType.Star), owner => owner.DockHeight, (owner, value) => owner.DockHeight = value);
    public static readonly DependencyProperty DockMinWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(DockMinWidth), 25d, owner => owner.DockMinWidth, (owner, value) => owner.DockMinWidth = value);
    public static readonly DependencyProperty DockMinHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(DockMinHeight), 25d, owner => owner.DockMinHeight, (owner, value) => owner.DockMinHeight = value);
    public static readonly DependencyProperty FloatingLeftProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(FloatingLeft), 0d, owner => owner.FloatingLeft, (owner, value) => owner.FloatingLeft = value);
    public static readonly DependencyProperty FloatingTopProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(FloatingTop), 0d, owner => owner.FloatingTop, (owner, value) => owner.FloatingTop = value);
    public static readonly DependencyProperty FloatingWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(FloatingWidth), 0d, owner => owner.FloatingWidth, (owner, value) => owner.FloatingWidth = value);
    public static readonly DependencyProperty FloatingHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, double>(nameof(FloatingHeight), 0d, owner => owner.FloatingHeight, (owner, value) => owner.FloatingHeight = value);
    public static readonly DependencyProperty IsMaximizedProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, bool>(nameof(IsMaximized), false, owner => owner.IsMaximized, (owner, value) => owner.IsMaximized = value);
    public static readonly DependencyProperty CanRepositionItemsProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, bool>(nameof(CanRepositionItems), true, owner => owner.CanRepositionItems, (owner, value) => owner.CanRepositionItems = value);
    public static readonly DependencyProperty AllowDuplicateContentProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, bool>(nameof(AllowDuplicateContent), true, owner => owner.AllowDuplicateContent, (owner, value) => owner.AllowDuplicateContent = value);
    public static readonly DependencyProperty OrientationProperty = LayoutXamlProperty.Register<LayoutAnchorablePaneGroup, Orientation>(nameof(Orientation), Orientation.Horizontal, owner => owner.Orientation, (owner, value) => owner.Orientation = value);
}
