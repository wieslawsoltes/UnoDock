using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

internal sealed class DockChromeButton : Button
{
    private DockPalette _palette;
    private bool _over, _pressed;
    internal DockChromeButton()
    {
        MinHeight = 0; MinWidth = 0; Padding = new(0); BorderThickness = new(1); CornerRadius = new(0);
        HorizontalContentAlignment = HorizontalAlignment.Center; VerticalContentAlignment = VerticalAlignment.Center;
        Template = DockChrome.ButtonTemplate; UseSystemFocusVisuals = true;
        Configure(DockChrome.Default(false));
        PointerEntered += (_, _) => { _over = true; Paint(); };
        PointerExited += (_, _) => { _over = false; Paint(); };
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => { _pressed = true; Paint(); }), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => { _pressed = false; Paint(); }), true);
        PointerCaptureLost += (_, _) => { _pressed = false; Paint(); };
        IsEnabledChanged += (_, _) => Paint();
        GotFocus += (_, _) => Paint(); LostFocus += (_, _) => Paint();
        Unloaded += (_, _) => { _over = _pressed = false; Paint(); };
    }
    internal void Configure(DockPalette palette)
    {
        _palette = palette; Foreground = palette.Foreground; FontSize = palette.FontSize;
        if (Content is Path path) { path.Stroke = palette.Foreground; path.Fill = palette.Foreground; }
        Paint();
    }
    private void Paint()
    {
        Background = IsEnabled && _over ? _pressed ? _palette.Pressed : _palette.Hover : DockChrome.Transparent;
        BorderBrush = FocusState == FocusState.Keyboard ? _palette.Accent : null;
        Opacity = IsEnabled ? 1 : .45;
    }
}
