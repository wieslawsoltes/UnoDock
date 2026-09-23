namespace Xceed.Wpf.AvalonDock.Controls;

/// <summary>An immutable glyph hit region and its independently validated docking intent.</summary>
public sealed class DockGuideTarget
{
    public DockGuideTarget(DockDropPlan plan, Rect detectionRect)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (!double.IsFinite(detectionRect.X) || !double.IsFinite(detectionRect.Y) ||
            !double.IsFinite(detectionRect.Right) || !double.IsFinite(detectionRect.Bottom) ||
            detectionRect.Width <= 0 || detectionRect.Height <= 0) throw new ArgumentOutOfRangeException(nameof(detectionRect));
        DetectionRect = detectionRect;
    }
    public DockDropPlan Plan { get; }
    public Rect DetectionRect { get; }
    public DropTargetType Type => Plan.Type;
    public bool HitTest(Point point) => Plan.CanExecute && DockGuideLayout.HitTest(
        new(DetectionRect.X, DetectionRect.Y, DetectionRect.Width, DetectionRect.Height), new(point.X, point.Y));
}
