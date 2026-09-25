using UnoDock.Compatibility;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public class LayoutDocumentPaneGroupControl(LayoutDocumentPaneGroup model) : LayoutGridControl<ILayoutDocumentPane>(model)
{
    protected override void OnFixChildrenDockLengths() => NormalizeLengths();
}
