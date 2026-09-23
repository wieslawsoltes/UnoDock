using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Compatibility;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public class LayoutAnchorControl : DockInputControl, ILayoutControl
{
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(AnchorSide), typeof(LayoutAnchorControl), new PropertyMetadata(AnchorSide.Left));
    private readonly LayoutAnchorable _model;
    private readonly DockChromeButton _button;
    private readonly DockRotatedLabel _rotator;
    public LayoutAnchorControl(LayoutAnchorable model)
    {
        _model = model;
        _button = DockChrome.Button(model.Title ?? "Tool", ActivateFromKeyboard);
        _button.Padding = new(2, 1, 2, 1);
        _rotator = new(_button); Content = _rotator;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Top;
        Margin = new(2);

    }
    private void ActivateFromKeyboard()
    { if (_button.FocusState != FocusState.Pointer) _model.Root?.Manager?.OpenAutoHide(_model); }
    protected override void OnMouseDown(DockMouseButtonEventArgs e)
    { if (!e.Handled && e.ChangedButton == DockMouseButton.Left && _model.IsEnabled) _model.Root?.Manager?.OpenAutoHide(_model); base.OnMouseDown(e); }
    protected override void OnMouseEnter(DockMouseEventArgs e)
    { if (!e.Handled && _model.IsEnabled) { _model.Root?.Manager?.Surface?.StopAutoHideTimer(); _model.Root?.Manager?.OpenAutoHide(_model, activate: false); } base.OnMouseEnter(e); }
    protected override void OnMouseLeave(DockMouseEventArgs e)
    { if (!e.Handled) _model.Root?.Manager?.Surface?.StartAutoHideTimer(); base.OnMouseLeave(e); }
    public ILayoutElement Model => _model;
    public AnchorSide Side => (AnchorSide)GetValue(SideProperty);
    protected void SetSide(AnchorSide value) => SetValue(SideProperty, value);
    internal void Update(DockingManager manager)
    {
        SetSide(_model.GetSide()); _button.Content = _model.Title; _button.IsEnabled = _model.IsEnabled;
        var palette = DockChrome.Palette(manager); _button.Configure(palette);
        _button.BorderBrush = palette.Border; _button.Height = palette.RailThickness - 4;
        _rotator.Vertical = Side is AnchorSide.Left or AnchorSide.Right;
        ToolTipService.SetToolTip(_button, _model.ToolTip ?? _model.Title);
        MenuContext.SetTarget(this, _model);
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
    public LayoutAnchorSideControl(LayoutAnchorSide model) { _model = model; Content = _panel; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Top; }
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
        var palette = DockChrome.Palette(manager); Background = palette.Header;
        Width = IsLeftSide || IsRightSide ? palette.RailThickness : double.NaN;
        Height = IsTopSide || IsBottomSide ? palette.RailThickness : double.NaN;
        Visibility = models.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        VisualParenting.ReconcilePanel(_panel, Children.Cast<UIElement>().ToArray());
        if (manager.AnchorSideTemplate != null) Template = manager.AnchorSideTemplate;
    }
}
