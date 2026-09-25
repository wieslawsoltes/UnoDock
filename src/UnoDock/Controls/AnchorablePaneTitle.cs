using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;
public class AnchorablePaneTitle : LayoutAnchorableTabItem
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }
    public AnchorablePaneTitle() { }
    protected override void OnMouseLeave(DockMouseEventArgs e) => base.OnMouseLeave(e);
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonDown(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
}
