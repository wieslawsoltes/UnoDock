using Microsoft.UI.Xaml.Automation;
using UnoDock.Layout;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;
/// <summary>One row of a document pane's open-documents list. A real
/// ToggleMenuFlyoutItem keeps menu keyboard navigation, invocation, the checked
/// state and its Toggle automation peer; only the row presentation is custom:
/// DocumentPaneMenuItemHeaderTemplate (data context: the listed content) or the
/// content icon followed by its title.</summary>
internal sealed class DockDocumentMenuRow : ToggleMenuFlyoutItem
{
    [ThreadStatic]
    private static ControlTemplate? _rowTemplate;
    private readonly DockHeaderPresenter _header = new()
    {
        IconSpacing = 6,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly Path _check = DockChrome.Glyph(DockGlyph.Check);
    private ContentPresenter? _headerHost, _checkHost;
    private Border? _gutter;
    private DockMenuPalette _palette;
    private bool _pointer;
    internal DockDocumentMenuRow()
    {
        DefaultStyleKey = typeof(ToggleMenuFlyoutItem);
        MinWidth = 0;
        Padding = new(0);
        Margin = new(0);
        BorderThickness = new(1);
        UseSystemFocusVisuals = true;
        Template = _rowTemplate ??= DockChrome.Resource<ControlTemplate>("UnoDock.DocumentMenuRowTemplate");
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
        RegisterPropertyChangedCallback(IsCheckedProperty, (_, _) => Paint());
    }

    internal LayoutContent? Model
    {
        get;
        private set;
    }

    protected override void OnApplyTemplate()
    {
        if (_headerHost != null && ReferenceEquals(_headerHost.Content, _header))
            _headerHost.Content = null;
        if (_checkHost != null && ReferenceEquals(_checkHost.Content, _check))
            _checkHost.Content = null;
        base.OnApplyTemplate();
        _gutter = GetTemplateChild("PART_MenuGutter") as Border;
        _headerHost = GetTemplateChild("PART_MenuHeader") as ContentPresenter;
        _checkHost = GetTemplateChild("PART_MenuCheck") as ContentPresenter;
        if (_headerHost != null)
            _headerHost.Content = _header;
        if (_checkHost != null)
            _checkHost.Content = _check;
        Paint();
    }

    internal void Configure(DockingManager manager, DockMenuPalette palette, LayoutContent model)
    {
        Model = model;
        _palette = palette;
        Text = model.Title ?? "Untitled";
        AutomationProperties.SetName(this, Text);
        IsChecked = model.IsSelected;
        IsEnabled = model.IsEnabled;
        _header.Update(manager, model, manager.MenuItemHeaderTemplate(model, this), Text);
        RequestedTheme = palette.Theme;
        FontSize = palette.FontSize;
        // Application row templates may be taller than the stock row.
        MinHeight = palette.RowHeight;
        FlowDirection = palette.FlowDirection;
        CornerRadius = new(palette.UsesFluentControls ? 4 : 0);
        Paint();
    }

    private void Paint()
    {
        if (_palette.Surface == null)
            return;
        var fluent = _palette.UsesFluentControls;
        var hot = IsEnabled && (_pointer || FocusState == FocusState.Keyboard);
        Background = hot ? _palette.Hover : fluent ? DockChrome.Transparent : _palette.Surface;
        BorderBrush = hot && !fluent ? _palette.HoverBorder : DockChrome.Transparent;
        Foreground = IsEnabled ? _palette.Foreground : _palette.Disabled;
        _check.Stroke = Foreground;
        _check.Visibility = IsChecked ? Visibility.Visible : Visibility.Collapsed;
        if (_gutter != null)
        {
            _gutter.Background = hot || fluent ? DockChrome.Transparent : _palette.Gutter;
            _gutter.BorderBrush = hot || fluent ? DockChrome.Transparent : _palette.Border;
        }
    }
}
