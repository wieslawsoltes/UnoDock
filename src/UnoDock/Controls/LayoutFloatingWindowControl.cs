using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
#if WINDOWS
using DockWindowActivationState = Microsoft.UI.Xaml.WindowActivationState;
#else
using DockWindowActivationState = Windows.UI.Core.CoreWindowActivationState;
#endif
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
/// <summary>Compositional replacement for the WPF Window base. Native desktop and in-surface hosts share the same layout.</summary>
public abstract partial class LayoutFloatingWindowControl : DockWindowControl, ILayoutControl
{
    public static readonly DependencyProperty IsContentImmutableProperty = DependencyProperty.Register(nameof(IsContentImmutable), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = DependencyProperty.Register(nameof(IsDragging), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false, (d, e) => ((LayoutFloatingWindowControl)d).OnIsDraggingChanged(e)));
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(nameof(IsMaximized), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false, (d, e) => ((LayoutFloatingWindowControl)d).MaximizedChanged()));
    public static readonly DependencyProperty ResizeBorderThicknessProperty = DependencyProperty.Register(nameof(ResizeBorderThickness), typeof(Thickness), typeof(LayoutFloatingWindowControl), new PropertyMetadata(new Thickness(5)));
    private readonly Grid _frame = new();
    private readonly OverlayWindow _dropOverlay = new();
    private static long _interactionSequence;
    internal long InteractionOrder
    {
        get;
        private set;
    } = System.Threading.Interlocked.Increment(ref _interactionSequence);

    private readonly ContentPresenter _body = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    private readonly TextBlock _caption = new()
    {
        Margin = new Thickness(10, 6, 10, 6),
        VerticalAlignment = VerticalAlignment.Center
    };
    // Icon and title (or DocumentTitleTemplate/AnchorableTitleTemplate) of the
    // caption content. The whole header lets input through to the drag handle.
    private readonly DockHeaderPresenter _captionHeader;
    private readonly Grid _title = new();
    private bool _closingHost, _syncBounds, _hostDisposed;
    private Window? _window;
    private IDisposable? _systemRegistration;
    private bool _closingOperation, _minimized;
    private Window? _pendingNativeSync;
    private NativeWindowMessageHook? _messageHook;
    private DockRect? _displayBounds;
    // The coordinate space the persisted floating bounds were last shown in:
    // desktop DIPs for a native host (true), surface coordinates otherwise.
    private bool? _nativeHostSpace;
    internal bool IsMinimized => _minimized;
    internal bool IsHostDisposed => _hostDisposed;
    /// <summary>True when the persisted bounds are desktop DIPs of a native host.</summary>
    internal bool IsInNativeHostSpace => _nativeHostSpace == true;
    /// <summary>True while this control is presented, natively or in the surface.</summary>
    internal bool IsHostVisible => !_hostDisposed && (_window is { } window ? window.AppWindow.IsVisible : Visibility == Visibility.Visible && VisualTreeHelper.GetParent(this) != null);

    /// <summary>A floating model is kept while hidden content can still return to it;
        /// its host is shown only while it has content to present.</summary>
        internal static bool CanPresent(LayoutFloatingWindow model) => model.IsValid && model is not LayoutAnchorableFloatingWindow { IsVisible: false } && model.Descendents().OfType<LayoutContent>().Any();
    public event EventHandler<Exception>? MessageFilterFailed;
    /// <summary>The caption row; for a single-pane tool window it is also that
        /// pane's title, so it accepts tab insertion like a pane title does.</summary>
        internal FrameworkElement CaptionElement => _title;
    internal double ChromeCaptionHeight
    {
        get => _title.MinHeight;
        set => _title.MinHeight = value;
    }

    internal bool CanPerformSystemAction(Microsoft.Windows.Shell.WindowAction action)
    {
        if (_hostDisposed || _closingOperation)
            return false;
        var presenter = _window?.AppWindow.Presenter as OverlappedPresenter;
        return action switch
        {
            Microsoft.Windows.Shell.WindowAction.Close => CanClose(),
            Microsoft.Windows.Shell.WindowAction.Maximize => !IsMaximized && (presenter?.IsMaximizable ?? true),
            Microsoft.Windows.Shell.WindowAction.Minimize => !_minimized && presenter?.State != OverlappedPresenterState.Minimized && (presenter?.IsMinimizable ?? true),
            Microsoft.Windows.Shell.WindowAction.Restore => IsMaximized || _minimized || presenter?.State == OverlappedPresenterState.Minimized,
            Microsoft.Windows.Shell.WindowAction.Menu => IsLoaded,
            _ => false
        };
    }

    internal void PerformSystemAction(Microsoft.Windows.Shell.WindowAction action)
    {
        if (!CanPerformSystemAction(action))
            return;
        if (action == Microsoft.Windows.Shell.WindowAction.Close)
        {
            Close();
            return;
        }

        CancelFrameResize(true);
        if (_window?.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (action == Microsoft.Windows.Shell.WindowAction.Maximize)
                presenter.Maximize();
            else if (action == Microsoft.Windows.Shell.WindowAction.Minimize)
                presenter.Minimize();
            else if (action == Microsoft.Windows.Shell.WindowAction.Restore)
            {
                _dragCoordinates.PrepareRestore(_window);
                presenter.Restore();
            }

            return;
        }

        if (action == Microsoft.Windows.Shell.WindowAction.Minimize)
            SetMinimized(true);
        else
        {
            SetMinimized(false);
            Visibility = Visibility.Visible;
            if (action == Microsoft.Windows.Shell.WindowAction.Maximize || IsMaximized)
                ToggleMaximize();
        }
    }

    protected bool CloseInitiatedByUser
    {
        get;
        private set;
    }

    protected LayoutFloatingWindowControl(ILayoutElement model) : this(model, false)
    {
    }

    protected LayoutFloatingWindowControl(ILayoutElement model, bool isContentImmutable)
    {
        ArgumentNullException.ThrowIfNull(model);
        SetValue(IsContentImmutableProperty, isContentImmutable);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _frame.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        _frame.RowDefinitions.Add(new()
        {
            Height = new(1, GridUnitType.Star)
        });
        _title.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        _title.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        InitializeCaptionDrag();
        _caption.IsHitTestVisible = false;
        _captionHeader = new(_caption)
        {
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            IconSpacing = 5
        };
        _title.Children.Add(_dragHandle);
        _title.Children.Add(_captionHeader);
        var actions = CreateCaptionButtons();
        Grid.SetColumn(actions, 1);
        _title.Children.Add(actions);
        Grid.SetRow(_body, 1);
        _frame.Children.Add(_title);
        _frame.Children.Add(_body);
        var resize = _legacyResizeGrip = new Thumb
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = .25
        };
        resize.DragDelta += (_, e) => ResizeBy(e.HorizontalChange, e.VerticalChange);
        Grid.SetRow(resize, 1);
        _frame.Children.Add(resize);
        Grid.SetRowSpan(_dropOverlay, 2);
        _frame.Children.Add(_dropOverlay);
        // One retained caption handle moves and docks the whole window. Pane
        // tabs retain their separate single-item tear-off interaction.
        _captionHeader.HorizontalAlignment = HorizontalAlignment.Left;
        GotFocus += (_, _) => MarkInteraction();
        Content = _frame;
        BorderThickness = new(1);
        MinWidth = 160;
        MinHeight = 100;
        InitializeResizeChrome();
        GotFocus += (_, _) =>
        {
            // Focus inside a pane has already activated that pane's content; only a
            // window with no active content activates its first selection.
            if (Contents.Any(c => c.IsActive))
                return;
            if ((Contents.FirstOrDefault(c => c.IsSelected) ?? Contents.FirstOrDefault()) is { } selected)
                selected.IsActive = true;
        };
    }

    public abstract ILayoutElement Model
    {
        get;
    }
    public bool IsContentImmutable
    {
        get => (bool)GetValue(IsContentImmutableProperty);
        private set => SetValue(IsContentImmutableProperty, value);
    }
    public bool IsDragging => (bool)GetValue(IsDraggingProperty);
    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        private set => SetValue(IsMaximizedProperty, value);
    }
    public Thickness ResizeBorderThickness
    {
        get => (Thickness)GetValue(ResizeBorderThicknessProperty);
        set => SetValue(ResizeBorderThicknessProperty, value);
    }
    public Window? NativeWindow => _window;
    internal IEnumerable<LayoutContent> Contents => Model.Descendents().OfType<LayoutContent>();
    private LayoutContent? PositionModel => Contents.FirstOrDefault();
    internal DockRect RestoredBounds => PositionModel is { } p ? new(p.FloatingLeft, p.FloatingTop, p.FloatingWidth > 0 ? Math.Max(160, p.FloatingWidth) : 640, p.FloatingHeight > 0 ? Math.Max(100, p.FloatingHeight) : 480) : new(80, 80, 640, 480);
    internal DockRect Bounds => IsMaximized && _displayBounds is { } bounds ? bounds : RestoredBounds;

    protected virtual bool CanClose(object? parameter = null) => Contents.Any() && Contents.All(c => c.CanClose || c is LayoutAnchorable { CanHide: true });
    protected virtual bool CanHide(object? parameter = null) => Contents.Any() && Contents.All(c => c is LayoutAnchorable { CanHide: true });
    protected virtual void DoHide()
    {
        foreach (var tool in Contents.OfType<LayoutAnchorable>().ToArray())
            tool.Hide();
    }

    protected override void OnInitialized(EventArgs e)
    {
        SetValue(IsMaximizedProperty, PositionModel?.IsMaximized == true);
        base.OnInitialized(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        SetIsDragging(false);
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e) => base.OnClosing(e);
    protected override void OnStateChanged(EventArgs e) => base.OnStateChanged(e);
    protected virtual System.IntPtr FilterMessage(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled) => 0;
    private void MaximizedChanged()
    {
        using ((Model.Root as LayoutRoot)?.BeginUpdate())
            foreach (var content in Contents.ToArray())
                content.IsMaximized = IsMaximized;
        if (!IsMaximized)
            _displayBounds = null;
        ApplyManagedBounds();
        UpdateChromeControls();
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
        OnStateChanged(EventArgs.Empty);
    }

    private void SetMinimized(bool minimized)
    {
        if (_minimized == minimized)
            return;
        _minimized = minimized;
        if (_window == null)
            Visibility = minimized ? Visibility.Collapsed : Visibility.Visible;
        UpdateChromeControls();
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
        OnStateChanged(EventArgs.Empty);
    }

    private void ApplyManagedBounds()
    {
        if (_window != null || _hostDisposed)
            return;
        if (IsMaximized && Model.Root?.Manager?.Surface is { } surface)
            _displayBounds = new(0, 0, Math.Max(160, surface.ActualWidth), Math.Max(100, surface.ActualHeight));
        var bounds = Bounds;
        // Keep in-surface windows reachable when the workspace shrinks or a
        // layout from a larger host is restored; persisted bounds are kept.
        if (!IsMaximized && Model.Root?.Manager?.Surface is { ActualWidth: > 0, ActualHeight: > 0 } host)
            bounds = FloatingPlacement.Fit(bounds, [new DockRect(0, 0, host.ActualWidth, host.ActualHeight)]);
        Width = bounds.Width;
        Height = bounds.Height;
        Canvas.SetLeft(this, bounds.X);
        Canvas.SetTop(this, bounds.Y);
    }

    protected virtual void OnIsDraggingChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    protected void SetIsDragging(bool value) => SetValue(IsDraggingProperty, value);
    public void Close()
    {
        if (_hostDisposed || _closingOperation || !CanClose())
            return;
        _closingOperation = true;
        CloseInitiatedByUser = true;
        try
        {
            var root = Model.Root as LayoutRoot;
            var manager = root?.Manager;
            var args = new CancelEventArgs();
            OnClosing(args);
            if (args.Cancel || !CanClose() || !ReferenceEquals(Model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root))
                return;
            using (root?.BeginUpdate())
                foreach (var content in Contents.ToArray())
                {
                    if (!ReferenceEquals(Model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root))
                        break;
                    if (ReferenceEquals(content.FindParent<LayoutFloatingWindow>(), Model))
                        DockVisuals.CloseOrHide(content);
                }

            root?.CollectGarbage();
            // A tool window whose tools are all hidden stays in the layout (for
            // Show) but its host closes; showing a tool creates a new host.
            if (Model is LayoutFloatingWindow { IsValid: false } or LayoutAnchorableFloatingWindow { IsVisible: false } || Model.Root == null)
                CloseHost();
        }
        finally
        {
            _closingOperation = false;
            CloseInitiatedByUser = false;
        }
    }

    public void Hide()
    {
        if (_hostDisposed || _closingOperation || !CanHide())
            return;
        _closingOperation = true;
        try
        {
            DoHide();
            // Hidden tools keep this model for Show(); never leave an empty host.
            if (!_hostDisposed && Model is LayoutFloatingWindow model && !CanPresent(model))
                HideHost();
        }
        finally
        {
            _closingOperation = false;
        }
    }

    private void MarkInteraction()
    {
        InteractionOrder = System.Threading.Interlocked.Increment(ref _interactionSequence);
        Model.Root?.Manager?.Surface?.RefreshFloatingOrder();
    }

    public void Activate()
    {
        if (_hostDisposed)
            return;
        MarkInteraction();
        if (_window != null)
        {
            if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p)
                p.Restore();
            _window.Activate();
        }
        else
        {
            SetMinimized(false);
            Visibility = Visibility.Visible;
            Focus(FocusState.Programmatic);
        }

        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
    }

    internal void SetChromeBounds(DockRect bounds)
    {
        if (!_hostDisposed)
            SetBounds(bounds);
    }

    internal virtual void UpdateView()
    {
        EnsureInitialized();
        if (_hostDisposed)
            return;
        var manager = Model.Root?.Manager;
        if (manager?.Surface == null)
            return;
        _caption.IsHitTestVisible = false;
        var palette = DockChrome.Palette(manager);
        _frame.RequestedTheme = DockThemeResources.EffectiveTheme(manager);
        _caption.Foreground = palette.Foreground;
        _caption.FontSize = palette.FontSize;
        _caption.Margin = new Thickness(0, 3, 0, 3);
        _captionHeader.Margin = new Thickness(8, 0, 8, 0);
        _captionHeader.Templated.FontSize = palette.FontSize;
        // Keep WindowChrome's explicitly owned caption MinHeight independent.
        _caption.MinHeight = Math.Max(0, palette.TitleHeight - 6);
        var fallback = Model is LayoutDocumentFloatingWindow ? "Document" : "Tools";
        if (CaptionContent is { } captionContent)
            _captionHeader.Update(manager, captionContent, manager.HeaderTemplate(captionContent, _captionHeader, title: true), captionContent.Title ?? fallback);
        else
            _captionHeader.Reset(fallback);
        if (_window != null)
            _window.Title = _caption.Text;
        _frame.Background = palette.Surface;
        PaintCaption(palette);
        UIElement? body = Model switch
        {
            LayoutDocumentFloatingWindow { RootDocument: { } d } => manager.GetLayoutItemFromModel(d).View,
            LayoutAnchorableFloatingWindow { RootPanel: { } p } => manager.Surface.GetView(p),
            _ => null
        };
        if (Model is LayoutAnchorableFloatingWindow { RootPanel: { } panel })
            manager.Surface.UpdateView(panel);
        if (body != null)
        {
            body.Visibility = Visibility.Visible;
            if (!ReferenceEquals(_body.Content, body))
            {
                VisualParenting.Detach(body);
                _body.Content = body;
                VisualParenting.Hosted(_body, body);
            }
        }

        ApplyManagedBounds();
        UpdateChromeControls();
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
    }

    internal void ShowDropPreview(DockDropPlan plan, FrameworkElement coordinateOwner, Brush accent)
    {
        try
        {
            _dropOverlay.ShowPreview(plan, DockCoordinates.Bounds(coordinateOwner, plan.PreviewRect, _dropOverlay, Model.Root?.Manager?.CrossWindowCoordinates), accent);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e))
        {
            _dropOverlay.Hide();
        }
    }

    internal void ShowDropGuides(IReadOnlyList<DockGuideTarget> guides, DockDropPlan? plan, FrameworkElement coordinateOwner, DockingManager manager)
    {
        try
        {
            var local = guides.Select(g => new DockGuideTarget(g.Plan, DockCoordinates.Bounds(coordinateOwner, g.DetectionRect, _dropOverlay, manager.CrossWindowCoordinates))).ToArray();
            Rect? preview = null;
            if (plan != null)
                preview = DockCoordinates.Bounds(coordinateOwner, plan.PreviewRect, _dropOverlay, manager.CrossWindowCoordinates);
            _dropOverlay.ShowGuides(local, plan, manager, preview);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e))
        {
            _dropOverlay.Hide();
        }
    }

    internal void HideDropPreview() => _dropOverlay.Hide();
    internal void ShowNative()
    {
        EnsureInitialized();
        if (_hostDisposed)
            return;
        EnterHostSpace(true);
        Visibility = Visibility.Visible;
        if (_window == null)
        {
            VisualParenting.Detach(this);
            Width = double.NaN;
            Height = double.NaN;
            _window = new Window
            {
                Title = _caption.Text,
                Content = this
            };
            _systemRegistration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Closing += OnNativeClosing;
            _window.AppWindow.Changed += OnNativeChanged;
            _window.Closed += OnNativeClosed;
            _window.Activated += OnNativeActivated;
            // Apply the native frame contract before the first activation. A
            // theme-aware client caption must not flash beneath an OS title bar.
            try
            {
                RefreshNativeChrome();
            }
            catch (Exception error)
            {
                ReportFilterFailure(error);
            }

            var scale = HostScale();
            var bounds = FloatingPlacement.Fit(RestoredBounds, DesktopWindowCoordinates.WorkAreas(scale));
            if (bounds != RestoredBounds && PositionModel?.IsMaximized != true)
                SetBounds(bounds);
            // Physical frame on the monitor receiving the window (its own scale on
            // Windows with mixed DPI; the host scale elsewhere).
            var physical = DesktopWindowCoordinates.DipToPhysical(bounds, scale);
            _syncBounds = true;
            try
            {
                _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = (int)physical.Width, Height = (int)physical.Height });
#if !WINDOWS
                // Persisted floating bounds are top-left screen coordinates on
                // every platform; AppKit frame origins are bottom-left.
                if (OperatingSystem.IsMacOS())
                    MacDesktopInterop.MoveTopLeft(_window, new(bounds.X, bounds.Y));
                else
#endif
                _window.AppWindow.Move(new Windows.Graphics.PointInt32 { X = (int)physical.X, Y = (int)physical.Y });
#if !WINDOWS
                if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        _dragCoordinates.RequestInitialPlacementX11(_window, new(bounds.X * scale, bounds.Y * scale, bounds.Width * scale, bounds.Height * scale));
                    }
                    catch (Exception error) when (DockCoordinates.IsUnavailable(error) || error is InvalidOperationException or DllNotFoundException)
                    {
                        ReportFilterFailure(error);
                    }
                }
