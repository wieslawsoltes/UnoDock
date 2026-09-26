namespace UnoDock.Layout;

public abstract partial class LayoutContent
{
    static LayoutContent()
    {
    }

    public static readonly DependencyProperty ContentProperty = LayoutXamlProperty.Register<LayoutContent, object?>(nameof(Content), null, owner => owner.Content, (owner, value) => owner.Content = value);
    public static readonly DependencyProperty ToolTipProperty = LayoutXamlProperty.Register<LayoutContent, object?>(nameof(ToolTip), null, owner => owner.ToolTip, (owner, value) => owner.ToolTip = value);
    public static readonly DependencyProperty IconSourceProperty = LayoutXamlProperty.Register<LayoutContent, ImageSource?>(nameof(IconSource), null, owner => owner.IconSource, (owner, value) => owner.IconSource = value);
    public static readonly DependencyProperty IsEnabledProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(IsEnabled), true, owner => owner.IsEnabled, (owner, value) => owner.IsEnabled = value);
    public static readonly DependencyProperty CanCloseProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(CanClose), true, owner => owner.CanClose, (owner, value) => owner.CanClose = value);
    public static readonly DependencyProperty CanFloatProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(CanFloat), true, owner => owner.CanFloat, (owner, value) => owner.CanFloat = value);
    public static readonly DependencyProperty IsMaximizedProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(IsMaximized), false, owner => owner.IsMaximized, (owner, value) => owner.IsMaximized = value);
    public static readonly DependencyProperty IsActiveProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(IsActive), false, owner => owner.IsActive, (owner, value) => owner.IsActive = value);
    public static readonly DependencyProperty IsSelectedProperty = LayoutXamlProperty.Register<LayoutContent, bool>(nameof(IsSelected), false, owner => owner.IsSelected, (owner, value) => owner.IsSelected = value);
    public static readonly DependencyProperty FloatingLeftProperty = LayoutXamlProperty.Register<LayoutContent, double>(nameof(FloatingLeft), 0d, owner => owner.FloatingLeft, (owner, value) => owner.FloatingLeft = value);
    public static readonly DependencyProperty FloatingTopProperty = LayoutXamlProperty.Register<LayoutContent, double>(nameof(FloatingTop), 0d, owner => owner.FloatingTop, (owner, value) => owner.FloatingTop = value);
    public static readonly DependencyProperty FloatingWidthProperty = LayoutXamlProperty.Register<LayoutContent, double>(nameof(FloatingWidth), 0d, owner => owner.FloatingWidth, (owner, value) => owner.FloatingWidth = value);
    public static readonly DependencyProperty FloatingHeightProperty = LayoutXamlProperty.Register<LayoutContent, double>(nameof(FloatingHeight), 0d, owner => owner.FloatingHeight, (owner, value) => owner.FloatingHeight = value);
}
