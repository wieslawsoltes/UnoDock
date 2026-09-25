using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

internal sealed partial class NavigatorListItem : ListBoxItem
{
    private DockPalette _palette = DockChrome.Default(false);
    private Brush _surface = DockChrome.Transparent;
    private bool _hovered;
    internal NavigatorListItem()
    {
        MinHeight = 0;
        MinWidth = 0;
        Height = 24;
        Padding = new(8, 3, 4, 3);
        Margin = new(0);
        BorderThickness = new(1);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;
        Template = DockChrome.ButtonTemplate;
        RegisterPropertyChangedCallback(IsSelectedProperty, (_, _) => Paint());
        GotFocus += (_, _) => Paint();
        LostFocus += (_, _) => Paint();
        PointerEntered += (_, _) =>
        {
            _hovered = true;
            Paint();
        };
        PointerExited += (_, _) =>
        {
            _hovered = false;
            Paint();
        };
    }

    internal void Configure(DockPalette palette, Brush surface)
    {
        _palette = palette;
        _surface = surface;
        FontSize = palette.FontSize;
        Foreground = palette.Foreground;
        Height = RowHeight(palette.FontSize);
        Paint();
    }

    internal static double RowHeight(double fontSize) => Math.Max(24, Math.Ceiling(fontSize * 1.4 + 6));
    private void Paint()
    {
        Background = IsSelected ? _palette.Tab : _hovered ? _palette.Hover : _surface;
        BorderBrush = IsSelected ? _palette.Border : DockChrome.Transparent;
    }
}
