using UnoDock.Compatibility;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
/// <summary>Natural-width tab panel: overflow is handled by its containing ScrollViewer.</summary>
public class DocumentPaneTabPanel : Panel
{
    public DocumentPaneTabPanel()
    {
        PointerExited += (_, native) =>
        {
            var point = native.GetCurrentPoint(this).Position;
            // Exiting a descendant is not exiting the complete header strip.
            if (point.X >= 0 && point.Y >= 0 && point.X < ActualWidth && point.Y < ActualHeight)
                return;
            var args = new DockMouseEventArgs(native, this);
            try
            {
                OnMouseLeave(args);
            }
            finally
            {
                args.Complete();
            }
        };
    }

    protected virtual void OnMouseLeave(DockMouseEventArgs e) => InvalidateMeasure();
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(new(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }
}
