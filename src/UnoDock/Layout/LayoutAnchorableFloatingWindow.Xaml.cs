namespace UnoDock.Layout;

public partial class LayoutAnchorableFloatingWindow
{
    static LayoutAnchorableFloatingWindow()
    {
    }

    public static readonly DependencyProperty RootPanelProperty = LayoutXamlProperty.Register<LayoutAnchorableFloatingWindow, LayoutAnchorablePaneGroup?>(nameof(RootPanel), null, owner => owner.RootPanel, (owner, value) => owner.RootPanel = value!);
}
