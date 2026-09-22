using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Compatibility;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public class LayoutAnchorControl : DockInputControl, ILayoutControl
{
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(AnchorSide), typeof(LayoutAnchorControl), new PropertyMetadata(AnchorSide.Left));
    private readonly LayoutAnchorable _model;
    private readonly Button _button;
    public LayoutAnchorControl(LayoutAnchorable model)
    {
        _model = model;
        _button = DockVisuals.Button(model.Title ?? "Tool", ActivateFromKeyboard); Content = _button;

    }
    private void ActivateFromKeyboard()
    { if (_button.FocusState != FocusState.Pointer) _model.Root?.Manager?.OpenAutoHide(_model); }
    protected override void OnMouseDown(DockMouseButtonEventArgs e)
    { if (!e.Handled && e.ChangedButton == DockMouseButton.Left && _model.IsEnabled) _model.Root?.Manager?.OpenAutoHide(_model); base.OnMouseDown(e); }
    protected override void OnMouseEnter(DockMouseEventArgs e)
    { if (!e.Handled && _model.IsEnabled) { _model.Root?.Manager?.Surface?.StopAutoHideTimer(); _model.Root?.Manager?.OpenAutoHide(_model); } base.OnMouseEnter(e); }
    protected override void OnMouseLeave(DockMouseEventArgs e)
    { if (!e.Handled) _model.Root?.Manager?.Surface?.StartAutoHideTimer(); base.OnMouseLeave(e); }
    public ILayoutElement Model => _model;
    public AnchorSide Side => (AnchorSide)GetValue(SideProperty);
    protected void SetSide(AnchorSide value) => SetValue(SideProperty, value);
    internal void Update(DockingManager manager)
    {
        SetSide(_model.GetSide()); _button.Content = _model.Title; _button.IsEnabled = _model.IsEnabled;
        ToolTipService.SetToolTip(_button, _model.ToolTip ?? _model.Title);
        ContextFlyout = DockVisuals.Menu(manager, _model);
        DockVisuals.SetName(_button, "Auto-hidden tool: " + _model.Title);
        if (manager.AnchorTemplate != null) Template = manager.AnchorTemplate;
    }
}
public class LayoutAnchorGroupControl : ContentControl, ILayoutControl
{
    private readonly LayoutAnchorGroup _model;
    private readonly StackPanel _panel = new() { Spacing = 2 };
    public LayoutAnchorGroupControl(LayoutAnchorGroup model) { _model = model; Content = _panel; }
    public ILayoutElement Model => _model;
    public ObservableCollection<LayoutAnchorControl> Children { get; } = [];
    internal void Update(DockingManager manager)
    {
        var models = _model.Children.ToArray();
        foreach (var stale in Children.Where(c => !models.Contains(c.Model)).ToArray()) Children.Remove(stale);
        for (var i = 0; i < models.Length; i++)
        {
            var child = Children.FirstOrDefault(c => ReferenceEquals(c.Model, models[i]));
            if (child == null) Children.Insert(Math.Min(i, Children.Count), child = new(models[i]));
            else if (Children.IndexOf(child) != i) Children.Move(Children.IndexOf(child), i);
            child.Update(manager);
        }
        _panel.Orientation = _model.GetSide() is AnchorSide.Left or AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
        VisualParenting.ReconcilePanel(_panel, Children.Cast<UIElement>().ToArray());
        if (manager.AnchorGroupTemplate != null) Template = manager.AnchorGroupTemplate;
    }
}
public class LayoutAnchorSideControl : ContentControl, ILayoutControl
{
    public static readonly DependencyProperty IsLeftSideProperty = DependencyProperty.Register(nameof(IsLeftSide), typeof(bool), typeof(LayoutAnchorSideControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsRightSideProperty = DependencyProperty.Register(nameof(IsRightSide), typeof(bool), typeof(LayoutAnchorSideControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsTopSideProperty = DependencyProperty.Register(nameof(IsTopSide), typeof(bool), typeof(LayoutAnchorSideControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsBottomSideProperty = DependencyProperty.Register(nameof(IsBottomSide), typeof(bool), typeof(LayoutAnchorSideControl), new PropertyMetadata(false));
    private readonly LayoutAnchorSide _model;
    private readonly StackPanel _panel = new() { Spacing = 6 };
    public LayoutAnchorSideControl(LayoutAnchorSide model) { _model = model; Content = _panel; }
    public ILayoutElement Model => _model;
    public ObservableCollection<LayoutAnchorGroupControl> Children { get; } = [];
    public bool IsLeftSide => (bool)GetValue(IsLeftSideProperty);
    public bool IsRightSide => (bool)GetValue(IsRightSideProperty);
    public bool IsTopSide => (bool)GetValue(IsTopSideProperty);
    public bool IsBottomSide => (bool)GetValue(IsBottomSideProperty);
    protected void SetIsLeftSide(bool value) => SetValue(IsLeftSideProperty, value);
    protected void SetIsRightSide(bool value) => SetValue(IsRightSideProperty, value);
    protected void SetIsTopSide(bool value) => SetValue(IsTopSideProperty, value);
    protected void SetIsBottomSide(bool value) => SetValue(IsBottomSideProperty, value);
    internal void Update(DockingManager manager)
    {
        SetIsLeftSide(_model.Side == AnchorSide.Left); SetIsRightSide(_model.Side == AnchorSide.Right);
        SetIsTopSide(_model.Side == AnchorSide.Top); SetIsBottomSide(_model.Side == AnchorSide.Bottom);
        var models = _model.Children.ToArray();
        foreach (var stale in Children.Where(c => !models.Contains(c.Model)).ToArray()) Children.Remove(stale);
        foreach (var model in models)
        {
            var child = Children.FirstOrDefault(c => ReferenceEquals(c.Model, model));
            if (child == null) Children.Add(child = new(model)); child.Update(manager);
        }
        _panel.Orientation = IsLeftSide || IsRightSide ? Orientation.Vertical : Orientation.Horizontal;
        Visibility = models.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        VisualParenting.ReconcilePanel(_panel, Children.Cast<UIElement>().ToArray());
        if (manager.AnchorSideTemplate != null) Template = manager.AnchorSideTemplate;
    }
}

public class LayoutAutoHideWindowControl : ContentControl, ILayoutControl
{
    public new static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty;
    public static readonly DependencyProperty AnchorableStyleProperty = DependencyProperty.Register(nameof(AnchorableStyle), typeof(Style), typeof(LayoutAutoHideWindowControl), new PropertyMetadata(null));
    private readonly Grid _layout = new();
    private readonly ContentPresenter _presenter = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock _title = new() { Margin = new Thickness(10, 8, 10, 8) };
    private readonly Thumb _resize = new();
    private LayoutAnchorable? _model;
    public LayoutAutoHideWindowControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); _layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var title = new Grid(); title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); title.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); title.Children.Add(_title);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(DockVisuals.Button("◇", () => { var model = _model; model?.Root?.Manager?.CloseAutoHide(); model?.ToggleAutoHide(); }, "Pin tool"));
        buttons.Children.Add(DockVisuals.Button("×", () => _model?.Root?.Manager?.CloseAutoHide(), "Close auto-hide flyout")); Grid.SetColumn(buttons, 1); title.Children.Add(buttons);
        _layout.Children.Add(title); Grid.SetRow(_presenter, 1); _layout.Children.Add(_presenter);
        _resize.DragDelta += (_, e) => Resize(e.HorizontalChange, e.VerticalChange); Grid.SetRowSpan(_resize, 2); _layout.Children.Add(_resize);
        Content = _layout; BorderThickness = new(1);
        PointerEntered += (_, _) => _model?.Root?.Manager?.Surface?.StopAutoHideTimer();
        PointerExited += (_, _) => _model?.Root?.Manager?.Surface?.StartAutoHideTimer();
    }
    public ILayoutElement Model => _model!;
    public Style? AnchorableStyle { get => (Style?)GetValue(AnchorableStyleProperty); set => SetValue(AnchorableStyleProperty, value); }
    internal void Open(LayoutAnchorable model)
    {
        _model = model; var manager = model.Root?.Manager ?? throw new InvalidOperationException("Auto-hidden content must be attached.");
        var item = manager.GetLayoutItemFromModel(model); item.UpdateView(); VisualParenting.Detach(item.View); item.View.Visibility = Visibility.Visible; _presenter.Content = item.View;
        _title.Text = model.Title; Background = DockVisuals.Brush(manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush"); BorderBrush = DockVisuals.Brush(manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        var side = model.GetSide(); var horizontal = side is AnchorSide.Left or AnchorSide.Right;
        _resize.Width = horizontal ? 6 : double.NaN; _resize.Height = horizontal ? double.NaN : 6;
        _resize.HorizontalAlignment = side == AnchorSide.Left ? HorizontalAlignment.Right : side == AnchorSide.Right ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _resize.VerticalAlignment = side == AnchorSide.Top ? VerticalAlignment.Bottom : side == AnchorSide.Bottom ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        _resize.Background = BorderBrush; model.IsActive = true; Visibility = Visibility.Visible;
        if (AnchorableStyle != null) item.ApplyContainerStyle(AnchorableStyle);
    }
    protected virtual bool HasFocusWithinCore()
    {
        if (XamlRoot == null) return false;
        for (var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; element != null; element = VisualTreeHelper.GetParent(element))
            if (ReferenceEquals(element, this)) return true;
        return false;
    }
    protected virtual IEnumerator LogicalChildren => (_presenter.Content is DependencyObject child ? new[] { child } : Array.Empty<DependencyObject>()).GetEnumerator();
    private void Resize(double x, double y)
    {
        if (_model == null || !double.IsFinite(x) || !double.IsFinite(y)) return;
        // Zero means unspecified in the original model. Begin resizing from the
        // rendered fallback size, not from zero, to avoid a first-drag jump.
        var width = _model.AutoHideWidth > 0 ? _model.AutoHideWidth : ActualWidth > 0 ? ActualWidth : 300;
        var height = _model.AutoHideHeight > 0 ? _model.AutoHideHeight : ActualHeight > 0 ? ActualHeight : 240;
        switch (_model.GetSide())
        {
            case AnchorSide.Left: _model.AutoHideWidth = Math.Max(_model.AutoHideMinWidth, width + x); break;
            case AnchorSide.Right: _model.AutoHideWidth = Math.Max(_model.AutoHideMinWidth, width - x); break;
            case AnchorSide.Top: _model.AutoHideHeight = Math.Max(_model.AutoHideMinHeight, height + y); break;
            case AnchorSide.Bottom: _model.AutoHideHeight = Math.Max(_model.AutoHideMinHeight, height - y); break;
        }
        _model.Root?.Manager?.Surface?.PositionAutoHide();
    }
    internal void CloseView() { _presenter.Content = null; _model = null; Visibility = Visibility.Collapsed; }
}
