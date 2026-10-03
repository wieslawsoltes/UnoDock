#if WINDOWS
namespace UnoDock.Layout;
/// <summary>Native WinUI markup: the XAML Binary Format generator cannot encode members
/// declared on generic base classes, so the markup-facing ones are declared here.</summary>
public partial class LayoutAnchorablePaneGroup
{
    private XamlChildList<ILayoutAnchorablePane>? _xamlChildren;
    /// <summary>The children as objects, for the content property in WinUI markup.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public IList<object> XamlChildren => _xamlChildren ??= new(Children);
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.DockWidth"/>
    public new GridLength DockWidth
    {
        get => base.DockWidth;
        set => base.DockWidth = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.DockHeight"/>
    public new GridLength DockHeight
    {
        get => base.DockHeight;
        set => base.DockHeight = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.DockMinWidth"/>
    public new double DockMinWidth
    {
        get => base.DockMinWidth;
        set => base.DockMinWidth = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.DockMinHeight"/>
    public new double DockMinHeight
    {
        get => base.DockMinHeight;
        set => base.DockMinHeight = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.FloatingLeft"/>
    public new double FloatingLeft
    {
        get => base.FloatingLeft;
        set => base.FloatingLeft = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.FloatingTop"/>
    public new double FloatingTop
    {
        get => base.FloatingTop;
        set => base.FloatingTop = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.FloatingWidth"/>
    public new double FloatingWidth
    {
        get => base.FloatingWidth;
        set => base.FloatingWidth = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.FloatingHeight"/>
    public new double FloatingHeight
    {
        get => base.FloatingHeight;
        set => base.FloatingHeight = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.IsMaximized"/>
    public new bool IsMaximized
    {
        get => base.IsMaximized;
        set => base.IsMaximized = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.CanRepositionItems"/>
    public new bool CanRepositionItems
    {
        get => base.CanRepositionItems;
        set => base.CanRepositionItems = value;
    }
    /// <inheritdoc cref = "LayoutPositionableGroup{T}.AllowDuplicateContent"/>
    public new bool AllowDuplicateContent
    {
        get => base.AllowDuplicateContent;
        set => base.AllowDuplicateContent = value;
    }
}
#endif
