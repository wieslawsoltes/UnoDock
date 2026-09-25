using Windows.Foundation;

namespace UnoDock.Gallery;

/// <summary>Compact, independently authored wrapping layout for native sample
/// buttons. Resizing only rearranges existing children; it never replaces commands,
/// automation peers, focus targets or editor instances.</summary>
internal sealed class SampleCommandPanel : Panel
{
    private const double Gap = 5;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : double.PositiveInfinity;
        foreach (var child in Children) child.Measure(new Size(width, double.PositiveInfinity));
        return Layout(width, false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(Math.Max(0, finalSize.Width), true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        var occupied = false;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            var childWidth = Math.Min(width, size.Width);
            if (occupied && x + Gap + childWidth > width)
            {
                usedWidth = Math.Max(usedWidth, x);
                y += rowHeight + Gap;
                x = rowHeight = 0;
                occupied = false;
            }
            if (occupied) x += Gap;
            if (arrange) child.Arrange(new Rect(x, y, childWidth, size.Height));
            x += childWidth;
            rowHeight = Math.Max(rowHeight, size.Height);
            occupied = true;
        }
        return new Size(Math.Max(usedWidth, x), y + rowHeight);
    }
}
