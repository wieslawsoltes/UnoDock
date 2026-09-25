using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;
public class LayoutDocumentPaneControl : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    private readonly LayoutDocumentPane _model;
    public LayoutDocumentPaneControl(LayoutDocumentPane model) { ArgumentNullException.ThrowIfNull(model); _model = model; BindPane(model); }
    public ILayoutElement Model => _model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.DocumentPaneControlStyle; UpdatePane(_model, surface); }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseRightButtonDown(e); }
    protected override void OnSelectionChanged(SelectionChangedEventArgs e) => base.OnSelectionChanged(e);
    protected override IEnumerator LogicalChildren => base.LogicalChildren;
}
