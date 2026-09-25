using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

// Rotates one live element and exchanges its measure/arrange axes. Unlike a render
// transform on a normal StackPanel this reserves the correct vertical rail length.
internal sealed class DockRotatedLabel : Panel
{
    private bool _vertical;
    private readonly CompositeTransform _rotation = new() { Rotation = 90 };
    internal DockRotatedLabel(UIElement child) => Children.Add(child);
    internal bool Vertical
    {
        get => _vertical;
        set { if (_vertical == value) return; _vertical = value; InvalidateMeasure(); }
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Children[0]; child.Measure(_vertical ? new(availableSize.Height, availableSize.Width) : availableSize);
        return _vertical ? new(child.DesiredSize.Height, child.DesiredSize.Width) : child.DesiredSize;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Children[0];
        if (_vertical)
        {
            child.Arrange(new(0, 0, finalSize.Height, finalSize.Width));
            _rotation.TranslateX = finalSize.Width;
            if (!ReferenceEquals(child.RenderTransform, _rotation)) child.RenderTransform = _rotation;
        }
        else { child.RenderTransform = null; child.Arrange(new(0, 0, finalSize.Width, finalSize.Height)); }
        return finalSize;
    }
}
