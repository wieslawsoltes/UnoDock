using UnoDock.Controls;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;
// Native WinUI seals Border. Both targets use the same composed visual tree so
// generic Uno runtime tests exercise the arrangement used by the native package.
internal abstract class DockGuideContainer : Panel
{
    protected Border Frame { get; } = new();

    protected DockGuideContainer() => Children.Add(Frame);
    protected override Size MeasureOverride(Size availableSize)
    {
        Frame.Measure(availableSize);
        return Frame.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Frame.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}
