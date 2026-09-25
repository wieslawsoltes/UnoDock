using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
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
        _rotator = new(_button);
        Content = _rotator;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
        Margin = new(2);
    }

    private void ActivateFromKeyboard()
    {
        if (_button.FocusState != FocusState.Pointer)
            _model.Root?.Manager?.OpenAutoHide(_model);
    }

    protected override void OnMouseDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && e.ChangedButton == DockMouseButton.Left && _model.IsEnabled)
            _model.Root?.Manager?.OpenAutoHide(_model);
        base.OnMouseDown(e);
    }

    protected override void OnMouseEnter(DockMouseEventArgs e)
    {
        if (!e.Handled && _model.IsEnabled)
        {
            _model.Root?.Manager?.Surface?.StopAutoHideTimer();
            _model.Root?.Manager?.OpenAutoHide(_model, activate: false);
        }

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(DockMouseEventArgs e)
    {
        if (!e.Handled)
            _model.Root?.Manager?.Surface?.StartAutoHideTimer();
        base.OnMouseLeave(e);
    }

    public ILayoutElement Model => _model;
    public AnchorSide Side => (AnchorSide)GetValue(SideProperty);

    protected void SetSide(AnchorSide value) => SetValue(SideProperty, value);
    internal void Update(DockingManager manager)
    {
        SetSide(_model.GetSide());
        _button.Content = _model.Title;
        _button.IsEnabled = _model.IsEnabled;
        var palette = DockChrome.Palette(manager);
        _button.Configure(palette);
        _button.BorderBrush = palette.Border;
        _button.Height = palette.RailThickness - 4;
        _rotator.Vertical = Side is AnchorSide.Left or AnchorSide.Right;
        ToolTipService.SetToolTip(_button, _model.ToolTip ?? _model.Title);
        MenuContext.SetTarget(this, _model);
        ContextFlyout = DockVisuals.Menu(manager, _model);
        DockVisuals.SetName(_button, "Auto-hidden tool: " + _model.Title);
        if (manager.AnchorTemplate != null)
            Template = manager.AnchorTemplate;
    }
}
