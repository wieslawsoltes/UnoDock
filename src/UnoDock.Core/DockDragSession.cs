namespace UnoDock.Core;

/// <summary>Single-pointer, allocation-free drag state machine. The host owns capture and coordinates.</summary>
public sealed class DockDragSession
{
    private uint _pointer;
    private DockPoint _origin;
    private bool _document;
    public double Threshold { get; }
    public DockDragState State { get; private set; }
    public DockDropTarget? Target { get; private set; }
    public DockPosition Position { get; private set; }
    public DockDragSession(double threshold = 5)
    {
        if (!double.IsFinite(threshold) || threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        Threshold = threshold;
    }
    public void Arm(uint pointerId, DockPoint origin, bool isDocument)
    {
        Validate(origin);
        _pointer = pointerId; _origin = origin; _document = isDocument;
        State = DockDragState.Armed; Target = null; Position = DockPosition.Inside;
    }
    public bool Move(uint pointerId, DockPoint point, ReadOnlySpan<DockDropTarget> targets)
    {
        if (pointerId != _pointer || State is not (DockDragState.Armed or DockDragState.Dragging)) return false;
        Validate(point);
        if (State == DockDragState.Armed)
        {
            if (double.Hypot(point.X - _origin.X, point.Y - _origin.Y) < Threshold) return false;
            State = DockDragState.Dragging;
        }
        Target = null;
        foreach (var target in targets)
        {
            if ((_document ? !target.AcceptsDocuments : !target.AcceptsAnchorables) || !target.Bounds.Contains(point)) continue;
            if (Target is not { } best || target.Priority > best.Priority ||
                target.Priority == best.Priority && (Area(target) < Area(best) ||
                Area(target) == Area(best) && string.CompareOrdinal(target.Id, best.Id) < 0)) Target = target;
        }
        Position = Target is { } selected ? DockSplitSolver.HitTest(selected.Bounds, point) : DockPosition.Inside;
        return true;
    }
    public bool OwnsPointer(uint pointerId) => pointerId == _pointer && State is DockDragState.Armed or DockDragState.Dragging;
    public bool Commit(uint pointerId)
    {
        if (pointerId != _pointer || State is not (DockDragState.Armed or DockDragState.Dragging)) return false;
        var commit = State == DockDragState.Dragging;
        State = commit ? DockDragState.Committed : DockDragState.Cancelled;
        if (!commit) Target = null;
        return commit;
    }
    public void Cancel() { State = DockDragState.Cancelled; Target = null; Position = DockPosition.Inside; }
    private static double Area(DockDropTarget target) => target.Bounds.Width * target.Bounds.Height;
    private static void Validate(DockPoint point)
    { if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point)); }
}
