using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using Microsoft.Windows.Shell;

namespace UnoDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private NativeFloatingChrome? _nativeChrome;
    private Window? _chromeWindow;
    private bool _changingChrome;
    private bool _nativeCaptionActive = true;
    private readonly Grid _resizeChrome = new() { Visibility = Visibility.Collapsed };
    private readonly Dictionary<ChromeHit, ResizeGrip> _resizeGrips = [];
    private Thumb? _legacyResizeGrip;
    private Button _dockCaptionButton = null!, _minimizeCaptionButton = null!, _maximizeCaptionButton = null!, _closeCaptionButton = null!;
    private FrameResize? _frameResize;
    private NativeDragClock? _resizeClock;

    /// <summary>True only after a custom decoration adapter has attached to this native host.</summary>
    public bool IsCustomTitleBar => _nativeChrome != null;
    public bool IsResizing => _frameResize != null;

    private StackPanel CreateCaptionButtons()
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        _dockCaptionButton = Add("↙", DockAll, "Dock floating content", "FloatingWindowDock");
        _minimizeCaptionButton = Add("—", () => PerformSystemAction(WindowAction.Minimize), "Minimize floating window", "FloatingWindowMinimize");
        _maximizeCaptionButton = Add("□", () => PerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize),
            "Maximize or restore floating window", "FloatingWindowMaximize");
        _closeCaptionButton = Add("×", Close, "Close floating window", "FloatingWindowClose");
        _minimizeCaptionButton.Visibility = Visibility.Collapsed;
        WindowChrome.SetIsHitTestVisibleInChrome(actions, true);
        return actions;
        Button Add(string glyph, Action command, string help, string id)
        {
            var button = DockVisuals.Button(glyph, command, help);
            AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, help);
            actions.Children.Add(button); return button;
        }
    }

    private void InitializeResizeChrome()
    {
        foreach (var hit in new[] { ChromeHit.Left, ChromeHit.Top, ChromeHit.Right, ChromeHit.Bottom,
                     ChromeHit.TopLeft, ChromeHit.TopRight, ChromeHit.BottomLeft, ChromeHit.BottomRight })
        {
            var grip = new ResizeGrip(hit) { Background = DockChrome.Transparent, Name = "PART_FloatingResize" + hit };
            AutomationProperties.SetAutomationId(grip, "FloatingWindowResize" + hit);
            AutomationProperties.SetName(grip, "Resize floating window " + hit);
            grip.PointerPressed += ResizePressed; grip.PointerMoved += ResizeMoved; grip.PointerReleased += ResizeReleased;
            grip.PointerCanceled += ResizeCancelled; grip.PointerCaptureLost += ResizeCancelled;
            _resizeGrips.Add(hit, grip); _resizeChrome.Children.Add(grip);
        }
        Grid.SetRowSpan(_resizeChrome, 2); _frame.Children.Add(_resizeChrome);
        _dragHandle.DoubleTapped += (_, e) =>
        {
            CancelCaptionDrag(); CancelFrameResize(true);
            PerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize); e.Handled = true;
        };
        _dragHandle.RightTapped += (_, e) =>
        {
            CancelCaptionDrag();
            SystemCommands.CreateSystemMenu(this).ShowAt(_dragHandle, new FlyoutShowOptions { Position = e.GetPosition(_dragHandle) });
            e.Handled = true;
        };
        Unloaded += (_, _) => CancelFrameResize(false);
        IsEnabledChanged += (_, _) => { if (!IsEnabled) CancelFrameResize(true); };
        SizeChanged += (_, _) => UpdateChromeControls();
        RegisterPropertyChangedCallback(ResizeBorderThicknessProperty, (_, _) => UpdateChromeControls());
    }

    internal void RefreshNativeChrome()
    {
        if (_changingChrome || _hostDisposed) return;
        if (_window is not { } window) { UpdateChromeControls(); return; }
        var mode = Model.Root?.Manager?.FloatingWindowTitleBarMode ?? FloatingWindowTitleBarMode.Custom;
        if (ReferenceEquals(window, _chromeWindow) && (mode == FloatingWindowTitleBarMode.Custom) == IsCustomTitleBar)
        { UpdateChromeControls(); return; }
        _changingChrome = true;
        var wasSyncing = _syncBounds; _syncBounds = true;
        try
        {
            CancelCaptionDrag(); CancelFrameResize(true);
            var previous = _nativeChrome; _nativeChrome = null; _chromeWindow = null;
            previous?.Dispose();
            if (mode == FloatingWindowTitleBarMode.Custom)
            {
                var lease = NativeFloatingChrome.Attach(window, this, ReportFilterFailure);
                if (_hostDisposed || !ReferenceEquals(window, _window)) { lease.Release(false); return; }
                _nativeChrome = lease;
            }
            _chromeWindow = window;
            if (Model.Root?.Manager?.FloatingWindowTitleBarMode != mode)
                DispatcherQueue.TryEnqueue(() => { if (!_hostDisposed) RefreshNativeChrome(); });
        }
        finally { _syncBounds = wasSyncing; _changingChrome = false; UpdateChromeControls(); }
    }
    private void ReleaseNativeChrome()
    {
        CancelFrameResize(false);
        var lease = _nativeChrome; _nativeChrome = null; _chromeWindow = null;
        // Closing/hiding a host must not briefly repaint its OS decorations.
        lease?.Release(false);
    }
    private void SetNativeCaptionActive(bool active)
    { _nativeCaptionActive = active; UpdateChromeControls(); }

    private void UpdateChromeControls()
    {
        if (_dockCaptionButton == null) return; // No derived/model calls during construction.
        var custom = IsCustomTitleBar;
        var presenter = _window?.AppWindow.Presenter as OverlappedPresenter;
        var canResize = custom && !_hostDisposed && IsEnabled && !IsMaximized && !_minimized && presenter?.IsResizable == true;
        _resizeChrome.Visibility = canResize ? Visibility.Visible : Visibility.Collapsed;
        if (_legacyResizeGrip != null) _legacyResizeGrip.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        _minimizeCaptionButton.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        _minimizeCaptionButton.IsEnabled = CanPerformSystemAction(WindowAction.Minimize);
        _maximizeCaptionButton.IsEnabled = CanPerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize);
        _closeCaptionButton.IsEnabled = CanPerformSystemAction(WindowAction.Close);
        _dockCaptionButton.IsEnabled = !_hostDisposed && !IsContentImmutable && Contents.Any() && Contents.All(DockOperations.CanMove);
        if (_maximizeCaptionButton.Content is TextBlock glyph) glyph.Text = IsMaximized ? "❐" : "□";
        AutomationProperties.SetName(_maximizeCaptionButton, IsMaximized ? "Restore floating window" : "Maximize floating window");
        if (_window != null && Model.Root?.Manager is { } manager && !_nativeCaptionActive)
            _title.Background = DockChrome.Palette(manager).Header;
        var b = ResizeBorderThickness;
        double Extent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 32) : 5;
        var left = Extent(b.Left); var top = Extent(b.Top); var right = Extent(b.Right); var bottom = Extent(b.Bottom);
        var cw = Math.Max(8, Math.Max(left, right)); var ch = Math.Max(8, Math.Max(top, bottom));
        foreach (var (hit, grip) in _resizeGrips)
        {
            var l = hit is ChromeHit.Left or ChromeHit.TopLeft or ChromeHit.BottomLeft;
            var r = hit is ChromeHit.Right or ChromeHit.TopRight or ChromeHit.BottomRight;
            var t = hit is ChromeHit.Top or ChromeHit.TopLeft or ChromeHit.TopRight;
            var d = hit is ChromeHit.Bottom or ChromeHit.BottomLeft or ChromeHit.BottomRight;
            var corner = (l || r) && (t || d);
            grip.HorizontalAlignment = l ? HorizontalAlignment.Left : r ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            grip.VerticalAlignment = t ? VerticalAlignment.Top : d ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            grip.Width = corner ? cw : l ? left : r ? right : double.NaN;
            grip.Height = corner ? ch : t ? top : d ? bottom : double.NaN;
            grip.Margin = corner ? new(0) : l || r ? new(0, ch, 0, ch) : new(cw, 0, cw, 0);
            grip.Visibility = (l && left > 0 || r && right > 0 || t && top > 0 || d && bottom > 0) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private sealed class ResizeGrip : Border
    {
        internal ChromeHit Hit { get; }
        internal ResizeGrip(ChromeHit hit)
        {
            Hit = hit;
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

    // Also used by deterministic actual-host tests; physical-input suites exercise
    // the routed entry points independently, without synthesizing XAML event args.
    private FrameResize? BeginFrameResize(ChromeHit hit, Point screenPoint, uint? pointer = null, ResizeGrip? grip = null)
    {
        if (_frameResize != null || _nativeChrome == null || _window == null || _hostDisposed || !IsEnabled || IsMaximized || _minimized ||
            Model.Root is not LayoutRoot root || _window.AppWindow.Presenter is not OverlappedPresenter { IsResizable: true } ||
            hit is < ChromeHit.Left or > ChromeHit.BottomRight) return null;
        CancelCaptionDrag(); root.Manager?.Surface?.CancelDrag();
        if (_nativeChrome == null || _window == null || !ReferenceEquals(Model.Root, root)) return null;
        var resize = new FrameResize(_nativeChrome, _window, root, hit, screenPoint, _nativeChrome.ReadBounds(), RestoredBounds,
            NativeFloatingChrome.ScreenScale(this), pointer, grip);
        _frameResize = resize; return resize;
    }
    private void MoveFrameResize(FrameResize resize, Point point)
    {
        if (!CurrentResize(resize)) { CancelFrameResize(false); return; }
        var scale = resize.Scale;
        var minWidth = Math.Max(160, MinWidth) * scale; var minHeight = Math.Max(100, MinHeight) * scale;
        var bounds = ChromeResize.Apply(resize.Bounds, resize.Hit, point.X - resize.Down.X, point.Y - resize.Down.Y,
            minWidth, minHeight, Math.Max(minWidth, MaxWidth * scale), Math.Max(minHeight, MaxHeight * scale));
        if (resize.LastWritten == bounds) return;
        resize.LastWritten = bounds;
        _movingFromCaption = true;
        try { resize.Chrome.WriteBounds(bounds); }
        finally { _movingFromCaption = false; }
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
                _movingFromCaption = true;
                try { resize.Chrome.WriteBounds(resize.Bounds); SetBounds(resize.ModelBounds); }
                finally { _movingFromCaption = false; }
            });
        if (resize.Grip != null) cleanup.Attempt(resize.Grip.ReleasePointerCaptures);
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
            if (!grip.CapturePointer(e.Pointer)) { CancelFrameResize(false); return; }
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
        if (!CurrentResize(resize)) { CancelFrameResize(false); return; }
        if (!_dragCoordinates.TryGetPointer(resize.Window, out var state)) throw new PlatformNotSupportedException("Native resize pointer state is unavailable.");
        if (state.EscapeDown) { EndFrameResize(resize, true); return; }
        MoveFrameResize(resize, NativeFloatingChrome.NormalizeScreen(state.Position));
        if (!state.LeftDown) EndFrameResize(resize, false);
    }
}
