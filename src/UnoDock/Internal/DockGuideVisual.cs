using UnoDock.Controls;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;
internal sealed class DockGuideVisual : DockGuideContainer
{
    private readonly Border _pane, _selection, _title;
    private readonly Path _detail = new()
    {
        StrokeThickness = 1.1,
        Stretch = Stretch.None
    };
    internal DockGuideVisual(DropTargetType type, DockPosition position)
    {
        Name = "Guide_" + type;
        Frame.BorderThickness = new(1);
        Frame.CornerRadius = new(0);
        IsHitTestVisible = false;
        Canvas.SetZIndex(this, 2);
        DockVisuals.SetName(this, "Docking target: " + type);
        var drawing = new Canvas
        {
            Width = 30,
            Height = 30,
            IsHitTestVisible = false
        };
        _pane = new Border
        {
            Width = 22,
            Height = 22,
            BorderThickness = new(1.2),
            CornerRadius = new(.5)
        };
        Canvas.SetLeft(_pane, 4);
        Canvas.SetTop(_pane, 4);
        drawing.Children.Add(_pane);
        var r = DockSplitSolver.Preview(new(5.2, 5.2, 19.6, 19.6), position);
        _selection = new Border
        {
            Width = r.Width,
            Height = r.Height
        };
        Canvas.SetLeft(_selection, r.X);
        Canvas.SetTop(_selection, r.Y);
        drawing.Children.Add(_selection);
        _title = new Border
        {
            Width = 19.6,
            Height = 2.3
        };
        Canvas.SetLeft(_title, 5.2);
        Canvas.SetTop(_title, 5.2);
        drawing.Children.Add(_title);
        var geometry = new PathGeometry();
        void Figure(bool filled, params Point[] points)
        {
            var f = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = filled,
                IsFilled = filled
            };
            foreach (var point in points.Skip(1))
                f.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(f);
        }

        var tool = type <= DropTargetType.DockingManagerDockBottom || type >= DropTargetType.DocumentPaneDockAsAnchorableLeft;
        if (tool)
        {
            switch (position)
            {
                case DockPosition.Left:
                    Figure(true, new(17, 15), new(21, 11), new(21, 19));
                    break;
                case DockPosition.Right:
                    Figure(true, new(13, 15), new(9, 11), new(9, 19));
                    break;
                case DockPosition.Top:
                    Figure(true, new(15, 17), new(11, 21), new(19, 21));
                    break;
                case DockPosition.Bottom:
                    Figure(true, new(15, 13), new(11, 9), new(19, 9));
                    break;
            }
        }
        else if (position == DockPosition.Inside)
        {
            // Two small tab corners distinguish "into group" from a side split.
            Figure(false, new(8, 9), new(8, 22), new(16, 22), new(16, 19), new(25, 19));
            Figure(false, new(16, 22), new(16, 26), new(20, 26), new(20, 22), new(25, 22));
        }
        else if (position is DockPosition.Left or DockPosition.Right)
            Figure(false, new(15, 8), new(15, 24));
        else
            Figure(false, new(6, 15), new(24, 15));
        _detail.Data = geometry;
        drawing.Children.Add(_detail);
        Frame.Child = new Viewbox
        {
            Child = drawing,
            Stretch = Stretch.Fill
        };
    }

    internal void Paint(DockGuidePalette p, bool selected, bool joined)
    {
        Frame.Background = selected ? p.Selection : joined ? DockChrome.Transparent : p.Background;
        Frame.BorderBrush = selected ? p.Ink : joined ? DockChrome.Transparent : p.Border;
        _pane.BorderBrush = p.Ink;
        _pane.Background = p.Window;
        _selection.Background = p.Fill;
        _title.Background = p.Title;
        _detail.Stroke = _detail.Fill = p.Ink;
    }
}
