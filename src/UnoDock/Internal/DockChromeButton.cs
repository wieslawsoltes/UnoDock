using Microsoft.UI.Xaml.Controls.Primitives;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

internal sealed class DockChromeButton : Button
{
    private DockPalette _palette;
    private bool _over;
    private bool _fluent;
    private bool _subdued;
    private DockControlStateResources? _states;
    internal bool IsSubdued
    {
        get => _subdued;
        set
        {
            if (_subdued == value)
                return;
            _subdued = value;
            Paint();
        }
    }

    internal DockChromeButton()
    {
        DefaultStyleKey = typeof(Button);
        MinHeight = 0;
        MinWidth = 0;
        Padding = new(0);
        BorderThickness = new(1);
        CornerRadius = new(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Template = DockChrome.ButtonTemplate;
        UseSystemFocusVisuals = true;
        Configure(DockChrome.Default(false));
        PointerEntered += (_, _) =>
        {
            _over = true;
            Paint();
        };
        PointerExited += (_, _) =>
        {
            _over = false;
            Paint();
        };
        // ButtonBase owns mouse capture and Space/Enter behavior. A handled or
        // right-button press must not invent a second visual pressed state.
        RegisterPropertyChangedCallback(ButtonBase.IsPressedProperty, (_, _) => Paint());
        IsEnabledChanged += (_, _) => Paint();
        GotFocus += (_, _) => Paint();
        LostFocus += (_, _) => Paint();
        Unloaded += (_, _) =>
        {
            _over = false;
            Paint();
        };
    }

    internal void Configure(DockPalette palette)
    {
        _palette = palette;
        if (_fluent != palette.UsesFluentControls)
        {
            _fluent = palette.UsesFluentControls;
            if (_fluent)
            {
                // Keep Uno/WinUI's template, CommonStates and system focus
                // visuals. Only lightweight metric/normal-fill setters change.
                Style = DockChrome.Resource<Style>("UnoDock.FluentChromeButtonStyle");
                ClearValue(TemplateProperty);
            }
            else
            {
                ClearValue(StyleProperty);
                Template = DockChrome.ButtonTemplate;
            }
        }

        if (_fluent)
        {
            _states ??= new(this);
            _states.Set("ButtonBackgroundPointerOver", palette.Hover);
            _states.Set("ButtonBackgroundPressed", palette.Pressed);
            _states.Set("ButtonForegroundPointerOver", palette.Foreground);
            _states.Set("ButtonForegroundPressed", palette.Foreground);
            _states.Set("ButtonForegroundDisabled", palette.DisabledForeground ?? palette.Foreground);
        }

        CornerRadius = new(palette.ButtonCornerRadius);
        BorderThickness = new(_fluent ? 0 : 1);
        FontSize = palette.FontSize;
        if (Content is Path)
        {
            Width = palette.ChromeButtonSize;
            Height = palette.ChromeButtonSize;
        }

        Paint();
    }

    private void Paint()
    {
        var foreground = !_fluent ? _palette.Foreground : !IsEnabled ? _palette.DisabledForeground ?? _palette.Foreground : _subdued ? _palette.SecondaryForeground ?? _palette.Foreground : _palette.Foreground;
        Foreground = foreground;
        if (Content is Path path)
        {
            path.Stroke = foreground;
            path.Fill = foreground;
        }

        if (_fluent)
        {
            // Native CommonStates paint hover/pressed/disabled on template parts,
            // including keyboard presses. Do not dim the whole subtree or create
            // a second focus border around the platform's two-tone focus visual.
            Background = DockChrome.Transparent;
            BorderBrush = DockChrome.Transparent;
            Opacity = 1;
            return;
        }

        Background = IsEnabled && (IsPressed || _over) ? IsPressed ? _palette.Pressed : _palette.Hover : DockChrome.Transparent;
        BorderBrush = FocusState == FocusState.Keyboard ? _palette.Accent : null;
        Opacity = IsEnabled ? 1 : .45;
    }
}
