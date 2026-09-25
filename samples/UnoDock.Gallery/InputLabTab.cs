using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using UnoDock.Compatibility;
using UnoDock.Controls;

namespace UnoDock.Gallery;
internal sealed class InputLabTab(InputLabState state, Action<string> record) : LayoutDocumentTabItem
{
    protected override void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e)
    {
        record($"Preview left: {Model?.Title}, pointer={e.PointerId}, veto={state.VetoPress}");
        if (state.VetoPress) e.Handled = true;
        base.OnPreviewMouseLeftButtonDown(e);
    }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e)
    { record("Left down: " + Model?.Title); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e)
    {
        record($"Left up: {Model?.Title}, veto={state.VetoDrop}");
        if (state.VetoDrop) e.Handled = true;
        base.OnMouseLeftButtonUp(e);
    }
}
