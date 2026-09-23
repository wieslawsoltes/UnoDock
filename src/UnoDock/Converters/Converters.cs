using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;

/// <summary>Legacy preview extension base. Concrete compatibility converters use the original
/// non-virtual member shapes; this base remains available to existing derived converters.</summary>
public abstract class CompatibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

internal static class ConverterInterop
{
    internal static CultureInfo Culture(string language)
    {
        try { return string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(language); }
        catch (CultureNotFoundException) { return CultureInfo.CurrentUICulture; }
    }
    // Native WinUI uses UnsetValue to invoke FallbackValue. It has no equivalent of WPF
    // DoNothing. ConverterBinding bypasses this adapter to retain exact no-transfer behavior.
    internal static object Native(object value) => ReferenceEquals(value, BindingValue.DoNothing) ? DependencyProperty.UnsetValue : value;
    internal static AnchorSide Side(object value) => value switch
    {
        null => throw new NullReferenceException(),
        AnchorSide side => side,
        int side => (AnchorSide)side,
        _ => throw new InvalidCastException()
    };
    internal static LayoutItem? Item(object value) => value is LayoutContent content && content.Root?.Manager is { } manager
        ? manager.GetLayoutItemFromModel(content) : null;
    // This is the observed reference contract for unsupported reverse directions, not
    // an unimplemented forward conversion. See the public-call fixtures and regression suite.
    internal static object UnsupportedReverse() => throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value == null || targetType == typeof(Visibility) && value is false ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        targetType == typeof(bool) && value is Visibility visibility ? visibility == Visibility.Visible : throw new ArgumentException("Expected a Visibility value and Boolean target.");
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        targetType == typeof(Visibility) && value is bool flag ? flag ? Visibility.Collapsed : Visibility.Visible : throw new ArgumentException("Expected a Boolean value and Visibility target.");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        targetType == typeof(bool) && value is Visibility visibility ? visibility != Visibility.Visible : throw new ArgumentException("Expected a Visibility value and Boolean target.");
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

[ValueConversion(typeof(AnchorSide), typeof(Orientation))]
public class AnchorSideToOrientationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ConverterInterop.Side(value) is AnchorSide.Left or AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

[ValueConversion(typeof(AnchorSide), typeof(double))]
public class AnchorSideToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ConverterInterop.Side(value) is AnchorSide.Left or AnchorSide.Right ? 90d : BindingValue.DoNothing;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class NullToDoNothingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value ?? BindingValue.DoNothing;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class UriSourceToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return BindingValue.DoNothing;
        var uri = (Uri)value;
        // The original public converter returns an Image CONTROL, despite its name.
        // Image-source identity passthrough and string-to-URI coercion are not its contract.
        return new Image { Source = new BitmapImage(uri) };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class LayoutItemFromLayoutModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.Item(value)!;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class ActivateCommandLayoutItemFromLayoutModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.Item(value)?.ActivateCommand!;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class AutoHideCommandLayoutItemFromLayoutModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.Item(value) switch
    {
        LayoutAnchorableItem item => item.AutoHideCommand!,
        null => null!,
        _ => BindingValue.DoNothing
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class HideCommandLayoutItemFromLayoutModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.Item(value) switch
    {
        LayoutAnchorableItem item => item.HideCommand!,
        null => null!,
        _ => BindingValue.DoNothing
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

public class AnchorableContextMenuAutoHideHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Properties.Resources.Window_Restore : Properties.Resources.Anchorable_AutoHide;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}

/// <summary>Returns the first value unless a two-input hide flag suppresses visibility.
/// Input identity, non-visibility values and invalid-array exceptions follow the observed contract.</summary>
public class AnchorableContextMenuHideVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Length == 2 && values[1] is true ? Visibility.Collapsed : values[0];
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException(); // Reference contract: reverse conversion is unsupported.
}
