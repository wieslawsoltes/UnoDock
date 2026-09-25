using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
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
    private bool _resizeAxesMirrored;
    // Physical window edges are not reading-order content. Keep this layer LTR
    // while the caption, document and tool contents retain their inherited flow.
    private readonly Grid _resizeChrome = new()
    {
        Visibility = Visibility.Collapsed,
        FlowDirection = FlowDirection.LeftToRight
    };
    private readonly Dictionary<ChromeHit, ResizeGrip> _resizeGrips = [];
    private Thumb? _legacyResizeGrip;
    private Button _dockCaptionButton = null!, _minimizeCaptionButton = null!, _maximizeCaptionButton = null!, _closeCaptionButton = null!;
    /// <summary>True only after a custom decoration adapter has attached to this native host.</summary>
    public bool IsCustomTitleBar => _nativeChrome != null;
    public bool IsResizing => _frameResize != null;

    private StackPanel CreateCaptionButtons()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        _dockCaptionButton = Add("↙", DockAll, "Dock floating content", "FloatingWindowDock");
        _minimizeCaptionButton = Add("—", () => PerformSystemAction(WindowAction.Minimize), "Minimize floating window", "FloatingWindowMinimize");
        _maximizeCaptionButton = Add("□", () => PerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize), "Maximize or restore floating window", "FloatingWindowMaximize");
        _closeCaptionButton = Add("×", Close, "Close floating window", "FloatingWindowClose");
        _minimizeCaptionButton.Visibility = Visibility.Collapsed;
        WindowChrome.SetIsHitTestVisibleInChrome(actions, true);
        return actions;
        Button Add(string glyph, Action command, string help, string id)
        {
            var button = DockVisuals.Button(glyph, command, help);
            AutomationProperties.SetAutomationId(button, id);
            AutomationProperties.SetName(button, help);
            actions.Children.Add(button);
            return button;
        }
    }

    private void InitializeResizeChrome()
    {
        foreach (var hit in new[]
        {
            ChromeHit.Left,
            ChromeHit.Top,
            ChromeHit.Right,
            ChromeHit.Bottom,
            ChromeHit.TopLeft,
            ChromeHit.TopRight,
            ChromeHit.BottomLeft,
            ChromeHit.BottomRight
        }

        )
        {
            var grip = new ResizeGrip(hit)
            {
                Background = DockChrome.Transparent,
                Name = "PART_FloatingResize" + hit
            };
            AutomationProperties.SetAutomationId(grip, "FloatingWindowResize" + hit);
            AutomationProperties.SetName(grip, "Resize floating window " + hit);
            grip.PointerPressed += ResizePressed;
            grip.PointerMoved += ResizeMoved;
            grip.PointerReleased += ResizeReleased;
            grip.PointerCanceled += ResizeCancelled;
            grip.PointerCaptureLost += ResizeCancelled;
            _resizeGrips.Add(hit, grip);
            _resizeChrome.Children.Add(grip);
        }

        Grid.SetRowSpan(_resizeChrome, 2);
        _frame.Children.Add(_resizeChrome);
        _resizeChrome.LayoutUpdated += (_, _) => UpdateResizeAxes();
        _dragHandle.DoubleTapped += (_, e) =>
        {
            CancelCaptionDrag();
            CancelFrameResize(true);
            PerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize);
            e.Handled = true;
        };
        _dragHandle.RightTapped += (_, e) =>
        {
            CancelCaptionDrag();
            SystemCommands.CreateSystemMenu(this).ShowAt(_dragHandle, new FlyoutShowOptions { Position = e.GetPosition(_dragHandle) });
            e.Handled = true;
        };
        Unloaded += (_, _) => CancelFrameResize(false);
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
                CancelFrameResize(true);
        };
        SizeChanged += (_, _) => UpdateChromeControls();
        RegisterPropertyChangedCallback(ResizeBorderThicknessProperty, (_, _) => UpdateChromeControls());
    }

    internal void RefreshNativeChrome()
    {
        if (_changingChrome || _hostDisposed)
            return;
        if (_window is not { } window)
        {
            UpdateChromeControls();
            return;
        }

        var mode = Model.Root?.Manager?.FloatingWindowTitleBarMode ?? FloatingWindowTitleBarMode.Custom;
        if (ReferenceEquals(window, _chromeWindow) && (mode == FloatingWindowTitleBarMode.Custom) == IsCustomTitleBar)
        {
            UpdateChromeControls();
            return;
        }

        _changingChrome = true;
        var wasSyncing = _syncBounds;
        _syncBounds = true;
        try
        {
            CancelCaptionDrag();
            CancelFrameResize(true);
            var previous = _nativeChrome;
            _nativeChrome = null;
            _chromeWindow = null;
            previous?.Dispose();
            if (mode == FloatingWindowTitleBarMode.Custom)
            {
                var lease = NativeFloatingChrome.Attach(window, this, ReportFilterFailure);
                if (_hostDisposed || !ReferenceEquals(window, _window))
                {
                    lease.Release(false);
                    return;
                }

                _nativeChrome = lease;
            }

            _chromeWindow = window;
            if (Model.Root?.Manager?.FloatingWindowTitleBarMode != mode)
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_hostDisposed)
                        RefreshNativeChrome();
                });
        }
        finally
        {
            _syncBounds = wasSyncing;
            _changingChrome = false;
            UpdateChromeControls();
        }
    }

    private void ReleaseNativeChrome()
    {
        var cleanup = new DockCleanup();
        cleanup.Attempt(() => CancelFrameResize(false));
        var lease = _nativeChrome;
        _nativeChrome = null;
        _chromeWindow = null;
        // Closing/hiding a host must not briefly repaint its OS decorations.
        cleanup.Attempt(() => lease?.Release(false));
        cleanup.ThrowIfFailed();
    }

    private void SetNativeCaptionActive(bool active)
    {
        _nativeCaptionActive = active;
        UpdateChromeControls();
    }

    private void UpdateResizeAxes()
    {
        if (_hostDisposed || !_resizeChrome.IsLoaded || _resizeChrome.XamlRoot == null)
            return;
        // Uno and native WinUI differ in where they apply inherited RTL transforms.
        // Inspect the actual layer-to-client axes, rather than inferring them from
        // FlowDirection or hard-coding a platform exception. Grip placement itself
        // does not change this transform, so the correction cannot oscillate.
        var transform = _resizeChrome.TransformToVisual(null);
        var origin = transform.TransformPoint(new(0, 0));
        var xAxis = transform.TransformPoint(new(1, 0));
        var mirrored = xAxis.X < origin.X;
        if (_resizeAxesMirrored == mirrored)
            return;
        _resizeAxesMirrored = mirrored;
        UpdateChromeControls();
    }

    private void UpdateChromeControls()
    {
        if (_dockCaptionButton == null)
            return; // No derived/model calls during construction.
        var custom = IsCustomTitleBar;
        var presenter = _window?.AppWindow.Presenter as OverlappedPresenter;
        var canResize = custom && !_hostDisposed && IsEnabled && !IsMaximized && !_minimized && presenter?.IsResizable == true;
        _resizeChrome.Visibility = canResize ? Visibility.Visible : Visibility.Collapsed;
        if (_legacyResizeGrip != null)
            _legacyResizeGrip.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        _minimizeCaptionButton.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        _minimizeCaptionButton.IsEnabled = CanPerformSystemAction(WindowAction.Minimize);
        _maximizeCaptionButton.IsEnabled = CanPerformSystemAction(IsMaximized ? WindowAction.Restore : WindowAction.Maximize);
        _closeCaptionButton.IsEnabled = CanPerformSystemAction(WindowAction.Close);
        _dockCaptionButton.IsEnabled = !_hostDisposed && !IsContentImmutable && Contents.Any() && Contents.All(DockOperations.CanMove);
        // The stock factory uses a string; leave application-supplied content intact.
        if (_maximizeCaptionButton.Content is string and ("□" or "❐"))
            _maximizeCaptionButton.Content = IsMaximized ? "❐" : "□";
        AutomationProperties.SetName(_maximizeCaptionButton, IsMaximized ? "Restore floating window" : "Maximize floating window");
        if (_window != null && Model.Root?.Manager is { } manager)
        {
            var palette = DockChrome.Palette(manager);
            _title.Background = _nativeCaptionActive && Contents.Any(c => c.IsActive) ? palette.ActiveTitle : palette.Header;
        }

        var b = ResizeBorderThickness;
        double Extent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 32) : 5;
        var left = Extent(b.Left);
        var top = Extent(b.Top);
        var right = Extent(b.Right);
        var bottom = Extent(b.Bottom);
        var cw = Math.Max(8, Math.Max(left, right));
        var ch = Math.Max(8, Math.Max(top, bottom));
        foreach (var(hit, grip)in _resizeGrips)
        {
            var l = hit is ChromeHit.Left or ChromeHit.TopLeft or ChromeHit.BottomLeft;
            var r = hit is ChromeHit.Right or ChromeHit.TopRight or ChromeHit.BottomRight;
            var t = hit is ChromeHit.Top or ChromeHit.TopLeft or ChromeHit.TopRight;
            var d = hit is ChromeHit.Bottom or ChromeHit.BottomLeft or ChromeHit.BottomRight;
            var corner = (l || r) && (t || d);
            var physicalLeft = _resizeAxesMirrored ? r : l;
            var physicalRight = _resizeAxesMirrored ? l : r;
            grip.HorizontalAlignment = physicalLeft ? HorizontalAlignment.Left : physicalRight ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            grip.VerticalAlignment = t ? VerticalAlignment.Top : d ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            grip.Width = corner ? cw : l ? left : r ? right : double.NaN;
            grip.Height = corner ? ch : t ? top : d ? bottom : double.NaN;
            // Full-length edges remain reachable when an adjacent side is disabled.
            // Enabled corners are above them in z-order and require BOTH axes.
            grip.Margin = new(0);
            grip.Visibility = IsResizeEdgeEnabled(hit) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
