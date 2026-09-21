using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Converters;

/// <summary>WinUI converter contract plus the CultureInfo overload used by WPF callers.</summary>
public abstract class CompatibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => Convert(value, targetType, parameter, Culture(language));
    public object ConvertBack(object value, Type targetType, object parameter, string language) => ConvertBack(value, targetType, parameter, Culture(language));
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
    private static CultureInfo Culture(string language)
    { try { return string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(language); } catch (CultureNotFoundException) { return CultureInfo.CurrentUICulture; } }
}
public class BoolToVisibilityConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Visible : Visibility.Collapsed;
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is Visibility.Visible;
}
public class InverseBoolToVisibilityConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not Visibility.Visible;
}
public class AnchorSideToOrientationConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AnchorSide.Left or AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
}
public class AnchorSideToAngleConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch { AnchorSide.Left => -90d, AnchorSide.Right => 90d, _ => 0d };
}
public class NullToDoNothingConverter : CompatibilityConverter
{
    // WinUI has no WPF Binding.DoNothing. UnsetValue deliberately invokes the binding's fallback semantics.
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value ?? DependencyProperty.UnsetValue;
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value ?? DependencyProperty.UnsetValue;
}
public class UriSourceToBitmapImageConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ImageSource image => image,
        Uri uri => new BitmapImage(uri),
        string text when Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri) => new BitmapImage(uri),
        _ => DependencyProperty.UnsetValue
    };
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is BitmapImage image ? image.UriSource : DependencyProperty.UnsetValue;
}
public class LayoutItemFromLayoutModelConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        LayoutItem item => item,
        LayoutContent content when content.Root?.Manager is { } manager => manager.GetLayoutItemFromModel(content),
        _ => DependencyProperty.UnsetValue
    };
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is LayoutItem item ? item.Model! : DependencyProperty.UnsetValue;
    internal static LayoutItem? Item(object value) => value as LayoutItem ?? (value is LayoutContent content ? content.Root?.Manager?.GetLayoutItemFromModel(content) : null);
}
public class ActivateCommandLayoutItemFromLayoutModelConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (object?)LayoutItemFromLayoutModelConverter.Item(value)?.ActivateCommand ?? DependencyProperty.UnsetValue;
}
public class AutoHideCommandLayoutItemFromLayoutModelConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (object?)(LayoutItemFromLayoutModelConverter.Item(value) as LayoutAnchorableItem)?.AutoHideCommand ?? DependencyProperty.UnsetValue;
}
public class HideCommandLayoutItemFromLayoutModelConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (object?)(LayoutItemFromLayoutModelConverter.Item(value) as LayoutAnchorableItem)?.HideCommand ?? DependencyProperty.UnsetValue;
}
public class AnchorableContextMenuAutoHideHeaderConverter : CompatibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value is true || value is LayoutAnchorable { IsAutoHidden: true }) ? Properties.Resources.Anchorable_Dock : Properties.Resources.Anchorable_AutoHide;
}
/// <summary>Callable multi-value adapter. WinUI does not implement WPF MultiBinding; compose a view-model property for XAML bindings.</summary>
public class AnchorableContextMenuHideVisibilityConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values.Length > 0 && values.All(v => v is true) ? Visibility.Visible : Visibility.Collapsed;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}
