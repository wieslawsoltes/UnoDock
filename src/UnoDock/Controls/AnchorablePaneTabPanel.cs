using UnoDock.Compatibility;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public class AnchorablePaneTabPanel : DocumentPaneTabPanel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = base.MeasureOverride(availableSize);
        return new(Math.Min(natural.Width, availableSize.Width), natural.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = TabStripSolver.Allocate(finalSize.Width, Children.Select(c => c.DesiredSize.Width).ToArray());
        double x = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
            x += widths[i];
        }

        return finalSize;
    }
}
