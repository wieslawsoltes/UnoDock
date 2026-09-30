using Microsoft.UI.Xaml.Controls.Primitives;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

internal sealed partial class DockChromeButton : Button
{
    private DockPalette _palette = DockChrome.Default(false);
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

    private Brush? _foregroundOverride, _hoverOverride, _backgroundOverride;
    /// <summary>Resting fill supplied by a theme (for example an auto-hide rail tab).</summary>
    internal Brush? BackgroundOverride
    {
        get => _backgroundOverride;
        set
        {
            if (ReferenceEquals(_backgroundOverride, value))
                return;
            _backgroundOverride = value;
            Paint();
        }
    }

    /// <summary>Theme state foreground (for example a selected tab or an active
        /// title bar); null keeps the palette's primary/secondary rule.</summary>
        internal Brush? ForegroundOverride
    {
        get => _foregroundOverride;
        set
        {
            if (ReferenceEquals(_foregroundOverride, value))
                return;
            _foregroundOverride = value;
            Paint();
        }
    }

    internal Brush? HoverOverride
    {
        get => _hoverOverride;
        set
        {
            if (ReferenceEquals(_hoverOverride, value))
                return;
            _hoverOverride = value;
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
        Configure(_palette);
        if (OperatingSystem.IsBrowser())
            Loaded += (_, _) => Paint();
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
        ArgumentNullException.ThrowIfNull(palette);
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
                _states?.Detach();
                _states = null;
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
        _states?.Refresh(this);
    }

    private void Paint()
    {
        var foreground = _foregroundOverride != null && IsEnabled ? _foregroundOverride : !_fluent ? _palette.Foreground : !IsEnabled ? _palette.DisabledForeground ?? _palette.Foreground : _subdued ? _palette.SecondaryForeground ?? _palette.Foreground : _palette.Foreground;
        // The pinned browser renderer can stall in inherited brush propagation
        // before attachment. Retain the latest palette and publish the same brush
        // at Loaded; desktop/offscreen presentation keeps its existing contract.
        if (!OperatingSystem.IsBrowser() || IsLoaded)
            Foreground = foreground;
        PaintIcon();
        if (_fluent)
        {
            // Native CommonStates paint hover/pressed/disabled on template parts,
            // including keyboard presses. Do not dim the whole subtree or create
            // a second focus border around the platform's two-tone focus visual.
            Background = _backgroundOverride ?? DockChrome.Transparent;
            BorderBrush = DockChrome.Transparent;
            Opacity = 1;
            return;
        }

        Background = IsEnabled && (IsPressed || _over) ? IsPressed ? _palette.Pressed : _hoverOverride ?? _palette.Hover : _backgroundOverride ?? DockChrome.Transparent;
        BorderBrush = FocusState == FocusState.Keyboard ? _palette.Accent : null;
        Opacity = IsEnabled ? 1 : .45;
    }
}
