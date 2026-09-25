using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;
public class LayoutAnchorableTabItem : LayoutTabItemBase
{
    public LayoutAnchorableTabItem() { }
    protected override void OnMouseEnter(DockMouseEventArgs e) => base.OnMouseEnter(e);
    protected override void OnMouseLeave(DockMouseEventArgs e) => base.OnMouseLeave(e);
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonDown(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
}
