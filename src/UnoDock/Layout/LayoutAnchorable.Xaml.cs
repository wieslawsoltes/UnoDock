namespace UnoDock.Layout;

public partial class LayoutAnchorable
{
    static LayoutAnchorable()
    {
    }

    public static readonly DependencyProperty CanHideProperty = LayoutXamlProperty.Register<LayoutAnchorable, bool>(nameof(CanHide), true, owner => owner.CanHide, (owner, value) => owner.CanHide = value);
    public static readonly DependencyProperty CanAutoHideProperty = LayoutXamlProperty.Register<LayoutAnchorable, bool>(nameof(CanAutoHide), true, owner => owner.CanAutoHide, (owner, value) => owner.CanAutoHide = value);
    public static readonly DependencyProperty CanDockAsTabbedDocumentProperty = LayoutXamlProperty.Register<LayoutAnchorable, bool>(nameof(CanDockAsTabbedDocument), true, owner => owner.CanDockAsTabbedDocument, (owner, value) => owner.CanDockAsTabbedDocument = value);
    public static readonly DependencyProperty AutoHideWidthProperty = LayoutXamlProperty.Register<LayoutAnchorable, double>(nameof(AutoHideWidth), 0d, owner => owner.AutoHideWidth, (owner, value) => owner.AutoHideWidth = value);
    public static readonly DependencyProperty AutoHideHeightProperty = LayoutXamlProperty.Register<LayoutAnchorable, double>(nameof(AutoHideHeight), 0d, owner => owner.AutoHideHeight, (owner, value) => owner.AutoHideHeight = value);
    public static readonly DependencyProperty AutoHideMinWidthProperty = LayoutXamlProperty.Register<LayoutAnchorable, double>(nameof(AutoHideMinWidth), 100d, owner => owner.AutoHideMinWidth, (owner, value) => owner.AutoHideMinWidth = value);
    public static readonly DependencyProperty AutoHideMinHeightProperty = LayoutXamlProperty.Register<LayoutAnchorable, double>(nameof(AutoHideMinHeight), 100d, owner => owner.AutoHideMinHeight, (owner, value) => owner.AutoHideMinHeight = value);
    public static readonly DependencyProperty IsVisibleProperty = LayoutXamlProperty.Register<LayoutAnchorable, bool>(nameof(IsVisible), false, owner => owner.IsVisible, (owner, value) => owner.IsVisible = value);
}
