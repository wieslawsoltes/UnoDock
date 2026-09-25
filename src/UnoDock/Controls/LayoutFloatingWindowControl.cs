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
    internal long InteractionOrder { get; private set; } = System.Threading.Interlocked.Increment(ref _interactionSequence);
    private readonly ContentPresenter _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock _caption = new() { Margin = new Thickness(10, 6, 10, 6), VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _title = new();
    private bool _closingHost, _syncBounds, _hostDisposed;
    private Window? _window;
    private IDisposable? _systemRegistration;
    private bool _closingOperation, _minimized;
    private Window? _pendingNativeSync;
    private NativeWindowMessageHook? _messageHook;
    private DockRect? _displayBounds;
    internal bool IsMinimized => _minimized;
    public event EventHandler<Exception>? MessageFilterFailed;
    internal double ChromeCaptionHeight { get => _title.MinHeight; set => _title.MinHeight = value; }
    internal bool CanPerformSystemAction(Microsoft.Windows.Shell.WindowAction action)
    {
        if (_hostDisposed || _closingOperation) return false;
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
        if (!CanPerformSystemAction(action)) return;
        if (action == Microsoft.Windows.Shell.WindowAction.Close) { Close(); return; }
        if (_window?.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (action == Microsoft.Windows.Shell.WindowAction.Maximize) presenter.Maximize();
            else if (action == Microsoft.Windows.Shell.WindowAction.Minimize) presenter.Minimize();
            else if (action == Microsoft.Windows.Shell.WindowAction.Restore) presenter.Restore();
            return;
        }
        if (action == Microsoft.Windows.Shell.WindowAction.Minimize) SetMinimized(true);
        else
        {
            SetMinimized(false); Visibility = Visibility.Visible;
            if (action == Microsoft.Windows.Shell.WindowAction.Maximize || IsMaximized) ToggleMaximize();
        }
    }
    protected bool CloseInitiatedByUser { get; private set; }
    protected LayoutFloatingWindowControl(ILayoutElement model) : this(model, false) { }
    protected LayoutFloatingWindowControl(ILayoutElement model, bool isContentImmutable)
    {
        ArgumentNullException.ThrowIfNull(model); SetValue(IsContentImmutableProperty, isContentImmutable);
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _frame.RowDefinitions.Add(new() { Height = GridLength.Auto }); _frame.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        InitializeCaptionDrag();
        _caption.IsHitTestVisible = false; _title.Children.Add(_dragHandle); _title.Children.Add(_caption);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(DockVisuals.Button("↙", DockAll, "Dock floating content"));
        actions.Children.Add(DockVisuals.Button("□", ToggleMaximize, "Maximize or restore floating window"));
        actions.Children.Add(DockVisuals.Button("×", Close, "Close floating window")); Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(actions, true);
        Grid.SetColumn(actions, 1); _title.Children.Add(actions);
        Grid.SetRow(_body, 1); _frame.Children.Add(_title); _frame.Children.Add(_body);
        var resize = new Thumb { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Opacity = .25 };
        resize.DragDelta += (_, e) => ResizeBy(e.HorizontalChange, e.VerticalChange); Grid.SetRow(resize, 1); _frame.Children.Add(resize);
        Grid.SetRowSpan(_dropOverlay, 2); _frame.Children.Add(_dropOverlay);
        // One retained caption handle moves and docks the whole window. Pane
        // tabs retain their separate single-item tear-off interaction.
        _caption.HorizontalAlignment = HorizontalAlignment.Left;
        GotFocus += (_, _) => MarkInteraction();
        Content = _frame; BorderThickness = new(1); MinWidth = 160; MinHeight = 100;
        GotFocus += (_, _) => { if ((Contents.FirstOrDefault(c => c.IsSelected) ?? Contents.FirstOrDefault()) is { } selected) selected.IsActive = true; };
    }
    public abstract ILayoutElement Model { get; }
    public bool IsContentImmutable { get => (bool)GetValue(IsContentImmutableProperty); private set => SetValue(IsContentImmutableProperty, value); }
    public bool IsDragging => (bool)GetValue(IsDraggingProperty);
    public bool IsMaximized { get => (bool)GetValue(IsMaximizedProperty); private set => SetValue(IsMaximizedProperty, value); }
    public Thickness ResizeBorderThickness { get => (Thickness)GetValue(ResizeBorderThicknessProperty); set => SetValue(ResizeBorderThicknessProperty, value); }
    public Window? NativeWindow => _window;
    internal IEnumerable<LayoutContent> Contents => Model.Descendents().OfType<LayoutContent>();
    private LayoutContent? PositionModel => Contents.FirstOrDefault();
    internal DockRect RestoredBounds => PositionModel is { } p ? new(p.FloatingLeft, p.FloatingTop, p.FloatingWidth > 0 ? Math.Max(160, p.FloatingWidth) : 640, p.FloatingHeight > 0 ? Math.Max(100, p.FloatingHeight) : 480) : new(80, 80, 640, 480);
    internal DockRect Bounds => IsMaximized && _displayBounds is { } bounds ? bounds : RestoredBounds;
    protected virtual bool CanClose(object? parameter = null) => Contents.Any() && Contents.All(c => c.CanClose || c is LayoutAnchorable { CanHide: true });
    protected virtual bool CanHide(object? parameter = null) => Contents.Any() && Contents.All(c => c is LayoutAnchorable { CanHide: true });
    protected virtual void DoHide() { foreach (var tool in Contents.OfType<LayoutAnchorable>().ToArray()) tool.Hide(); }
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
            foreach (var content in Contents.ToArray()) content.IsMaximized = IsMaximized;
        if (!IsMaximized) _displayBounds = null;
        ApplyManagedBounds();
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
        OnStateChanged(EventArgs.Empty);
    }
    private void SetMinimized(bool minimized)
    {
        if (_minimized == minimized) return;
        _minimized = minimized;
        if (_window == null) Visibility = minimized ? Visibility.Collapsed : Visibility.Visible;
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
        OnStateChanged(EventArgs.Empty);
    }
    private void ApplyManagedBounds()
    {
        if (_window != null || _hostDisposed) return;
        if (IsMaximized && Model.Root?.Manager?.Surface is { } surface)
            _displayBounds = new(0, 0, Math.Max(160, surface.ActualWidth), Math.Max(100, surface.ActualHeight));
        var bounds = Bounds;
        Width = bounds.Width; Height = bounds.Height; Canvas.SetLeft(this, bounds.X); Canvas.SetTop(this, bounds.Y);
    }
    protected virtual void OnIsDraggingChanged(DependencyPropertyChangedEventArgs e) { }
    protected void SetIsDragging(bool value) => SetValue(IsDraggingProperty, value);
    public void Close()
    {
        if (_hostDisposed || _closingOperation || !CanClose()) return;
        _closingOperation = true; CloseInitiatedByUser = true;
        try
        {
            var root = Model.Root as LayoutRoot; var manager = root?.Manager;
            var args = new CancelEventArgs(); OnClosing(args);
            if (args.Cancel || !CanClose() || !ReferenceEquals(Model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root)) return;
            using (root?.BeginUpdate())
                foreach (var content in Contents.ToArray())
                {
                    if (!ReferenceEquals(Model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root)) break;
                    if (ReferenceEquals(content.FindParent<LayoutFloatingWindow>(), Model)) DockVisuals.CloseOrHide(content);
                }
            root?.CollectGarbage();
            if (Model is LayoutFloatingWindow { IsValid: false } || Model.Root == null) CloseHost();
        }
        finally { _closingOperation = false; CloseInitiatedByUser = false; }
    }
    public void Hide()
    {
        if (_hostDisposed || _closingOperation || !CanHide()) return;
        _closingOperation = true;
        try { DoHide(); } finally { _closingOperation = false; }
    }
    private void MarkInteraction() { InteractionOrder = System.Threading.Interlocked.Increment(ref _interactionSequence); Model.Root?.Manager?.Surface?.RefreshFloatingOrder(); }
    public void Activate()
    {
        if (_hostDisposed) return;
        MarkInteraction();
        if (_window != null)
        {
            if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p) p.Restore();
            _window.Activate();
        }
        else { SetMinimized(false); Visibility = Visibility.Visible; Focus(FocusState.Programmatic); }
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
    }
    internal void SetChromeBounds(DockRect bounds) { if (!_hostDisposed) SetBounds(bounds); }
    private void DockAll()
    {
        var root = Model.Root as LayoutRoot;
        using (root?.BeginUpdate()) foreach (var content in Contents.ToArray()) content.Dock();
    }
    internal virtual void UpdateView()
    {
        EnsureInitialized();
        if (_hostDisposed) return;
        var manager = Model.Root?.Manager; if (manager?.Surface == null) return;
        _caption.IsHitTestVisible = false;
        var palette = DockChrome.Palette(manager);
        _frame.RequestedTheme = DockThemeResources.EffectiveTheme(manager);
        _caption.Foreground = palette.Foreground; _caption.FontSize = palette.FontSize;
        _caption.Margin = new Thickness(8, 3, 8, 3);
        foreach (var button in _title.FindVisualChildren<Button>())
        { button.Foreground = palette.Foreground; button.FontSize = palette.FontSize; button.MinHeight = 0; button.MinWidth = 0; button.Padding = new Thickness(7, 2, 7, 2); }
        _caption.Text = Contents.FirstOrDefault(c => c.IsActive)?.Title ?? Contents.FirstOrDefault()?.Title ?? "Floating tools";
        if (_window != null) _window.Title = _caption.Text;
        _frame.Background = palette.Surface;
        _title.Background = Contents.Any(c => c.IsActive) ? palette.ActiveTitle : palette.Header;
        BorderBrush = palette.Border;
        UIElement? body = Model switch
        {
            LayoutDocumentFloatingWindow { RootDocument: { } d } => manager.GetLayoutItemFromModel(d).View,
            LayoutAnchorableFloatingWindow { RootPanel: { } p } => manager.Surface.GetView(p),
            _ => null
        };
        if (Model is LayoutAnchorableFloatingWindow { RootPanel: { } panel }) manager.Surface.UpdateView(panel);
        if (body != null) { body.Visibility = Visibility.Visible; if (!ReferenceEquals(_body.Content, body)) { VisualParenting.Detach(body); _body.Content = body; } }
        ApplyManagedBounds();
        Microsoft.Windows.Shell.SystemCommands.InvalidateCommands();
    }
    internal void ShowDropPreview(DockDropPlan plan, FrameworkElement coordinateOwner, Brush accent)
    {
        try { _dropOverlay.ShowPreview(plan, DockCoordinates.Bounds(coordinateOwner, plan.PreviewRect, _dropOverlay, Model.Root?.Manager?.CrossWindowCoordinates), accent); }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { _dropOverlay.Hide(); }
    }
    internal void ShowDropGuides(IReadOnlyList<DockGuideTarget> guides, DockDropPlan? plan, FrameworkElement coordinateOwner, DockingManager manager)
    {
        try
        {
            var local = guides.Select(g => new DockGuideTarget(g.Plan, DockCoordinates.Bounds(coordinateOwner,
                g.DetectionRect, _dropOverlay, manager.CrossWindowCoordinates))).ToArray();
            Rect? preview = null;
            if (plan != null) preview = DockCoordinates.Bounds(coordinateOwner, plan.PreviewRect, _dropOverlay, manager.CrossWindowCoordinates);
            _dropOverlay.ShowGuides(local, plan, manager, preview);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { _dropOverlay.Hide(); }
    }
    internal void HideDropPreview() => _dropOverlay.Hide();
    internal void ShowNative()
    {
        EnsureInitialized();
        if (_hostDisposed) return;
        Visibility = Visibility.Visible;
        if (_window == null)
        {
            VisualParenting.Detach(this); Width = double.NaN; Height = double.NaN;
            _window = new Window { Title = _caption.Text, Content = this };
            _systemRegistration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Closing += OnNativeClosing;
            _window.AppWindow.Changed += OnNativeChanged;
            _window.Closed += OnNativeClosed;
            _window.Activated += OnNativeActivated;
            var bounds = RestoredBounds; var scale = DesktopWindowCoordinates.Scale(this);
            _syncBounds = true;
            try { _window.AppWindow.Move(new Windows.Graphics.PointInt32 { X = (int)(bounds.X * scale), Y = (int)(bounds.Y * scale) }); _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = (int)(bounds.Width * scale), Height = (int)(bounds.Height * scale) }); }
            finally { _syncBounds = false; }
            if (PositionModel?.IsMaximized == true && _window.AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
            try { _messageHook = NativeWindowMessageHook.Attach(_window, FilterNativeDragMessage, ReportFilterFailure); }
            catch (Exception error) { ReportFilterFailure(error); }
            _window.Activate();
        }
        else if (!_window.AppWindow.IsVisible && !_minimized) _window.Activate();
        try { ConfigureNativeDragHost(); }
        catch (Exception error) { ReportFilterFailure(error); }
    }
    private void OnNativeClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_closingHost) return;
        // Prevent native destruction until model cancellation has been evaluated.
        e.Cancel = true;
        var window = _window;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_hostDisposed && ReferenceEquals(_window, window)) Close();
        });
    }
    private void OnNativeActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != DockWindowActivationState.Deactivated)
            MarkInteraction();
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
        if (!ReferenceEquals(sender, _window)) return;
        ReleaseNativeDragHost(false);
        _messageHook?.Dispose(); _messageHook = null;
        _systemRegistration?.Dispose(); _systemRegistration = null;
        if (_window is { } window)
        {
            window.AppWindow.Closing -= OnNativeClosing; window.AppWindow.Changed -= OnNativeChanged;
            window.Closed -= OnNativeClosed; window.Activated -= OnNativeActivated; window.Content = null;
        }
        _window = null;
        CloseHost();
    }
    private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)
    {
        try { ObserveNativeCaption(e); }
        catch (Exception error) { FailCaptionDrag(error); }
        if (_syncBounds || _closingHost || _window is not { } window || ReferenceEquals(_pendingNativeSync, window)) return;
        // Presenter, bounds and activation notifications can arrive separately.
        // Observe one coherent live snapshot after the native transition settles.
        _pendingNativeSync = window;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(_pendingNativeSync, window)) _pendingNativeSync = null;
            if (_hostDisposed || _closingHost || !ReferenceEquals(_window, window)) return;
            var scale = DesktopWindowCoordinates.Scale(this);
            var native = window.AppWindow;
            var state = (native.Presenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;
            try
            {
                var origin = _dragCoordinates.GetNativeOrigin(window);
                var positionScale = OperatingSystem.IsMacOS() ? 1 : scale;
                SynchronizeNativeState(new(origin.X / positionScale, origin.Y / positionScale,
                    native.Size.Width / scale, native.Size.Height / scale), state);
            }
            catch (Exception error) when (DockCoordinates.IsUnavailable(error)) { FailCaptionDrag(error); }
        })) _pendingNativeSync = null;
    }
    internal void SynchronizeNativeState(DockRect bounds, OverlappedPresenterState state)
    {
        if (_hostDisposed) return;
        if (state == OverlappedPresenterState.Minimized) { SetMinimized(true); return; }
        SetMinimized(false);
        var maximized = state == OverlappedPresenterState.Maximized;
        if (maximized) _displayBounds = bounds;
        IsMaximized = maximized;
        // Maximized/minimized host geometry is presentation, never persistence.
        if (!maximized) SetBounds(bounds);
    }
    internal void HideHost()
    {
        ReleaseNativeDragHost(false);
#if WINDOWS
        if (_window != null) _window.AppWindow.Hide();
#else
        // Uno has no AppWindow.Hide: release the host, retaining the control and content.
        if (_window is { } window)
        {
            _closingHost = true;
            try
            {
                _messageHook?.Dispose(); _messageHook = null;
                window.AppWindow.Closing -= OnNativeClosing;
                window.AppWindow.Changed -= OnNativeChanged;
                window.Closed -= OnNativeClosed; window.Activated -= OnNativeActivated;
                DesktopWindowCoordinates.HideNativeClientBeforeClose(window);
                window.Content = null;
                window.Close();
                _systemRegistration?.Dispose(); _systemRegistration = null;
                _window = null;
            }
            finally { _closingHost = false; }
        }
#endif
        Visibility = Visibility.Collapsed;
    }
    internal void CloseHost()
    {
        if (_hostDisposed) return; _hostDisposed = true; _closingHost = true;
        ReleaseNativeDragHost(true);
        Microsoft.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
        if (_window is { } window)
        {
            _messageHook?.Dispose(); _messageHook = null;
            window.AppWindow.Closing -= OnNativeClosing; window.AppWindow.Changed -= OnNativeChanged; window.Closed -= OnNativeClosed; window.Activated -= OnNativeActivated;
            DesktopWindowCoordinates.HideNativeClientBeforeClose(window);
            window.Content = null; window.Close(); _systemRegistration?.Dispose(); _systemRegistration = null; _window = null;
        }
        _dropOverlay.Hide(); _body.Content = null; VisualParenting.Detach(this); CompleteWindowClose();
    }
    internal void SetBounds(DockRect bounds)
    {
        if (_hostDisposed || IsMaximized || _minimized || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
            !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0) return;
        using var batch = (Model.Root as LayoutRoot)?.BeginUpdate();
        foreach (var content in Contents)
        { content.FloatingLeft = bounds.X; content.FloatingTop = bounds.Y; content.FloatingWidth = Math.Max(160, bounds.Width); content.FloatingHeight = Math.Max(100, bounds.Height); }
        if (_window == null) { Width = Math.Max(160, bounds.Width); Height = Math.Max(100, bounds.Height); Canvas.SetLeft(this, bounds.X); Canvas.SetTop(this, bounds.Y); }
    }
    private void MoveBy(double x, double y)
    {
        if (_hostDisposed || IsMaximized || _minimized) return;
        if (_window != null) { _window.AppWindow.Move(new Windows.Graphics.PointInt32 { X = _window.AppWindow.Position.X + (int)Math.Round(x * (XamlRoot?.RasterizationScale ?? 1)), Y = _window.AppWindow.Position.Y + (int)Math.Round(y * (XamlRoot?.RasterizationScale ?? 1)) }); return; }
        var bounds = Bounds; var surface = Model.Root?.Manager?.Surface;
        bounds = bounds with { X = bounds.X + x, Y = bounds.Y + y };
        if (surface != null && surface.ActualWidth > 0 && surface.ActualHeight > 0) bounds = bounds.ClampTo(new(0, 0, surface.ActualWidth, surface.ActualHeight));
        SetBounds(bounds);
    }
    private void ResizeBy(double x, double y)
    {
        if (_hostDisposed || IsMaximized || _minimized) return;
        if (_window != null) { _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Math.Max(160, _window.AppWindow.Size.Width + (int)Math.Round(x * (XamlRoot?.RasterizationScale ?? 1))), Height = Math.Max(100, _window.AppWindow.Size.Height + (int)Math.Round(y * (XamlRoot?.RasterizationScale ?? 1))) }); return; }
        var bounds = Bounds; SetBounds(bounds with { Width = Math.Max(160, bounds.Width + x), Height = Math.Max(100, bounds.Height + y) });
    }
    private void ToggleMaximize()
    {
        if (_window?.AppWindow.Presenter is OverlappedPresenter presenter)
        { if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore(); else presenter.Maximize(); return; }
        if (Model.Root?.Manager?.Surface is not { } surface) return;
        IsMaximized = !IsMaximized;
        ApplyManagedBounds();
    }
    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled) HandleWindowKey(e);
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled) HandleWindowKey(e);
    }
    private void HandleWindowKey(KeyRoutedEventArgs e)
    {
        if (_hostDisposed) return;
        if (e.Key == Windows.System.VirtualKey.Escape) { Model.Root?.Manager?.Surface?.CancelDrag(); e.Handled = true; return; }
        var manager = Model.Root?.Manager;
        if (InputState.ControlDown && e.Key == Windows.System.VirtualKey.Tab)
        { manager?.ShowNavigatorWindow(); e.Handled = true; return; }
        if (InputState.ControlDown && e.Key == Windows.System.VirtualKey.F4 && manager?.Layout.ActiveContent is { } active)
        {
            var command = manager.GetLayoutItemFromModel(active).CloseCommand;
            if (command?.CanExecute(null) == true) command.Execute(null);
            e.Handled = true; return;
        }
        if (manager?.AllowMovingFloatingWindowWithKeyboard != true || !InputState.ControlDown) return;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: MoveBy(-10, 0); break;
            case Windows.System.VirtualKey.Right: MoveBy(10, 0); break;
            case Windows.System.VirtualKey.Up: MoveBy(0, -10); break;
            case Windows.System.VirtualKey.Down: MoveBy(0, 10); break;
            default: return;
        }
        e.Handled = true;
    }
}
public class LayoutDocumentFloatingWindowControl : LayoutFloatingWindowControl
{
    private readonly LayoutDocumentFloatingWindow _model;
    public LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model) : this(model, false) { }
    public LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable) => _model = model;
    public override ILayoutElement Model => _model;
    public LayoutItem? RootDocumentLayoutItem => !IsWindowClosed && _model.RootDocument is { } doc ? _model.Root?.Manager?.GetLayoutItemFromModel(doc) : null;
    protected override bool CanClose(object? parameter = null) => _model.RootDocument is { CanClose: true } && base.CanClose(parameter);
    protected override void OnInitialized(EventArgs e)
    {
        // Materialize the public document adapter, not its editor presenter.
        _ = RootDocumentLayoutItem;
        base.OnInitialized(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        ClearValue(DataContextProperty);
        base.OnClosed(e);
    }
    protected override System.IntPtr FilterMessage(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        => base.FilterMessage(hwnd, msg, wParam, lParam, ref handled);
}
public class LayoutAnchorableFloatingWindowControl : LayoutFloatingWindowControl
{
    public static readonly DependencyProperty SingleContentLayoutItemProperty = DependencyProperty.Register(nameof(SingleContentLayoutItem), typeof(LayoutItem), typeof(LayoutAnchorableFloatingWindowControl), new PropertyMetadata(null, (d, e) => ((LayoutAnchorableFloatingWindowControl)d).OnSingleContentLayoutItemChanged(e)));
    private readonly LayoutAnchorableFloatingWindow _model;
    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model) : this(model, false) { }
    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable)
    { _model = model; CloseWindowCommand = new DelegateCommand(_ => Close(), parameter => CanClose(parameter)); HideWindowCommand = new DelegateCommand(_ => Hide(), parameter => CanHide(parameter)); }
    public override ILayoutElement Model => _model;
    public LayoutItem? SingleContentLayoutItem { get => (LayoutItem?)GetValue(SingleContentLayoutItemProperty); set => SetValue(SingleContentLayoutItemProperty, value); }
    public ICommand CloseWindowCommand { get; private set; }
    public ICommand HideWindowCommand { get; private set; }
    protected virtual void OnSingleContentLayoutItemChanged(DependencyPropertyChangedEventArgs e) { }
    protected override bool CanClose(object? parameter = null) => !IsWindowClosed && base.CanClose(parameter);
    protected override bool CanHide(object? parameter = null) => !IsWindowClosed && base.CanHide(parameter);
    protected override void DoHide()
    {
        var root = _model.Root; var manager = root?.Manager;
        foreach (var tool in Contents.OfType<LayoutAnchorable>().ToArray())
        {
            if (!ReferenceEquals(_model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root)) break;
            if (ReferenceEquals(tool.FindParent<LayoutFloatingWindow>(), _model)) tool.Hide();
        }
    }
    protected override void OnInitialized(EventArgs e)
    {
        SingleContentLayoutItem = _model.IsSinglePane && (_model.SinglePane as ILayoutContentSelector)?.SelectedContent is { } selected
            ? _model.Root?.Manager?.GetLayoutItemFromModel(selected) : null;
        base.OnInitialized(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        SingleContentLayoutItem = null;
        ((DelegateCommand)CloseWindowCommand).RaiseCanExecuteChanged();
        ((DelegateCommand)HideWindowCommand).RaiseCanExecuteChanged();
        base.OnClosed(e);
    }
    protected override System.IntPtr FilterMessage(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        => base.FilterMessage(hwnd, msg, wParam, lParam, ref handled);
    internal override void UpdateView()
    {
        base.UpdateView(); SingleContentLayoutItem = _model.IsSinglePane && (_model.SinglePane as ILayoutContentSelector)?.SelectedContent is { } selected ? _model.Root?.Manager?.GetLayoutItemFromModel(selected) : null;
        ((DelegateCommand)CloseWindowCommand).RaiseCanExecuteChanged(); ((DelegateCommand)HideWindowCommand).RaiseCanExecuteChanged();
    }
}