#endif
            }
            finally
            {
                _syncBounds = false;
            }

            if (PositionModel?.IsMaximized == true && _window.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();
            try
            {
                _messageHook = NativeWindowMessageHook.Attach(_window, FilterNativeDragMessage, ReportFilterFailure);
            }
            catch (Exception error)
            {
                ReportFilterFailure(error);
            }

            // Own the window and set its tool-window style before it is first shown:
            // Windows creates the taskbar button and X11 window managers read the
            // window type and state when the window maps. The call after showing
            // below remains the fallback when the native window is not ready yet.
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                try
                {
                    ConfigureNativeDragHost();
                }
                catch (Exception error) when (DockCoordinates.IsUnavailable(error) || error is System.Runtime.InteropServices.ExternalException)
                {
                }
            }

            _window.Activate();
            QueueInitialNativeLayout(_window);
        }
        else if (!_window.AppWindow.IsVisible && !_minimized)
        {
#if WINDOWS
            // A host hidden while its tools were hidden is shown at its retained frame.
            _window.AppWindow.Show();
#endif
            _window.Activate();
        }

        try
        {
            ConfigureNativeDragHost();
            RefreshNativeChrome();
        }
        catch (Exception error)
        {
            ReportFilterFailure(error);
        }
    }

    private void OnNativeClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_closingHost)
            return;
        // Prevent native destruction until model cancellation has been evaluated.
        e.Cancel = true;
        var window = _window;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_hostDisposed && ReferenceEquals(_window, window))
                Close();
        });
    }

    private void OnNativeActivated(object sender, WindowActivatedEventArgs e)
    {
        var active = e.WindowActivationState != DockWindowActivationState.Deactivated;
        if (active)
            MarkInteraction();
        SetNativeCaptionActive(active);
    }

    private void ReportFilterFailure(Exception error)
    {
        // Schedule user diagnostics outside the native procedure, and never retain
        // a dead host merely because an observer was queued.
        var weak = new WeakReference<LayoutFloatingWindowControl>(this);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (weak.TryGetTarget(out var owner) && !owner._hostDisposed)
                owner.MessageFilterFailed?.Invoke(owner, error);
        });
    }

    private void OnNativeClosed(object sender, WindowEventArgs e)
    {
        if (!ReferenceEquals(sender, _window))
            return;
        ReleaseNativeChrome();
        ReleaseNativeDragHost(false);
        _messageHook?.Dispose();
        _messageHook = null;
        _systemRegistration?.Dispose();
        _systemRegistration = null;
        if (_window is { } window)
        {
            window.AppWindow.Closing -= OnNativeClosing;
            window.AppWindow.Changed -= OnNativeChanged;
            window.Closed -= OnNativeClosed;
            window.Activated -= OnNativeActivated;
            window.Content = null;
        }

        _window = null;
        CloseHost();
    }

    private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)
    {
        try
        {
            ObserveNativeCaption(e);
        }
        catch (Exception error)
        {
            FailCaptionDrag(error);
        }

        if (_syncBounds || _closingHost || _window is not { } window || ReferenceEquals(_pendingNativeSync, window))
            return;
        // Presenter, bounds and activation notifications can arrive separately.
        // Observe one coherent live snapshot after the native transition settles.
        _pendingNativeSync = window;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(_pendingNativeSync, window))
                _pendingNativeSync = null;
            if (_hostDisposed || _closingHost || !ReferenceEquals(_window, window))
                return;
            var scale = HostScale();
            var native = window.AppWindow;
            var state = (native.Presenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;
            try
            {
                SynchronizeNativeState(PersistedNativeBounds(window, scale), state);
                UpdateChromeControls();
            }
            catch (Exception error) when (DockCoordinates.IsUnavailable(error))
            {
                FailCaptionDrag(error);
            }
        }))
            _pendingNativeSync = null;
    }

    /// <summary>Rasterization scale of this host, falling back to the owning
        /// manager before the new window has a XamlRoot. A 1.0 guess would halve
        /// the first native size on high-density displays.</summary>
        internal double HostScale()
    {
        if (XamlRoot?.RasterizationScale is { } own && double.IsFinite(own) && own > 0)
            return own;
        return Model.Root?.Manager is { } manager ? DesktopWindowCoordinates.Scale(manager) : 1;
    }

    /// <summary>Native frame as persisted model bounds: top-left DIPs on every platform.</summary>
    private DockRect PersistedNativeBounds(Window window, double scale)
    {
        var native = window.AppWindow;
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
        {
            var frame = MacDesktopInterop.TopLeftFrame(window);
            return new(frame.X, frame.Y, native.Size.Width / scale, native.Size.Height / scale);
        }

#endif
        var origin = _dragCoordinates.GetNativeOrigin(window);
        return DesktopWindowCoordinates.PhysicalToDip(new DockRect(origin.X, origin.Y, native.Size.Width, native.Size.Height), scale);
    }

    internal void SynchronizeNativeState(DockRect bounds, OverlappedPresenterState state)
    {
        if (_hostDisposed)
            return;
        if (state == OverlappedPresenterState.Minimized)
        {
            SetMinimized(true);
            return;
        }

        SetMinimized(false);
        var maximized = state == OverlappedPresenterState.Maximized;
        if (maximized)
            _displayBounds = bounds;
        IsMaximized = maximized;
        // Maximized/minimized host geometry is presentation, never persistence.
        if (!maximized)
            SetBounds(bounds);
    }

    internal void HideHost()
    {
        CancelFrameResize(false);
        ReleaseNativeDragHost(false);
#if WINDOWS
        if (_window != null)
            _window.AppWindow.Hide();
#else
        // Uno has no AppWindow.Hide: release the host, retaining the control and content.
        ReleaseNativeWindow();
#endif
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Closes the native window, retaining this control, its content and
        /// its model so the same control can be shown again natively or in-surface.</summary>
        private void ReleaseNativeWindow()
    {
        if (_window is not { } window)
            return;
        CancelFrameResize(false);
        ReleaseNativeDragHost(false);
        _closingHost = true;
        try
        {
            ReleaseNativeChrome();
            _messageHook?.Dispose();
            _messageHook = null;
            window.AppWindow.Closing -= OnNativeClosing;
            window.AppWindow.Changed -= OnNativeChanged;
            window.Closed -= OnNativeClosed;
            window.Activated -= OnNativeActivated;
            DesktopWindowCoordinates.HideNativeClientBeforeClose(window);
            window.Content = null;
            window.Close();
            _systemRegistration?.Dispose();
            _systemRegistration = null;
            _window = null;
        }
        finally
        {
            _closingHost = false;
        }
    }

    /// <summary>Moves this control into the native (desktop DIP) or in-surface
        /// (surface coordinate) host space. When the manager's floating mode changed
        /// since it was last shown, the native window is released and the persisted
        /// origin is converted so the window stays where it appeared.</summary>
        internal void EnterHostSpace(bool native)
    {
        if (_hostDisposed)
            return;
        var previous = _nativeHostSpace;
        Point? origin = null;
        if (previous is { } space && space != native && Model.Root?.Manager is { Surface: { IsLoaded: true } surface } manager && manager.CrossWindowCoordinates is DesktopWindowCoordinates coordinates && PositionModel != null)
        {
            var bounds = RestoredBounds;
            try
            {
                origin = native ? coordinates.ToDesktopPoint(surface, new(bounds.X, bounds.Y)) : coordinates.FromDesktopPoint(new(bounds.X, bounds.Y), surface);
            }
            catch (Exception error) when (DockCoordinates.IsUnavailable(error))
            {
            }
        }

        if (!native)
            ReleaseNativeWindow();
        _nativeHostSpace = native;
        if (origin is { } converted && double.IsFinite(converted.X) && double.IsFinite(converted.Y))
        {
            using (Model.Root is LayoutRoot root ? root.BeginUpdate() : null)
                foreach (var content in Contents.ToArray())
                {
                    content.FloatingLeft = converted.X;
                    content.FloatingTop = converted.Y;
                }
        }

        if (!native)
        {
            ApplyManagedBounds();
            UpdateChromeControls();
        }
    }

    internal void CloseHost()
    {
        if (_hostDisposed)
            return;
        _hostDisposed = true;
        _closingHost = true;
        ReleaseNativeChrome();
        ReleaseNativeDragHost(true);
        Microsoft.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
        var closing = _window;
        if (closing is { } window)
        {
            _messageHook?.Dispose();
            _messageHook = null;
            window.AppWindow.Closing -= OnNativeClosing;
            window.AppWindow.Changed -= OnNativeChanged;
            window.Closed -= OnNativeClosed;
            window.Activated -= OnNativeActivated;
            DesktopWindowCoordinates.HideNativeClientBeforeClose(window);
            window.Content = null;
            _systemRegistration?.Dispose();
            _systemRegistration = null;
            _window = null;
        }

        _dropOverlay.Hide();
        _body.Content = null;
        VisualParenting.Detach(this);
        CompleteWindowClose();
        // Close last: when this is the application's last window, WinUI begins
        // shutting down inside Close and no XAML work may follow it.
        closing?.Close();
    }

    internal void SetBounds(DockRect bounds)
    {
        if (_hostDisposed || IsMaximized || _minimized || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var batch = (Model.Root as LayoutRoot)?.BeginUpdate();
        foreach (var content in Contents)
        {
            content.FloatingLeft = bounds.X;
            content.FloatingTop = bounds.Y;
            content.FloatingWidth = Math.Max(160, bounds.Width);
            content.FloatingHeight = Math.Max(100, bounds.Height);
        }

        if (_window == null)
        {
            Width = Math.Max(160, bounds.Width);
            Height = Math.Max(100, bounds.Height);
            Canvas.SetLeft(this, bounds.X);
            Canvas.SetTop(this, bounds.Y);
        }
    }

    private void MoveBy(double x, double y)
    {
        if (_hostDisposed || IsMaximized || _minimized)
            return;
        if (_window is { } window)
        {
            // Same convention as caption drags: live native origin, AppKit points
            // (bottom-left, Y-up) on macOS and physical pixels elsewhere.
            try
            {
                var origin = _dragCoordinates.GetNativeOrigin(window);
                var scale = OperatingSystem.IsMacOS() ? 1 : DesktopWindowCoordinates.Scale(this);
                var moved = Bounds with
                {
                    X = Bounds.X + x,
                    Y = Bounds.Y + y
                };
                // Persist like a completed caption drag: hosts that report moves
                // asynchronously (X11 ConfigureNotify) must not lose the model bounds.
                _movingFromCaption = true;
                try
                {
                    DesktopWindowCoordinates.MoveNative(window, new(origin.X + x * scale, origin.Y + (OperatingSystem.IsMacOS() ? -y : y) * scale));
                    SetBounds(moved);
                }
                finally
                {
                    _movingFromCaption = false;
                }
            }
            catch (Exception error) when (DockCoordinates.IsUnavailable(error))
            {
                ReportFilterFailure(error);
            }

            return;
        }

        var bounds = Bounds;
        var surface = Model.Root?.Manager?.Surface;
        bounds = bounds with
        {
            X = bounds.X + x,
            Y = bounds.Y + y
        };
        if (surface != null && surface.ActualWidth > 0 && surface.ActualHeight > 0)
            bounds = bounds.ClampTo(new(0, 0, surface.ActualWidth, surface.ActualHeight));
        SetBounds(bounds);
    }

    private void ResizeBy(double x, double y)
    {
        if (_hostDisposed || IsMaximized || _minimized || _window?.AppWindow.Presenter is OverlappedPresenter { IsResizable: false })
            return;
        if (_window != null)
        {
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Math.Max(160, _window.AppWindow.Size.Width + (int)Math.Round(x * (XamlRoot?.RasterizationScale ?? 1))), Height = Math.Max(100, _window.AppWindow.Size.Height + (int)Math.Round(y * (XamlRoot?.RasterizationScale ?? 1))) });
            return;
        }

        var bounds = Bounds;
        SetBounds(bounds with
        {
            Width = Math.Max(160, bounds.Width + x),
            Height = Math.Max(100, bounds.Height + y)
        });
    }

    private void ToggleMaximize()
    {
        if (_window?.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                _dragCoordinates.PrepareRestore(_window);
                presenter.Restore();
            }
            else
                presenter.Maximize();
            return;
        }

        if (Model.Root?.Manager?.Surface is not { } surface)
            return;
        IsMaximized = !IsMaximized;
        ApplyManagedBounds();
    }

    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled)
            HandleWindowKey(e);
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
            HandleWindowKey(e);
    }

    private void HandleWindowKey(KeyRoutedEventArgs e)
    {
        if (_hostDisposed)
            return;
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            // Escape belongs to the content unless it cancels a resize or drag.
            var resizing = _frameResize != null;
            CancelFrameResize(true);
            if (Model.Root?.Manager?.Surface?.CancelTransientSession() == true || resizing)
                e.Handled = true;
            return;
        }

        var manager = Model.Root?.Manager;
        if (InputState.ControlDown && e.Key == Windows.System.VirtualKey.Tab)
        {
            manager?.ShowNavigatorWindow();
            e.Handled = true;
            return;
        }

        if (InputState.ControlDown && e.Key == Windows.System.VirtualKey.F4 && manager?.Layout.ActiveContent is { } active)
        {
            var command = manager.GetLayoutItemFromModel(active).CloseCommand;
            if (command?.CanExecute(null) == true)
                command.Execute(null);
            e.Handled = true;
            return;
        }

        if (manager?.AllowMovingFloatingWindowWithKeyboard != true || !InputState.ControlDown)
            return;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                MoveBy(-10, 0);
                break;
            case Windows.System.VirtualKey.Right:
                MoveBy(10, 0);
                break;
            case Windows.System.VirtualKey.Up:
                MoveBy(0, -10);
                break;
            case Windows.System.VirtualKey.Down:
                MoveBy(0, 10);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
