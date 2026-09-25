using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
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
