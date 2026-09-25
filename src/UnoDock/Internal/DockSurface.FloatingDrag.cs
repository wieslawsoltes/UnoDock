using System.Diagnostics;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;

internal sealed partial class DockSurface
{
    private FloatingDockSession? _floatingDrag;
    private long _floatingDragGeneration;
    internal bool OwnsFloatingDrag(LayoutFloatingWindowControl window, long generation) =>
        !_disposed && generation == _floatingDragGeneration && ReferenceEquals(_floatingDrag?.Window, window);

    internal long BeginFloatingDrag(LayoutFloatingWindowControl window)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Docking requires its owning UI thread.");
        var previous = _floatingDragGeneration;
        CancelDrag();
        // Capture-loss/IsDraggingChanged callbacks can begin a successor gesture.
        if (_disposed || _floatingDragGeneration != previous + 1 || _floatingDrag != null || _dragSource != null ||
            !ReferenceEquals(window.Model.Root, Manager.Layout) || !window.Contents.Any()) return 0;
        var session = new FloatingDockSession(Manager, window);
        if (!session.IsCurrent) { session.Dispose(); return 0; }
        _floatingDrag = session; _dragContent = session.Representative;
        return ++_floatingDragGeneration;
    }

    internal bool UpdateFloatingDrag(LayoutFloatingWindowControl window, long generation, Point point, bool suppress)
    {
        if (!OwnsFloatingDrag(window, generation)) return false;
        if (_floatingDrag?.IsCurrent != true || !double.IsFinite(point.X) || !double.IsFinite(point.Y))
        { CancelDrag(); return false; }
        _lastDragPoint = point;
        if (suppress) { _dragScrollTimer.Stop(); ShowDragPreview(null); }
        else
        {
            UpdateDragAdorners(point);
            if (OwnsFloatingDrag(window, generation) && !_dragScrollTimer.IsEnabled)
            { _lastScrollTick = Stopwatch.GetTimestamp(); _dragScrollTimer.Start(); }
        }
        return OwnsFloatingDrag(window, generation);
    }

    internal bool CompleteFloatingDrag(LayoutFloatingWindowControl window, long generation, Point point, bool suppress)
    {
        if (!OwnsFloatingDrag(window, generation) || _floatingDrag is not { } session) return false;
        var plan = !suppress && session.IsCurrent ? GetDropPlan(session.Representative, point) : null;
        if (!OwnsFloatingDrag(window, generation)) return false;
        _floatingDrag = null; _dragContent = null;
        var closing = ++_floatingDragGeneration;
        _dragScrollTimer.Stop();
        try
        {
            ShowDragPreview(null);
            window.EndFloatingDragCapture(this, generation, false);
            // Clearing visuals and capture raises application callbacks. A newer
            // gesture, root or source owner withdraws the old one-use intent.
            return !_disposed && closing == _floatingDragGeneration && _floatingDrag == null &&
                _dragSource == null && session.IsCurrent && session.Execute(plan);
        }
        finally { session.Dispose(); }
    }

    private bool AcceptFloatingPlan(DockDropPlan? plan) => plan != null && (_floatingDrag == null || _floatingDrag.CanAccept(plan));
}
