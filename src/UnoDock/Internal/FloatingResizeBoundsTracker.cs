using UnoDock.Core;

namespace UnoDock.Internal;

/// <summary>Bounded ownership ledger for in-flight native frame requests. Native
/// acknowledgements need not arrive synchronously or through the event's cached
/// rectangle. Linux may apply size and position as separate requests.</summary>
internal sealed class FloatingResizeBoundsTracker(DockRect initial, bool splitRequests)
{
    private const int MaximumPending = 64;
    private DockRect _acknowledged = initial;
    private readonly List<DockRect> _pending = [];

    internal bool Observe(DockRect actual)
    {
        // Prefer the most recent full acknowledgement when a drag revisits a
        // rectangle, so old geometry cannot authorize indefinitely stale input.
        for (var i = _pending.Count - 1; i >= 0; i--)
            if (Same(actual, _pending[i]))
            {
                _acknowledged = _pending[i]; _pending.RemoveRange(0, i + 1); return true;
            }
        if (Same(actual, _acknowledged)) return true;
        if (splitRequests)
            for (var i = 0; i < _pending.Count; i++)
            {
                var before = i == 0 ? _acknowledged : _pending[i - 1]; var after = _pending[i];
                // AppWindow.Resize precedes Move on the X11 backend. Also allow
                // position-first WM application; both components must be ours.
                if (Same(actual, new(before.X, before.Y, after.Width, after.Height)) ||
                    Same(actual, new(after.X, after.Y, before.Width, before.Height))) return true;
            }
        return false;
    }
    internal bool Request(DockRect bounds)
    {
        if (_pending.Count == MaximumPending) return false;
        _pending.Add(bounds); return true;
    }
    private static bool Same(DockRect a, DockRect b) => Math.Abs(a.X - b.X) < 0.000001 && Math.Abs(a.Y - b.Y) < 0.000001 &&
        Math.Abs(a.Width - b.Width) < 0.000001 && Math.Abs(a.Height - b.Height) < 0.000001;
}
