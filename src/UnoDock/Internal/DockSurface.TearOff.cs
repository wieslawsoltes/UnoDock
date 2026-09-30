using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;

internal sealed partial class DockSurface
{
    private const double TearOffMargin = 12;
    private const double TearOffCaptionOffset = 10;
    private bool _dragWholePane;
    /// <summary>Continuous tear-off: once a started drag leaves its tab strip (or
        /// immediately for a tool pane title), float the content into a native window
        /// under the pointer and hand the still-pressed gesture to that window. A tab
        /// that is the only content of its floating window moves the window instead.</summary>
        private static readonly bool s_trace = Environment.GetEnvironmentVariable("UNODOCK_INPUT_TRACE") == "1";
    private static void Trace(string message) => Console.Error.WriteLine("TEAROFF " + message);
    private bool TryTearOff(LayoutContent content, Point point)
    {
        if (s_trace)
            Trace($"move content={content.Title} point={point} enabled={Manager.ContinuousTearOff} native={Manager.UsesNativeFloatingWindows} coords={Manager.CrossWindowCoordinates?.GetType().Name} source={_dragSource?.GetType().Name} pane={_dragWholePane}");
        if (!Manager.ContinuousTearOff || !Manager.UsesNativeFloatingWindows || Manager.CrossWindowCoordinates is not DesktopWindowCoordinates coordinates || _dragSource is not { } source)
            return false;
        var pane = content.Parent as ILayoutGroup;
        var paneView = pane == null ? null : GetView(pane) as LayoutCachePaneControl;
        var floatingModel = content.FindParent<LayoutFloatingWindow>();
        var floatingControl = floatingModel == null ? null : Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, floatingModel));
        var soleFloating = floatingControl != null && floatingControl.Contents.Count() == 1;
        if (!_dragWholePane && !soleFloating && paneView?.IsNearTabStrip(point, this, TearOffMargin) != false)
            return false;
        if (_dragWholePane && floatingModel != null)
            return false;
        var sourceWindow = DesktopWindowCoordinates.WindowFor(source);
        // Win32 reads the global pointer; a WinUI host island has no Window object.
        var pointerSource = sourceWindow != null || OperatingSystem.IsWindows() && Manager.HostHandle != 0;
        DesktopPointerState pointer = default;
        var hasPointer = pointerSource && coordinates.TryGetPointer(sourceWindow, out pointer);
        if (s_trace)
            Trace($"leave window={sourceWindow != null} pointer={hasPointer} left={pointer.LeftDown} escape={pointer.EscapeDown} at={pointer.Position}");
        if (!pointerSource || !hasPointer || !pointer.LeftDown || pointer.EscapeDown)
            return false;
        if (soleFloating)
        {
            if (floatingControl!.NativeWindow is not { } window)
                return false;
            var origin = coordinates.GetNativeOrigin(window);
            CancelDrag();
            return floatingControl.BeginPointerDrag(pointer.Position, origin);
        }

        if (!content.CanFloat || !DockOperations.CanMove(content))
            return false;
        var scale = DesktopWindowCoordinates.Scale(source);
        var grab = Grab(coordinates, source, pointer.Position, scale);
        var width = Math.Max(160, paneView?.ActualWidth ?? 300);
        var height = Math.Max(100, paneView?.ActualHeight ?? 240);
        var offsetX = Math.Clamp(grab.OffsetX, 16, Math.Max(16, width - 80));
        var bounds = new DockRect(grab.Pointer.X - offsetX, grab.Pointer.Y - TearOffCaptionOffset, width, height);
        var dragPane = _dragWholePane ? pane as LayoutAnchorablePane : null;
        CancelDrag();
        LayoutFloatingWindow? created;
        if (dragPane != null)
            created = DockOperations.FloatPane(dragPane, bounds);
        else
        {
            content.FloatingLeft = bounds.X;
            content.FloatingTop = bounds.Y;
            content.FloatingWidth = bounds.Width;
            content.FloatingHeight = bounds.Height;
            content.Float();
            created = content.FindParent<LayoutFloatingWindow>();
        }

        if (created == null)
            return false;
        // Create and show the native host now, so the pressed gesture continues
        // in the new window instead of waiting for the deferred render.
        Manager.RenderNow();
        if (Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, created)) is not { NativeWindow: not null } control)
        {
            if (s_trace)
                Trace("floated without a native host");
            return true;
        }

        var started = control.BeginPointerDrag(pointer.Position, NativeOrigin(bounds, scale));
        if (s_trace)
            Trace($"tear-off bounds={bounds} started={started}");
        return true;
    }

    /// <summary>Pointer position in top-left screen DIPs and its horizontal
        /// offset within the grabbed element, so the caption keeps its grip.</summary>
        private static (Point Pointer, double OffsetX) Grab(DesktopWindowCoordinates coordinates, FrameworkElement source, Point pointerNative, double scale)
    {
        Point pointer;
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
            pointer = MacDesktopInterop.ToTopLeft(pointerNative);
        else
#endif
        pointer = new(pointerNative.X / scale, pointerNative.Y / scale);
        double offset;
        try
        {
            offset = coordinates.FromScreen(pointerNative, source).X;
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e))
        {
            offset = 40;
        }

        return (pointer, offset);
    }

    private static Point NativeOrigin(DockRect bounds, double scale)
    {
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
            return new(bounds.X, MacDesktopInterop.PrimaryScreenHeight() - bounds.Y - bounds.Height);
#endif
        return new(Math.Round(bounds.X * scale), Math.Round(bounds.Y * scale));
    }
}
