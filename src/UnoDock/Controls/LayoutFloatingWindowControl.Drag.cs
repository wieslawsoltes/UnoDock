using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public abstract partial class LayoutFloatingWindowControl
{
    private readonly Border _dragHandle = new()
    {
        Name = "PART_FloatingDragHandle",
        Background = DockChrome.Transparent
    };
    private readonly DesktopWindowCoordinates _dragCoordinates = new();
    private CaptionDrag? _captionDrag;
    private NativeDragClock? _dragClock;
    private IDisposable? _nativeOwnerLease;
    private Window? _nativeOwnerConfiguredWindow;
    private Point? _lastNativeOrigin;
    private bool _nativeMoveLoop, _movingFromCaption;
    private sealed class CaptionDrag(DockSurface surface, long generation, LayoutRoot root, Window? window, Point down, Point origin, DockRect bounds, uint? pointer, bool native)
    {
        internal readonly DockSurface Surface = surface;
        internal readonly long Generation = generation;
        internal readonly LayoutRoot Root = root;
        internal readonly Window? Window = window;
        internal readonly Point Down = down, Origin = origin;
        internal readonly DockRect Bounds = bounds;
        internal readonly uint? Pointer = pointer;
        internal readonly bool Native = native;
        internal bool Started, CompletionQueued;
        internal Point? LastRequestedOrigin;
    }

    private void InitializeCaptionDrag()
    {
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_dragHandle, "FloatingWindowDragHandle");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_dragHandle, "Move and dock floating window");
        Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(_dragHandle, true);
        _dragHandle.PointerPressed += CaptionPressed;
        _dragHandle.PointerMoved += CaptionMoved;
        _dragHandle.PointerReleased += CaptionReleased;
        _dragHandle.PointerCanceled += CaptionCancelled;
        _dragHandle.PointerCaptureLost += CaptionCancelled;
        Unloaded += (_, _) => CancelCaptionDrag();
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
                CancelCaptionDrag();
        };
    }

    private bool Current(CaptionDrag drag) => ReferenceEquals(_captionDrag, drag) && !_hostDisposed && !IsMaximized && !_minimized && ReferenceEquals(_window, drag.Window) && ReferenceEquals(Model.Root, drag.Root) && ReferenceEquals(drag.Root.Manager?.Surface, drag.Surface) && drag.Surface.OwnsFloatingDrag(this, drag.Generation);
    private CaptionDrag? BeginCaptionDrag(Point point, uint? pointer, bool native)
    {
        if (_captionDrag != null || _hostDisposed || IsMaximized || _minimized || !IsEnabled || IsContentImmutable || Model.Root is not LayoutRoot root || root.Manager?.Surface is not { } surface)
            return null;
        var window = _window;
        var origin = window == null ? new Point(Bounds.X, Bounds.Y) : native && _lastNativeOrigin is { } previous ? previous : _dragCoordinates.GetNativeOrigin(window);
        var bounds = Bounds;
        var generation = surface.BeginFloatingDrag(this);
        if (generation == 0)
            return null;
        var drag = new CaptionDrag(surface, generation, root, window, point, origin, bounds, pointer, native);
        _captionDrag = drag;
        if (native)
        {
            drag.Started = true;
            SetIsDragging(true);
            if (!Current(drag))
            {
                CancelCaptionDrag();
                return null;
            }
        }

        return drag;
    }

    private void CaptionPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_dragHandle).Properties.IsLeftButtonPressed)
            return;
        try
        {
            var point = CaptionPoint(e);
            var drag = BeginCaptionDrag(point, e.Pointer.PointerId, false);
            if (drag == null)
                return;
            if (!_dragHandle.CapturePointer(e.Pointer))
            {
                CancelCaptionDrag();
                return;
            }

            e.Handled = true;
            if (_window != null && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
                StartDragClock();
        }
        catch (Exception error)when (DockCoordinates.IsUnavailable(error))
        {
            FailCaptionDrag(error);
        }
    }

    private Point CaptionPoint(PointerRoutedEventArgs e)
    {
        // A routed event can contain client coordinates from before a synchronous
        // window move. Mouse input has an authoritative global position; mapping
        // stale local coordinates through the new origin would feed motion back.
        if (_window is { } window && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse && _dragCoordinates.TryGetPointer(window, out var state))
            return state.Position;
        var point = e.GetCurrentPoint(_dragHandle).Position;
        return _window == null ? DockCoordinates.Translate(_dragHandle, point, Model.Root!.Manager!.Surface!, Model.Root.Manager.CrossWindowCoordinates) : _dragCoordinates.ToScreen(_dragHandle, point);
    }

    private void CaptionMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_captionDrag is not { Pointer: { } pointer } drag || pointer != e.Pointer.PointerId)
            return;
        try
        {
            MoveCaption(drag, CaptionPoint(e), InputState.ControlDown);
            e.Handled = true;
        }
        catch (Exception error)when (DockCoordinates.IsUnavailable(error))
        {
            FailCaptionDrag(error);
        }
    }

    private void MoveCaption(CaptionDrag drag, Point point, bool suppress)
    {
        if (!Current(drag))
        {
            CancelCaptionDrag();
            return;
        }

        var dx = point.X - drag.Down.X;
        var dy = point.Y - drag.Down.Y;
        var scale = drag.Window != null && !OperatingSystem.IsMacOS() ? DesktopWindowCoordinates.Scale(this) : 1;
        if (!drag.Started)
        {
            if (Math.Abs(dx) < 4 * scale && Math.Abs(dy) < 4 * scale)
                return;
            drag.Started = true;
            SetIsDragging(true);
            if (!Current(drag))
                return;
        }

        if (!drag.Native)
        {
            _movingFromCaption = true;
            try
            {
                var origin = new Point(drag.Origin.X + dx, drag.Origin.Y + dy);
                if (drag.LastRequestedOrigin != origin)
                {
                    // Do not flood the native event queue with identical moves
                    // while the pointer is stationary and guides are refreshing.
                    drag.LastRequestedOrigin = origin;
                    if (drag.Window != null)
                        DesktopWindowCoordinates.MoveNative(drag.Window, origin);
                    else
                        SetBounds(drag.Bounds with { X = origin.X, Y = origin.Y });
                }
            }
            finally
            {
                _movingFromCaption = false;
            }
        }

        if (!Current(drag))
            return;
        var surfacePoint = drag.Window == null ? point : _dragCoordinates.FromScreen(point, drag.Surface);
        drag.Surface.UpdateFloatingDrag(this, drag.Generation, surfacePoint, suppress);
    }

    private void CaptionReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_captionDrag is not { Pointer: { } pointer } drag || pointer != e.Pointer.PointerId)
            return;
        try
        {
            var point = CaptionPoint(e);
            MoveCaption(drag, point, InputState.ControlDown);
            if (Current(drag))
                CompleteCaption(drag, point, InputState.ControlDown);
            e.Handled = true;
        }
        catch (Exception error)when (DockCoordinates.IsUnavailable(error))
        {
            FailCaptionDrag(error);
        }
    }

    private void CaptionCancelled(object sender, PointerRoutedEventArgs e)
    {
        if (_captionDrag?.Pointer == e.Pointer.PointerId)
            CancelCaptionDrag();
    }

    private void CompleteCaption(CaptionDrag drag, Point point, bool suppress)
    {
        if (!Current(drag))
            return;
        if (!drag.Started)
        {
            drag.Surface.CancelDrag();
            return;
        }

        var surfacePoint = drag.Window == null ? point : _dragCoordinates.FromScreen(point, drag.Surface);
        drag.Surface.CompleteFloatingDrag(this, drag.Generation, surfacePoint, suppress);
    }

    internal void EndFloatingDragCapture(DockSurface surface, long generation, bool restore)
    {
        if (_captionDrag is not { } drag || !ReferenceEquals(drag.Surface, surface) || drag.Generation != generation)
            return;
        _captionDrag = null;
        _dragClock?.Dispose();
        _dragClock = null;
        try
        {
            if (restore && !_hostDisposed && ReferenceEquals(_window, drag.Window) && ReferenceEquals(Model.Root, drag.Root) && ReferenceEquals(drag.Root.Manager?.Surface, surface) && !IsMaximized && !_minimized)
            {
                _movingFromCaption = true;
                try
                {
                    if (drag.Window != null)
                        DesktopWindowCoordinates.MoveNative(drag.Window, drag.Origin);
                    SetBounds(drag.Bounds);
                }
                finally
                {
                    _movingFromCaption = false;
                }
            }
        }
        finally
        {
            try
            {
                SetIsDragging(false);
            }
            finally
            {
                if (_captionDrag == null)
                    _dragHandle.ReleasePointerCaptures();
            }
        }
    }

    private void CancelCaptionDrag()
    {
        if (_captionDrag is not { } drag)
            return;
        if (drag.Surface.OwnsFloatingDrag(this, drag.Generation))
            drag.Surface.CancelDrag();
        else
            EndFloatingDragCapture(drag.Surface, drag.Generation, false);
    }

    private void FailCaptionDrag(Exception error)
    {
        try
        {
            CancelCaptionDrag();
        }
        catch (Exception cleanup)
        {
            error = new AggregateException("Native drag and cleanup failed.", error, cleanup);
        }

        ReportFilterFailure(error);
    }

    private void StartDragClock()
    {
        if (_dragClock != null || _captionDrag == null)
            return;
        _dragClock = new NativeDragClock(TickCaptionDrag, FailCaptionDrag);
    }

    private void TickCaptionDrag()
    {
        if (_captionDrag is not { } drag || drag.CompletionQueued)
            return;
        if (!Current(drag))
        {
            CancelCaptionDrag();
            return;
        }

        if (drag.Window == null || !_dragCoordinates.TryGetPointer(drag.Window, out var state))
            throw new PlatformNotSupportedException("The active desktop host has no pointer-state adapter.");
        if (state.EscapeDown)
        {
            QueueNativeCompletion(drag, true);
            return;
        }

        if (!state.LeftDown)
        {
            if (!_nativeMoveLoop)
                QueueNativeCompletion(drag, false);
            return;
        }

        MoveCaption(drag, state.Position, state.ControlDown);
    }

    private void QueueNativeCompletion(CaptionDrag drag, bool cancel)
    {
        if (!Current(drag) || drag.CompletionQueued)
            return;
        drag.CompletionQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (!Current(drag))
                return;
            try
            {
                if (cancel || drag.Window == null || !_dragCoordinates.TryGetPointer(drag.Window, out var state) || state.EscapeDown)
                    drag.Surface.CancelDrag();
                else if (state.LeftDown)
                {
                    drag.CompletionQueued = false;
                }
                else
                    CompleteCaption(drag, state.Position, state.ControlDown);
            }
            catch (Exception error)
            {
                FailCaptionDrag(error);
            }
        }))
        {
            drag.CompletionQueued = false;
            CancelCaptionDrag();
        }
    }

    private nint FilterNativeDragMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        var result = FilterMessage(hwnd, msg, wParam, lParam, ref handled);
        if (handled || _hostDisposed || _window == null)
            return result;
        if (msg == 0x231)
        {
            _nativeMoveLoop = true;
            _lastNativeOrigin = _dragCoordinates.GetNativeOrigin(_window);
        }
        else if (msg == 0x216 && _captionDrag == null && _dragCoordinates.TryGetPointer(_window, out var state) && state.LeftDown && !state.EscapeDown)
        {
            if (BeginCaptionDrag(state.Position, null, true)is { } drag)
            {
                StartDragClock();
                MoveCaption(drag, state.Position, state.ControlDown);
            }
        }
        else if (msg == 0x232)
        {
            _nativeMoveLoop = false;
            if (_captionDrag is { Native: true } drag)
                QueueNativeCompletion(drag, false);
        }
        else if (msg == 0x1f && _captionDrag is { } cancelled)
            QueueNativeCompletion(cancelled, true);
        return result;
    }

    private void ObserveNativeCaption(AppWindowChangedEventArgs e)
    {
        if (_window is not { } window || _hostDisposed || _closingHost || _syncBounds || _movingFromCaption)
            return;
        var origin = _dragCoordinates.GetNativeOrigin(window);
        if (_captionDrag == null && !OperatingSystem.IsWindows() && e.DidPositionChange && !e.DidSizeChange && _lastNativeOrigin is { } previous && previous != origin && _dragCoordinates.TryGetPointer(window, out var state) && state.LeftDown && !state.EscapeDown && _dragCoordinates.IsNativeCaption(window, state.Position))
        {
            if (BeginCaptionDrag(state.Position, null, true)is { } drag)
            {
                StartDragClock();
                MoveCaption(drag, state.Position, state.ControlDown);
            }
        }

        if (_captionDrag == null)
            _lastNativeOrigin = origin;
    }

    private void ConfigureNativeDragHost()
    {
        if (_window is not { } window)
            return;
        if (_captionDrag == null)
            _lastNativeOrigin = _dragCoordinates.GetNativeOrigin(window);
        if (ReferenceEquals(_nativeOwnerConfiguredWindow, window) || Model.Root?.Manager is not { } manager)
            return;
        var owner = DesktopWindowCoordinates.WindowFor(manager);
        if (owner == null)
            return;
        _nativeOwnerLease = _dragCoordinates.ConfigureOwner(window, owner, Model is LayoutAnchorableFloatingWindow);
        _nativeOwnerConfiguredWindow = window;
    }

    private void ReleaseNativeDragHost(bool terminal)
    {
        CancelCaptionDrag();
        _dragClock?.Dispose();
        _dragClock = null;
        _nativeMoveLoop = false;
        _lastNativeOrigin = null;
        _nativeOwnerConfiguredWindow = null;
        _nativeOwnerLease?.Dispose();
        _nativeOwnerLease = null;
        if (terminal)
            _dragCoordinates.Dispose();
    }
}
