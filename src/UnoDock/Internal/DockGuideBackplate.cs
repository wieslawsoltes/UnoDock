using UnoDock.Controls;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;
internal sealed class DockGuideBackplate : DockGuideContainer
{
    private readonly Path _shape;
    internal DockGuideBackplate()
    {
        Name = "DockGuideBackplate";
        IsHitTestVisible = false;
        Canvas.SetZIndex(this, 1);
        var points = new Point[]
        {
            new(30, 0),
            new(60, 0),
            new(60, 25),
            new(65, 30),
            new(90, 30),
            new(90, 60),
            new(65, 60),
            new(60, 65),
            new(60, 90),
            new(30, 90),
            new(30, 65),
            new(25, 60),
            new(0, 60),
            new(0, 30),
            new(25, 30),
            new(30, 25)
        };
        var f = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true
        };
        foreach (var point in points.Skip(1))
            f.Segments.Add(new LineSegment { Point = point });
        _shape = new Path
        {
            Data = new PathGeometry
            {
                Figures =
                {
                    f
                }
            },
            StrokeThickness = .8,
            Stretch = Stretch.Fill
        };
        Frame.Child = _shape;
    }

    internal void Paint(DockGuidePalette p)
    {
        _shape.Fill = p.Background;
        _shape.Stroke = p.Border;
    }
}
