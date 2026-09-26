using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Layout;
using Strings = UnoDock.Properties.Resources;

namespace UnoDock.Internal;
// Real MenuFlyoutItem keeps menu keyboard navigation and its Invoke automation peer.
// Only its presentation is independent; application-supplied menus are never styled.
internal sealed class DockMenuRow : MenuFlyoutItem
{
    [ThreadStatic]
    private static ControlTemplate? _rowTemplate, _presenterTemplate;
    private DockMenuPalette _palette;
    private Border? _gutter;
    private bool _pointer, _fluent;
    private DockControlStateResources? _states;
    internal DockMenuRow()
    {
        DefaultStyleKey = typeof(MenuFlyoutItem);
        MinHeight = 0;
        MinWidth = 0;
        Padding = new(0);
        Margin = new(0);
        BorderThickness = new(1);
        CornerRadius = new(0);
        UseSystemFocusVisuals = true;
        Template = _rowTemplate ??= (ControlTemplate)DockChrome.Resource<ControlTemplate>("UnoDock.MenuRowTemplate");
        PointerEntered += (_, _) =>
        {
            _pointer = true;
            Paint();
        };
        PointerExited += (_, _) =>
        {
            _pointer = false;
            Paint();
        };
        GotFocus += (_, _) => Paint();
        LostFocus += (_, _) => Paint();
        IsEnabledChanged += (_, _) => Paint();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _gutter = GetTemplateChild("PART_MenuGutter") as Border;
        Paint();
    }

    internal void Configure(DockMenuPalette palette)
    {
        _palette = palette;
        if (_fluent != palette.UsesFluentControls)
        {
            _fluent = palette.UsesFluentControls;
            if (_fluent)
            {
                Style = DockChrome.Resource<Style>("UnoDock.FluentMenuRowStyle");
                ClearValue(TemplateProperty);
            }
            else
            {
                ClearValue(StyleProperty);
                Template = _rowTemplate;
            }
        }

        if (_fluent)
        {
            _states ??= new(this);
            _states.Set("MenuFlyoutItemBackgroundPointerOver", palette.Hover);
            _states.Set("MenuFlyoutItemBackgroundPressed", palette.Pressed ?? palette.Hover);
            _states.Set("MenuFlyoutItemForegroundPointerOver", palette.Foreground);
            _states.Set("MenuFlyoutItemForegroundPressed", palette.Foreground);
            _states.Set("MenuFlyoutItemForegroundDisabled", palette.Disabled);
        }

        Padding = _fluent ? new(10, 3, 10, 3) : new(0);
        CornerRadius = new(_fluent ? 4 : 0);
        RequestedTheme = palette.Theme;
        FontSize = palette.FontSize;
        Height = palette.RowHeight;
        FlowDirection = palette.FlowDirection;
        Paint();
    }

    private void Paint()
    {
        if (_palette.Surface == null)
            return;
        if (_fluent)
        {
            Background = DockChrome.Transparent;
            BorderBrush = DockChrome.Transparent;
            Foreground = IsEnabled ? _palette.Foreground : _palette.Disabled;
            return;
        }

        var hot = IsEnabled && (_pointer || FocusState == FocusState.Keyboard);
        Background = hot ? _palette.Hover : _palette.Surface;
        BorderBrush = hot ? _palette.HoverBorder : DockChrome.Transparent;
        Foreground = IsEnabled ? _palette.Foreground : _palette.Disabled;
        if (_gutter != null)
        {
            _gutter.Background = hot ? DockChrome.Transparent : _palette.Gutter;
            _gutter.BorderBrush = hot ? DockChrome.Transparent : _palette.Border;
        }
    }

    internal static Style PresenterStyle(DockMenuPalette palette)
    {
        var style = new Style(typeof(MenuFlyoutPresenter));
        if (palette.UsesFluentControls)
            style.BasedOn = DockChrome.Resource<Style>("UnoDock.FluentMenuPresenterStyle");
        style.Setters.Add(new Setter(FrameworkElement.RequestedThemeProperty, palette.Theme));
        style.Setters.Add(new Setter(Control.BackgroundProperty, palette.Surface));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.Border));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(palette.UsesFluentControls ? 4 : 2)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(palette.UsesFluentControls ? 8 : 0)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, palette.MinWidth));
        style.Setters.Add(new Setter(Control.FontSizeProperty, palette.FontSize));
        style.Setters.Add(new Setter(FrameworkElement.FlowDirectionProperty, palette.FlowDirection));
        if (!palette.UsesFluentControls)
            style.Setters.Add(new Setter(Control.TemplateProperty, _presenterTemplate ??= (ControlTemplate)DockChrome.Resource<ControlTemplate>("UnoDock.MenuPresenterTemplate")));
        return style;
    }
}
