namespace UnoDock.Layout;

public partial class LayoutAnchorablePane
{
    static LayoutAnchorablePane()
    {
    }

    public static readonly DependencyProperty DockWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, GridLength>(nameof(DockWidth), new GridLength(1, GridUnitType.Star), owner => owner.DockWidth, (owner, value) => owner.DockWidth = value);
    public static readonly DependencyProperty DockHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, GridLength>(nameof(DockHeight), new GridLength(1, GridUnitType.Star), owner => owner.DockHeight, (owner, value) => owner.DockHeight = value);
    public static readonly DependencyProperty DockMinWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(DockMinWidth), 25d, owner => owner.DockMinWidth, (owner, value) => owner.DockMinWidth = value);
    public static readonly DependencyProperty DockMinHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(DockMinHeight), 25d, owner => owner.DockMinHeight, (owner, value) => owner.DockMinHeight = value);
    public static readonly DependencyProperty FloatingLeftProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(FloatingLeft), 0d, owner => owner.FloatingLeft, (owner, value) => owner.FloatingLeft = value);
    public static readonly DependencyProperty FloatingTopProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(FloatingTop), 0d, owner => owner.FloatingTop, (owner, value) => owner.FloatingTop = value);
    public static readonly DependencyProperty FloatingWidthProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(FloatingWidth), 0d, owner => owner.FloatingWidth, (owner, value) => owner.FloatingWidth = value);
    public static readonly DependencyProperty FloatingHeightProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, double>(nameof(FloatingHeight), 0d, owner => owner.FloatingHeight, (owner, value) => owner.FloatingHeight = value);
    public static readonly DependencyProperty IsMaximizedProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, bool>(nameof(IsMaximized), false, owner => owner.IsMaximized, (owner, value) => owner.IsMaximized = value);
    public static readonly DependencyProperty CanRepositionItemsProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, bool>(nameof(CanRepositionItems), true, owner => owner.CanRepositionItems, (owner, value) => owner.CanRepositionItems = value);
    public static readonly DependencyProperty AllowDuplicateContentProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, bool>(nameof(AllowDuplicateContent), true, owner => owner.AllowDuplicateContent, (owner, value) => owner.AllowDuplicateContent = value);
    public static readonly DependencyProperty SelectedContentIndexProperty = LayoutXamlProperty.Register<LayoutAnchorablePane, int>(nameof(SelectedContentIndex), -1, owner => owner.SelectedContentIndex, (owner, value) => owner.SelectedContentIndex = value);
}
