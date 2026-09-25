using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

// Details occupy the observed 54-DIP band but never widen the content-sized list
// columns. Two independently arranged lines avoid the reference's overlapping text.
internal sealed partial class NavigatorDetailsPanel : Panel
{
    private double _lineHeight = 24;
    internal double LineHeight
    {
        get => _lineHeight;
        set { if (_lineHeight == value) return; _lineHeight = value; InvalidateMeasure(); }
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(new Size(availableSize.Width, LineHeight));
        return new Size(0, LineHeight * 2 + 6);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(new Rect(4, 3 + i * LineHeight, Math.Max(0, finalSize.Width - 8), LineHeight));
        return finalSize;
    }
}
