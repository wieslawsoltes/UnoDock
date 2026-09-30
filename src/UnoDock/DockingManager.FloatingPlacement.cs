using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock;

public partial class DockingManager
{
    /// <summary>Give content that has never floated initial floating bounds over
        /// its current pane (screen DIPs for native windows, surface coordinates for
        /// in-surface windows). Remembered floating bounds are kept.</summary>
        internal void PrepareFloatingBounds(LayoutContent content)
    {
        if (content.FloatingWidth > 0 && content.FloatingHeight > 0 || _surface is not { } surface || !_loaded)
            return;
        var caption = DockChrome.Palette(this).TitleHeight + 2;
        var pane = content.Parent is ILayoutGroup group ? surface.ExistingView(group) : null;
        var bounds = UsesNativeFloatingWindows ? NativeFloatingBounds(pane, caption) : SurfaceFloatingBounds(surface, pane, caption);
        if (bounds is not { } b)
            return;
        content.FloatingLeft = b.X;
        content.FloatingTop = b.Y;
        content.FloatingWidth = b.Width;
        content.FloatingHeight = b.Height;
    }

    private DockRect? NativeFloatingBounds(FrameworkElement? pane, double caption)
    {
        if (CrossWindowCoordinates is not DesktopWindowCoordinates coordinates)
            return null;
        var scale = DesktopWindowCoordinates.Scale(this);
        var areas = DesktopWindowCoordinates.WorkAreas(scale);
        try
        {
            var source = pane is { ActualWidth: > 0, ActualHeight: > 0, XamlRoot: not null } ? pane : this;
            var origin = coordinates.ToDesktopPoint(source, default);
            var rect = source == pane ? new DockRect(origin.X, origin.Y, pane.ActualWidth, pane.ActualHeight) : Centered(new(origin.X, origin.Y, ActualWidth, ActualHeight), 480, 360);
            var placed = FloatingPlacement.FromPane(rect, source == pane ? caption : 0, FloatingPlacement.Choose(rect, areas));
            return FloatingPlacement.Fit(placed, areas);
        }
        catch (Exception error) when (DockCoordinates.IsUnavailable(error))
        {
            return null;
        }
    }

    private DockRect? SurfaceFloatingBounds(DockSurface surface, FrameworkElement? pane, double caption)
    {
        var host = new DockRect(0, 0, surface.ActualWidth, surface.ActualHeight);
        if (!(host.Width > 0 && host.Height > 0))
            return null;
        DockRect rect;
        if (pane is { ActualWidth: > 0, ActualHeight: > 0, XamlRoot: not null } && ReferenceEquals(pane.XamlRoot, surface.XamlRoot))
        {
            var r = pane.TransformToVisual(surface).TransformBounds(new Rect(0, 0, pane.ActualWidth, pane.ActualHeight));
            rect = new(r.X + 16, r.Y + 16, r.Width, r.Height);
        }
        else
            rect = Centered(host, 480, 360);
        return FloatingPlacement.Fit(FloatingPlacement.FromPane(rect, caption, host), [host]);
    }

    private static DockRect Centered(DockRect host, double width, double height) => new(host.X + Math.Max(0, (host.Width - width) / 2), host.Y + Math.Max(0, (host.Height - height) / 2), width, height);
}
