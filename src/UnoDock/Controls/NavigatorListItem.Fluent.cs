using UnoDock.Internal;

namespace UnoDock.Controls;

internal sealed partial class NavigatorListItem
{
    private Button? _action;
    private Border? _selectionIndicator;
    private DockControlStateResources? _states;
    private bool _fluent;
    internal event Action<NavigatorListItem>? Invoked;
    private void ConfigureFluentTemplate()
    {
        if (_fluent == _palette.UsesFluentControls)
            return;
        _fluent = _palette.UsesFluentControls;
        ReleaseNativeAction();
        Template = _fluent ? DockChrome.Resource<ControlTemplate>("UnoDock.FluentNavigatorRowTemplate") : DockChrome.ButtonTemplate;
        CornerRadius = new(_fluent ? _palette.TabCornerRadius : 0);
        BorderThickness = new(_fluent ? 0 : 1);
        UseSystemFocusVisuals = _fluent;
    }

    protected override void OnApplyTemplate()
    {
        ReleaseNativeAction();
        base.OnApplyTemplate();
        if (_fluent && GetTemplateChild("PART_NavigatorAction") is Button action)
        {
            _action = action;
            _selectionIndicator = GetTemplateChild("PART_NavigatorSelection") as Border;
            _states = new(action);
            action.Click += ActionClicked;
        }

        AttachHeader();
        Paint();
    }

    private void ReleaseNativeAction()
    {
        var action = _action;
        _action = null;
        _selectionIndicator = null;
        if (action != null)
            action.Click -= ActionClicked;
#if !WINDOWS
        _states?.Detach();
#endif
        // On native WinUI the action leaves with its template; detaching its state dictionary
        // first makes replacing a Dark template fail (0x80004005).
        _states = null;
    }

    private void ActionClicked(object sender, RoutedEventArgs args)
    {
        if (!_fluent || !IsLoaded || !IsEnabled || !ReferenceEquals(sender, _action))
            return;
        Invoked?.Invoke(this);
    }

    private bool PaintFluent()
    {
        if (!_fluent)
            return false;
        Background = DockChrome.Transparent;
        BorderBrush = DockChrome.Transparent;
        CornerRadius = new(_palette.TabCornerRadius);
        if (_action is not { } action || _states is not { } states)
            return true;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(action, (Content as LayoutItem)?.Title ?? "");
        ToolTipService.SetToolTip(action, (Content as LayoutItem)?.Title);
        action.FontWeight = IsSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        action.BorderBrush = DockChrome.Transparent;
        action.BorderThickness = new(0);
        action.Background = IsSelected ? _palette.Tab : DockChrome.Transparent;
        // Native CommonStates and focus visuals paint interaction; no parallel
        // pointer/keyboard press state or copied Button ControlTemplate is used.
        states.Set("ButtonBackgroundPointerOver", _palette.Hover);
        states.Set("ButtonBackgroundPressed", _palette.Pressed);
        states.Set("ButtonForegroundPointerOver", _palette.Foreground);
        states.Set("ButtonForegroundPressed", _palette.Foreground);
        states.Set("ButtonForegroundDisabled", _palette.DisabledForeground ?? _palette.Foreground);
        if (_selectionIndicator is { } marker)
        {
            marker.Background = _palette.Accent;
            marker.Visibility = IsSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        states.Refresh(action);
        return true;
    }
}
