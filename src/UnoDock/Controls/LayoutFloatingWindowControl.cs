using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

/// <summary>Compositional replacement for the WPF Window base. Native desktop and in-surface hosts share the same layout.</summary>
public abstract class LayoutFloatingWindowControl : ContentControl, ILayoutControl
{
    public static readonly DependencyProperty IsContentImmutableProperty = DependencyProperty.Register(nameof(IsContentImmutable), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = DependencyProperty.Register(nameof(IsDragging), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(nameof(IsMaximized), typeof(bool), typeof(LayoutFloatingWindowControl), new PropertyMetadata(false));
    public static readonly DependencyProperty ResizeBorderThicknessProperty = DependencyProperty.Register(nameof(ResizeBorderThickness), typeof(Thickness), typeof(LayoutFloatingWindowControl), new PropertyMetadata(new Thickness(5)));
    private readonly Grid _frame = new();
    private readonly ContentPresenter _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock _caption = new() { Margin = new Thickness(10, 6, 10, 6), VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _title = new();
    private bool _closingHost, _syncBounds, _hostDisposed;
    private Window? _window;
    private DockRect? _restoreBounds;
    protected LayoutFloatingWindowControl(ILayoutElement model) : this(model, false) { }
    protected LayoutFloatingWindowControl(ILayoutElement model, bool isContentImmutable)
    {
        ArgumentNullException.ThrowIfNull(model); SetValue(IsContentImmutableProperty, isContentImmutable);
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _frame.RowDefinitions.Add(new() { Height = GridLength.Auto }); _frame.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var drag = new Thumb { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Opacity = .01 };
        drag.DragStarted += (_, _) => SetValue(IsDraggingProperty, true);
        drag.DragDelta += (_, e) => MoveBy(e.HorizontalChange, e.VerticalChange);
        drag.DragCompleted += (_, _) => SetValue(IsDraggingProperty, false);
        _caption.IsHitTestVisible = false; _title.Children.Add(drag); _title.Children.Add(_caption);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(DockVisuals.Button("↙", DockAll, "Dock floating content"));
        actions.Children.Add(DockVisuals.Button("□", ToggleMaximize, "Maximize or restore floating window"));
        actions.Children.Add(DockVisuals.Button("×", Close, "Close floating window")); Grid.SetColumn(actions, 1); _title.Children.Add(actions);
        Grid.SetRow(_body, 1); _frame.Children.Add(_title); _frame.Children.Add(_body);
        var resize = new Thumb { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Opacity = .25 };
        resize.DragDelta += (_, e) => ResizeBy(e.HorizontalChange, e.VerticalChange); Grid.SetRow(resize, 1); _frame.Children.Add(resize);
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
    internal DockRect Bounds => PositionModel is { } p ? new(p.FloatingLeft, p.FloatingTop, Math.Max(160, p.FloatingWidth), Math.Max(100, p.FloatingHeight)) : new(80, 80, 640, 480);
    protected virtual bool CanClose() => Contents.Any() && Contents.All(c => c.CanClose || c is LayoutAnchorable { CanHide: true });
    protected virtual bool CanHide() => Contents.Any() && Contents.All(c => c is LayoutAnchorable { CanHide: true });
    protected virtual void DoHide() { foreach (var tool in Contents.OfType<LayoutAnchorable>().ToArray()) tool.Hide(); }
    protected virtual void OnClosed(EventArgs e) { }
    public void Close()
    {
        if (!CanClose()) return;
        var root = Model.Root as LayoutRoot;
        using (root?.BeginUpdate()) foreach (var content in Contents.ToArray()) DockVisuals.CloseOrHide(content);
        root?.CollectGarbage();
        if (Model is LayoutFloatingWindow { IsValid: false } || Model.Root == null) CloseHost();
    }
    public void Hide() { if (CanHide()) DoHide(); }
    public void Activate() { if (_window != null) _window.Activate(); else Focus(FocusState.Programmatic); }
    private void DockAll()
    {
        var root = Model.Root as LayoutRoot;
        using (root?.BeginUpdate()) foreach (var content in Contents.ToArray()) content.Dock();
    }
    internal virtual void UpdateView()
    {
        var manager = Model.Root?.Manager; if (manager?.Surface == null) return;
        _caption.Text = Contents.FirstOrDefault(c => c.IsActive)?.Title ?? Contents.FirstOrDefault()?.Title ?? "Floating tools";
        if (_window != null) _window.Title = _caption.Text;
        _frame.Background = DockVisuals.Brush(manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush");
        _title.Background = DockVisuals.Brush(manager, "UnoDock.HeaderBrush", "ControlFillColorSecondaryBrush");
        BorderBrush = DockVisuals.Brush(manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        UIElement? body = Model switch
        {
            LayoutDocumentFloatingWindow { RootDocument: { } d } => manager.GetLayoutItemFromModel(d).View,
            LayoutAnchorableFloatingWindow { RootPanel: { } p } => manager.Surface.GetView(p),
            _ => null
        };
        if (Model is LayoutAnchorableFloatingWindow { RootPanel: { } panel }) manager.Surface.UpdateView(panel);
        if (body != null) { body.Visibility = Visibility.Visible; if (!ReferenceEquals(_body.Content, body)) { VisualParenting.Detach(body); _body.Content = body; } }
        if (_window == null) { Width = Bounds.Width; Height = Bounds.Height; Canvas.SetLeft(this, Bounds.X); Canvas.SetTop(this, Bounds.Y); }
    }
    internal void ShowNative()
    {
        if (_hostDisposed) return;
        if (_window == null)
        {
            VisualParenting.Detach(this); Width = double.NaN; Height = double.NaN;
            _window = new Window { Title = _caption.Text, Content = this };
            _window.AppWindow.Closing += OnNativeClosing;
            _window.AppWindow.Changed += OnNativeChanged;
            _window.Closed += OnNativeClosed;
            var bounds = Bounds; var scale = Math.Max(1, XamlRoot?.RasterizationScale ?? 1);
            _syncBounds = true;
            try { _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32 { X = (int)(bounds.X * scale), Y = (int)(bounds.Y * scale), Width = (int)(bounds.Width * scale), Height = (int)(bounds.Height * scale) }); }
            finally { _syncBounds = false; }
            if (PositionModel?.IsMaximized == true && _window.AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
            _window.Activate();
        }
        else if (!_window.AppWindow.IsVisible) _window.Activate();
    }
    private void OnNativeClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_closingHost) return;
        // Prevent native destruction until model cancellation has been evaluated.
        e.Cancel = true;
        DispatcherQueue.TryEnqueue(Close);
    }
    private void OnNativeClosed(object sender, WindowEventArgs e) { _window = null; OnClosed(EventArgs.Empty); }
    private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)
    {
        if (_syncBounds || _closingHost) return;
        var scale = Math.Max(1, XamlRoot?.RasterizationScale ?? 1);
        if (e.DidPositionChange || e.DidSizeChange) SetBounds(new(sender.Position.X / scale, sender.Position.Y / scale, sender.Size.Width / scale, sender.Size.Height / scale));
        var maximized = sender.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Maximized;
        IsMaximized = maximized;
        foreach (var content in Contents) content.IsMaximized = maximized;
    }
    internal void HideHost() { if (_window != null) _window.AppWindow.Hide(); else Visibility = Visibility.Collapsed; }
    internal void CloseHost()
    {
        if (_hostDisposed) return; _hostDisposed = true; _closingHost = true;
        if (_window is { } window)
        {
            window.AppWindow.Closing -= OnNativeClosing; window.AppWindow.Changed -= OnNativeChanged; window.Closed -= OnNativeClosed;
            window.Content = null; window.Close(); _window = null;
        }
        _body.Content = null; VisualParenting.Detach(this); OnClosed(EventArgs.Empty);
    }
    internal void SetBounds(DockRect bounds)
    {
        if (!double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height)) return;
        using var batch = (Model.Root as LayoutRoot)?.BeginUpdate();
        foreach (var content in Contents)
        { content.FloatingLeft = bounds.X; content.FloatingTop = bounds.Y; content.FloatingWidth = Math.Max(160, bounds.Width); content.FloatingHeight = Math.Max(100, bounds.Height); }
        if (_window == null) { Width = Math.Max(160, bounds.Width); Height = Math.Max(100, bounds.Height); Canvas.SetLeft(this, bounds.X); Canvas.SetTop(this, bounds.Y); }
    }
    private void MoveBy(double x, double y)
    {
        if (_window != null) { _window.AppWindow.Move(new Windows.Graphics.PointInt32 { X = _window.AppWindow.Position.X + (int)x, Y = _window.AppWindow.Position.Y + (int)y }); return; }
        var bounds = Bounds; var surface = Model.Root?.Manager?.Surface;
        bounds = bounds with { X = bounds.X + x, Y = bounds.Y + y };
        if (surface != null && surface.ActualWidth > 0 && surface.ActualHeight > 0) bounds = bounds.ClampTo(new(0, 0, surface.ActualWidth, surface.ActualHeight));
        SetBounds(bounds);
    }
    private void ResizeBy(double x, double y)
    {
        if (_window != null) { _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Math.Max(160, _window.AppWindow.Size.Width + (int)x), Height = Math.Max(100, _window.AppWindow.Size.Height + (int)y) }); return; }
        var bounds = Bounds; SetBounds(bounds with { Width = Math.Max(160, bounds.Width + x), Height = Math.Max(100, bounds.Height + y) });
    }
    private void ToggleMaximize()
    {
        if (_window?.AppWindow.Presenter is OverlappedPresenter presenter)
        { if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore(); else presenter.Maximize(); return; }
        if (Model.Root?.Manager?.Surface is not { } surface) return;
        if (_restoreBounds is { } restore) { _restoreBounds = null; IsMaximized = false; SetBounds(restore); }
        else { _restoreBounds = Bounds; IsMaximized = true; SetBounds(new(0, 0, surface.ActualWidth, surface.ActualHeight)); }
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (Model.Root?.Manager?.AllowMovingFloatingWindowWithKeyboard != true || !InputState.ControlDown) return;
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
    public LayoutItem? RootDocumentLayoutItem => _model.RootDocument is { } doc ? _model.Root?.Manager?.GetLayoutItemFromModel(doc) : null;
}
public class LayoutAnchorableFloatingWindowControl : LayoutFloatingWindowControl
{
    public static readonly DependencyProperty SingleContentLayoutItemProperty = DependencyProperty.Register(nameof(SingleContentLayoutItem), typeof(LayoutItem), typeof(LayoutAnchorableFloatingWindowControl), new PropertyMetadata(null, (d, e) => ((LayoutAnchorableFloatingWindowControl)d).OnSingleContentLayoutItemChanged(e)));
    private readonly LayoutAnchorableFloatingWindow _model;
    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model) : this(model, false) { }
    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable)
    { _model = model; CloseWindowCommand = new DelegateCommand(Close, CanClose); HideWindowCommand = new DelegateCommand(Hide, CanHide); }
    public override ILayoutElement Model => _model;
    public LayoutItem? SingleContentLayoutItem { get => (LayoutItem?)GetValue(SingleContentLayoutItemProperty); private set => SetValue(SingleContentLayoutItemProperty, value); }
    public ICommand CloseWindowCommand { get; private set; }
    public ICommand HideWindowCommand { get; private set; }
    protected virtual void OnSingleContentLayoutItemChanged(DependencyPropertyChangedEventArgs e) { }
    internal override void UpdateView()
    {
        base.UpdateView(); SingleContentLayoutItem = _model.IsSinglePane && (_model.SinglePane as ILayoutContentSelector)?.SelectedContent is { } selected ? _model.Root?.Manager?.GetLayoutItemFromModel(selected) : null;
        ((DelegateCommand)CloseWindowCommand).RaiseCanExecuteChanged(); ((DelegateCommand)HideWindowCommand).RaiseCanExecuteChanged();
    }
}
