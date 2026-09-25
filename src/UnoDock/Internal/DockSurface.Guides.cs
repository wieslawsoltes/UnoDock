using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;
internal sealed partial class DockSurface
{
    internal IReadOnlyList<DockGuideTarget> GetDockingGuides(LayoutContent content, Point point) => BuildGuides(content, FindDropArea(point));
    private IReadOnlyList<DockGuideTarget> BuildGuides(LayoutContent content, IModelDropArea? area)
    {
        if (_disposed || Manager.DockingGuideMode == DockingGuideMode.EdgesOnly || !ReferenceEquals(content.Root, Manager.Layout) || !DockOperations.CanMove(content) || area?.Model is not ILayoutGroup target)
            return[];
        var floating = target.FindParent<LayoutFloatingWindow>();
        var window = floating == null ? null : Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, floating));
        // Native pane glyphs are laid out inside their own physical host, then represented
        // in surface coordinates. Do not clip them to the primary window's client bounds.
        var host = new Rect(0, 0, ActualWidth, ActualHeight);
        if (window != null)
        {
            try
            {
                host = DockCoordinates.Bounds(window, new(0, 0, window.ActualWidth, window.ActualHeight), this, Manager.CrossWindowCoordinates);
            }
            catch (Exception e)when (DockCoordinates.IsUnavailable(e))
            {
                return[];
            }
        }

        var bounds = area.DetectionRect;
        var size = Manager.Resources.TryGetValue("UnoDock.GuideSize", out var v) && v is double d && double.IsFinite(d) ? Math.Clamp(d, 24, 64) : 88d / 3;
        var slots = DockGuideLayout.CreateStock(R(host), R(bounds), window == null && content is LayoutAnchorable, area.Type != DropAreaType.DockingManager, Manager.ShowDocumentPaneToolGuides && content is LayoutAnchorable && target is LayoutDocumentPane, size);
        var result = new List<DockGuideTarget>(slots.Count);
        foreach (var slot in slots)
        {
            var workspace = slot.Scope == DockGuideScope.Workspace;
            var offset = slot.Position switch
            {
                DockPosition.Left => 0,
                DockPosition.Top => 1,
                DockPosition.Right => 2,
                DockPosition.Bottom => 3,
                _ => 4
            };
            var type = workspace ? (DropTargetType)offset : slot.Scope == DockGuideScope.ToolBesideDocument ? (DropTargetType)(15 + offset) : area.Type == DropAreaType.DocumentPane ? (DropTargetType)(4 + offset) : area.Type == DropAreaType.AnchorablePane ? (DropTargetType)(10 + offset) : DropTargetType.DocumentPaneGroupDockInside;
            if (area.Type == DropAreaType.DocumentPaneGroup && !workspace && slot.Position != DockPosition.Inside)
                continue;
            var plan = DockDropPlan.Create(content, workspace ? Manager.Layout.RootPanel : target, type, workspace ? host : bounds);
            if (AcceptFloatingPlan(plan))
                result.Add(new(plan!, new(slot.Bounds.X, slot.Bounds.Y, slot.Bounds.Width, slot.Bounds.Height)));
        }

        return result.AsReadOnly();
        static DockRect R(Rect r) => new(r.X, r.Y, r.Width, r.Height);
    }

    private DockDropPlan? ResolveDrop(LayoutContent content, Point point, out IReadOnlyList<DockGuideTarget> guides)
    {
        var area = FindDropArea(point);
        guides = BuildGuides(content, area);
        foreach (var guide in guides)
            if (guide.HitTest(point))
                return guide.Plan;
        var legacy = area == null ? null : LegacyDropPlan(content, point, area, Manager.DockingGuideMode == DockingGuideMode.GuidesOnly);
        return AcceptFloatingPlan(legacy) ? legacy : null;
    }

    private DockDropPlan? UpdateDragAdorners(Point point)
    {
        if (_dragContent == null)
        {
            ShowDragPreview(null);
            return null;
        }

        var plan = ResolveDrop(_dragContent, point, out var guides);
        var windows = Manager.FloatingWindows.ToArray();
        var local = guides.Where(g => g.Plan.Target.FindParent<LayoutFloatingWindow>()is not { } f || windows.All(w => !ReferenceEquals(w.Model, f) || w.NativeWindow == null)).ToArray();
        var native = plan?.Target.FindParent<LayoutFloatingWindow>()is { } model ? windows.FirstOrDefault(w => ReferenceEquals(w.Model, model) && w.NativeWindow != null) : null;
        _overlay.ShowGuides(local, native == null ? plan : null, Manager);
        foreach (var window in windows)
        {
            if (window.NativeWindow == null)
            {
                window.HideDropPreview();
                continue;
            }

            // Keep the same non-hit-testable guides visible over the moving client.
            if (ReferenceEquals(window, _floatingDrag?.Window))
            {
                window.ShowDropGuides(guides, plan, this, Manager);
                continue;
            }

            var targets = guides.Where(g => ReferenceEquals(g.Plan.Target.FindParent<LayoutFloatingWindow>(), window.Model)).ToArray();
            window.ShowDropGuides(targets, ReferenceEquals(native, window) ? plan : null, this, Manager);
        }

        return plan;
    }
}
