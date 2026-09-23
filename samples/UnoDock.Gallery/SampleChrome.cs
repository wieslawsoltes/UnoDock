using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;
using PathShape = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Gallery;

internal readonly record struct SamplePalette(Brush Foreground, Brush Hover, Brush Pressed, Brush Accent);
internal static class SampleChrome
{
    internal static SolidColorBrush Color(uint rgb) => new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    internal static SamplePalette Default(bool dark) => new(Color(dark ? 0xf2f2f2u : 0x202020u), Color(dark ? 0x444444u : 0xdceaf4u), Color(dark ? 0x555555u : 0xb9d8edu), Color(0x0067b8));
    internal static SampleButton Button(string label, Action action, string? name = null)
    {
        var button = new SampleButton { Content = label };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name ?? label);
        ToolTipService.SetToolTip(button, name ?? label); button.Click += (_, _) => action(); return button;
    }
}
internal sealed class SampleButton : Button
{
    private SamplePalette _palette = SampleChrome.Default(false);
    private bool _hover, _pressed;
    internal SampleButton()
    {
        FontSize = 12; MinHeight = 0; MinWidth = 0; Padding = new(2); BorderThickness = new(1); CornerRadius = new(0);
        HorizontalContentAlignment = HorizontalAlignment.Center; VerticalContentAlignment = VerticalAlignment.Center;
        Template = (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ContentPresenter Content='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}' Padding='{TemplateBinding Padding}' HorizontalContentAlignment='Center' VerticalContentAlignment='Center'/></Border></ControlTemplate>");
        PointerEntered += (_, _) => { _hover = true; Paint(); }; PointerExited += (_, _) => { _hover = false; Paint(); };
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => { _pressed = true; Paint(); }), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => { _pressed = false; Paint(); }), true);
        PointerCaptureLost += (_, _) => { _pressed = false; Paint(); };
        Unloaded += (_, _) => { _hover = _pressed = false; Paint(); };
        IsEnabledChanged += (_, _) => Paint(); GotFocus += (_, _) => Paint(); LostFocus += (_, _) => Paint();
        Configure(_palette);
    }
    internal void Configure(SamplePalette palette)
    {
        _palette = palette; Foreground = palette.Foreground;
        if (Content is PathShape path) path.Stroke = palette.Foreground;
        Paint();
    }
    private void Paint()
    {
        Background = IsEnabled && _hover ? _pressed ? _palette.Pressed : _palette.Hover : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        BorderBrush = FocusState == FocusState.Keyboard ? _palette.Accent : null;
        Opacity = IsEnabled ? 1 : .5;
    }
}
