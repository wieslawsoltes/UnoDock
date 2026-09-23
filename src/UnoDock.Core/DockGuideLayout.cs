namespace UnoDock.Core;

/// <summary>The scope of a docking glyph: the whole workspace, a tab group,
/// or a tool pane placed beside a document group without becoming a document.</summary>
public enum DockGuideScope { Workspace, Pane, ToolBesideDocument }
public readonly record struct DockGuideSlot(DockGuideScope Scope, DockPosition Position, DockRect Bounds);

/// <summary>Independent compass layout. Slots are immutable, non-overlapping,
/// fully inside the visible host, and never scaled below the requested hit size.</summary>
public static class DockGuideLayout
{
    public static IReadOnlyList<DockGuideSlot> Create(DockRect host, DockRect pane,
        bool workspace, bool paneTargets, bool toolTargets, double size = 32, double gap = 4, double inset = 12)
        => CreateCore(host, pane, workspace, paneTargets, toolTargets, size, gap, inset, size, size);

    /// <summary>Stock geometry measured from the pinned reference's public visual tree:
    /// 88-DIP three-cell compass; 32x29 horizontal and 29x32 vertical workspace targets.</summary>
    public static IReadOnlyList<DockGuideSlot> CreateStock(DockRect host, DockRect pane,
        bool workspace, bool paneTargets, bool toolTargets = false, double size = 88d / 3)
        => CreateCore(host, pane, workspace, paneTargets, toolTargets, size, 0, 0, size * 12 / 11, size * 87 / 88);

    private static IReadOnlyList<DockGuideSlot> CreateCore(DockRect host, DockRect pane,
        bool workspace, bool paneTargets, bool toolTargets, double size, double gap, double inset, double rootLong, double rootShort)
    {
        Validate(host); Validate(pane);
        if (!double.IsFinite(size) || size < 16 || size > 128) throw new ArgumentOutOfRangeException(nameof(size));
        if (!double.IsFinite(gap) || gap < 0 || gap > 64) throw new ArgumentOutOfRangeException(nameof(gap));
        if (!double.IsFinite(inset) || inset < 0 || inset > 256) throw new ArgumentOutOfRangeException(nameof(inset));
        var result = new List<DockGuideSlot>(13);
        if (host.Width < size || host.Height < size) return result.AsReadOnly();
        // Workspace guides have precedence when a small pane's compass meets them.
        if (workspace)
        {
            var ix = Math.Max(0, Math.Min(inset, (host.Width - rootLong) / 2));
            var iy = Math.Max(0, Math.Min(inset, (host.Height - rootLong) / 2));
            var midX = host.X + (host.Width - rootShort) / 2;
            var midY = host.Y + (host.Height - rootShort) / 2;
            Add(DockGuideScope.Workspace, DockPosition.Left, new(host.X + ix, midY, rootLong, rootShort), host);
            Add(DockGuideScope.Workspace, DockPosition.Top, new(midX, host.Y + iy, rootShort, rootLong), host);
            Add(DockGuideScope.Workspace, DockPosition.Right, new(host.Right - ix - rootLong, midY, rootLong, rootShort), host);
            Add(DockGuideScope.Workspace, DockPosition.Bottom, new(midX, host.Bottom - iy - rootLong, rootShort, rootLong), host);
        }
        var visible = Intersect(host, pane);
        if (!paneTargets || visible.Width < size || visible.Height < size) return result.AsReadOnly();
        var cx = visible.X + visible.Width / 2 - size / 2;
        var cy = visible.Y + visible.Height / 2 - size / 2;
        var step = size + gap;
        Add(DockGuideScope.Pane, DockPosition.Inside, Cell(0, 0), visible);
        Ring(DockGuideScope.Pane, 1);
        if (toolTargets) Ring(DockGuideScope.ToolBesideDocument, 2);
        return result.AsReadOnly();

        DockRect Cell(int x, int y)
        {
            var left = cx + x * step; var top = cy + y * step;
            // Reuse the exact boundary expression for adjoining fractional-DIP cells.
            // Independently adding size can introduce tiny overlaps at 88/3 DIPs.
            return new(left, top, gap == 0 ? cx + (x + 1) * step - left : size,
                gap == 0 ? cy + (y + 1) * step - top : size);
        }
        void Ring(DockGuideScope scope, int ring)
        {
            Add(scope, DockPosition.Left, Cell(-ring, 0), visible);
            Add(scope, DockPosition.Top, Cell(0, -ring), visible);
            Add(scope, DockPosition.Right, Cell(ring, 0), visible);
            Add(scope, DockPosition.Bottom, Cell(0, ring), visible);
        }
        void Add(DockGuideScope scope, DockPosition position, DockRect rect, DockRect clip)
        {
            if (!Inside(clip, rect) || result.Any(slot => Overlaps(slot.Bounds, rect))) return;
            result.Add(new(scope, position, rect));
        }
    }
    /// <summary>Half-open hit regions prevent shared-edge ambiguities. No nearest-target guessing.</summary>
    public static bool HitTest(DockRect bounds, DockPoint point) =>
        bounds.Width > 0 && bounds.Height > 0 && double.IsFinite(point.X) && double.IsFinite(point.Y) &&
        point.X >= bounds.X && point.X < bounds.Right && point.Y >= bounds.Y && point.Y < bounds.Bottom;
    private static bool Inside(DockRect outer, DockRect inner) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
    private static bool Overlaps(DockRect a, DockRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
    private static DockRect Intersect(DockRect a, DockRect b)
    {
        var x = Math.Max(a.X, b.X); var y = Math.Max(a.Y, b.Y);
        return new(x, y, Math.Max(0, Math.Min(a.Right, b.Right) - x), Math.Max(0, Math.Min(a.Bottom, b.Bottom) - y));
    }
    private static void Validate(DockRect r)
    {
        if (!double.IsFinite(r.X) || !double.IsFinite(r.Y) || !double.IsFinite(r.Width) || !double.IsFinite(r.Height) ||
            !double.IsFinite(r.Right) || !double.IsFinite(r.Bottom) || r.Width < 0 || r.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(r));
    }
}
