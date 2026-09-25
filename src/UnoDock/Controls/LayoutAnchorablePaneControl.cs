using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;
public class LayoutAnchorablePaneControl : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    private readonly LayoutAnchorablePane _model;
    public LayoutAnchorablePaneControl(LayoutAnchorablePane model) { ArgumentNullException.ThrowIfNull(model); _model = model; BindPane(model); }
    public ILayoutElement Model => _model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.AnchorablePaneControlStyle; UpdatePane(_model, surface); }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseRightButtonDown(e); }
    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnGotKeyboardFocus(e); }
}
