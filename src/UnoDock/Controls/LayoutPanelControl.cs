using UnoDock.Compatibility;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public class LayoutPanelControl(LayoutPanel model) : LayoutGridControl<ILayoutPanelElement>(model)
{
    protected override void OnFixChildrenDockLengths() => NormalizeLengths();
}
