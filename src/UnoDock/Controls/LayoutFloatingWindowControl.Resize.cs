using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
#if WINDOWS
using ResizeGripBase = Microsoft.UI.Xaml.Controls.ContentControl;
using PointerDeviceType = Microsoft.UI.Input.PointerDeviceType;
#else
using ResizeGripBase = Microsoft.UI.Xaml.Controls.Border;
using PointerDeviceType = Windows.Devices.Input.PointerDeviceType;
#endif

namespace UnoDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private FrameResize? _frameResize;
    private NativeDragClock? _resizeClock;

    private sealed class ResizeGrip : ResizeGripBase
    {
        internal ChromeHit Hit { get; }
        internal ResizeGrip(ChromeHit hit)
        {
            Hit = hit;
#if WINDOWS
            // Native WinUI Border is sealed. Compose it in an extensible control;
            // the transparent child has the same routed hit/capture boundary.
            IsTabStop = false; Padding = new(0); BorderThickness = new(0);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Content = new Border { Background = DockChrome.Transparent };
#endif
            ProtectedCursor = InputSystemCursor.Create(hit switch
            {
                ChromeHit.Left or ChromeHit.Right => InputSystemCursorShape.SizeWestEast,
                ChromeHit.Top or ChromeHit.Bottom => InputSystemCursorShape.SizeNorthSouth,
                ChromeHit.TopLeft or ChromeHit.BottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
                _ => InputSystemCursorShape.SizeNortheastSouthwest
            });
        }
    }
    private sealed class FrameResize(NativeFloatingChrome chrome, Window window, LayoutRoot root, ChromeHit hit,
        Point down, DockRect bounds, DockRect modelBounds, double scale, uint? pointer, ResizeGrip? grip)
    {
        internal readonly NativeFloatingChrome Chrome = chrome;
        internal readonly Window Window = window;
        internal readonly LayoutRoot Root = root;
        internal readonly ChromeHit Hit = hit;
        internal readonly Point Down = down;
        internal readonly DockRect Bounds = bounds, ModelBounds = modelBounds;
        internal readonly double Scale = scale;
        internal readonly uint? Pointer = pointer;
        internal readonly ResizeGrip? Grip = grip;
        internal DockRect? LastWritten;
    }
    private bool CurrentResize(FrameResize resize) => ReferenceEquals(_frameResize, resize) && ReferenceEquals(_nativeChrome, resize.Chrome) &&
        !_hostDisposed && IsEnabled && !IsMaximized && !_minimized && ReferenceEquals(_window, resize.Window) && ReferenceEquals(Model.Root, resize.Root) &&
        ReferenceEquals(resize.Root.Manager?.Layout, resize.Root) && resize.Window.AppWindow.Presenter is OverlappedPresenter { IsResizable: true, State: OverlappedPresenterState.Restored } &&
        Math.Abs(NativeFloatingChrome.ScreenScale(this) - resize.Scale) < .000001;

    // Deterministic actual-host tests use this boundary. Physical-input tests
    // independently exercise the routed entry points without fabricated XAML args.
    private FrameResize? BeginFrameResize(ChromeHit hit, Point screenPoint, uint? pointer = null, ResizeGrip? grip = null)
    {
        if (_frameResize != null || _nativeChrome == null || _window == null || _hostDisposed || !IsEnabled || IsMaximized || _minimized ||
            Model.Root is not LayoutRoot root || _window.AppWindow.Presenter is not OverlappedPresenter { IsResizable: true } ||
            hit is < ChromeHit.Left or > ChromeHit.BottomRight) return null;
        if (!double.IsFinite(screenPoint.X) || !double.IsFinite(screenPoint.Y)) throw new ArgumentOutOfRangeException(nameof(screenPoint));
        CancelCaptionDrag(); root.Manager?.Surface?.CancelDrag();
        if (_frameResize != null || _nativeChrome == null || _window == null || !ReferenceEquals(Model.Root, root)) return null;
        var resize = new FrameResize(_nativeChrome, _window, root, hit, screenPoint, _nativeChrome.ReadBounds(), RestoredBounds,
            NativeFloatingChrome.ScreenScale(this), pointer, grip);
        _frameResize = resize; return resize;
    }
    private void MoveFrameResize(FrameResize resize, Point point)
    {
        // A late delivery from a retired resize cannot cancel its successor.
        if (!ReferenceEquals(_frameResize, resize)) return;
        if (!CurrentResize(resize)) { EndFrameResize(resize, false); return; }
        var scale = resize.Scale;
        var minWidth = Math.Max(160, MinWidth) * scale; var minHeight = Math.Max(100, MinHeight) * scale;
        var bounds = ChromeResize.Apply(resize.Bounds, resize.Hit, point.X - resize.Down.X, point.Y - resize.Down.Y,
            minWidth, minHeight, Math.Max(minWidth, MaxWidth * scale), Math.Max(minHeight, MaxHeight * scale));
        if (resize.LastWritten == bounds) return;
        resize.LastWritten = bounds;
        var moving = _movingFromCaption; _movingFromCaption = true;
        try { resize.Chrome.WriteBounds(bounds); }
        finally { _movingFromCaption = moving; }
    }
    private void EndFrameResize(FrameResize resize, bool restore)
    {
        if (!ReferenceEquals(_frameResize, resize)) return;
        var stillOwned = CurrentResize(resize);
        _frameResize = null; var clock = _resizeClock; _resizeClock = null;
        var cleanup = new DockCleanup(); cleanup.Attempt(() => clock?.Dispose());
        if (restore && stillOwned)
            cleanup.Attempt(() =>
            {
                var moving = _movingFromCaption; _movingFromCaption = true;
                try
                {
                    resize.Chrome.WriteBounds(resize.Bounds);
                    if (_frameResize == null && ReferenceEquals(_nativeChrome, resize.Chrome) && ReferenceEquals(Model.Root, resize.Root))
                        SetBounds(resize.ModelBounds);
                }
                finally { _movingFromCaption = moving; }
            });
        if (resize.Grip is { } grip && !ReferenceEquals(_frameResize?.Grip, grip)) cleanup.Attempt(grip.ReleasePointerCaptures);
        cleanup.ThrowIfFailed();
    }
    private void CancelFrameResize(bool restore) { if (_frameResize is { } resize) EndFrameResize(resize, restore); }
    private void FailFrameResize(Exception error)
    {
        try { CancelFrameResize(true); }
        catch (Exception cleanup) { error = new AggregateException("Floating resize and cleanup failed.", error, cleanup); }
        ReportFilterFailure(error);
    }
    private Point ResizePoint(ResizeGrip grip, PointerRoutedEventArgs e)
    {
        if (_window != null && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && _dragCoordinates.TryGetPointer(_window, out var state))
            return NativeFloatingChrome.NormalizeScreen(state.Position);
        return NativeFloatingChrome.NormalizeScreen(_dragCoordinates.ToScreen(grip, e.GetCurrentPoint(grip).Position));
    }
    private void ResizePressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ResizeGrip grip || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
        try
        {
            var resize = BeginFrameResize(grip.Hit, ResizePoint(grip, e), e.Pointer.PointerId, grip);
            if (resize == null) return;
            if (!grip.CapturePointer(e.Pointer)) { EndFrameResize(resize, false); return; }
            if (!ReferenceEquals(_frameResize, resize)) return;
            e.Handled = true;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) _resizeClock = new NativeDragClock(TickFrameResize, FailFrameResize);
        }
        catch (Exception error) { FailFrameResize(error); }
    }
    private void ResizeMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ResizeGrip grip || _frameResize is not { } resize || resize.Pointer != e.Pointer.PointerId) return;
        try { MoveFrameResize(resize, ResizePoint(grip, e)); e.Handled = true; }
        catch (Exception error) { FailFrameResize(error); }
    }
    private void ResizeReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ResizeGrip grip || _frameResize is not { } resize || resize.Pointer != e.Pointer.PointerId) return;
        try { MoveFrameResize(resize, ResizePoint(grip, e)); EndFrameResize(resize, false); e.Handled = true; }
        catch (Exception error) { FailFrameResize(error); }
    }
    private void ResizeCancelled(object sender, PointerRoutedEventArgs e)
    { if (_frameResize?.Pointer == e.Pointer.PointerId) CancelFrameResize(true); }
    private void TickFrameResize()
    {
        if (_frameResize is not { } resize) return;
        if (!CurrentResize(resize)) { EndFrameResize(resize, false); return; }
        if (!_dragCoordinates.TryGetPointer(resize.Window, out var state)) throw new PlatformNotSupportedException("Native resize pointer state is unavailable.");
        if (state.EscapeDown) { EndFrameResize(resize, true); return; }
        MoveFrameResize(resize, NativeFloatingChrome.NormalizeScreen(state.Position));
        if (!state.LeftDown) EndFrameResize(resize, false);
    }
}
