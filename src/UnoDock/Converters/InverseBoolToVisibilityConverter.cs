using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => targetType == typeof(Visibility) && value is bool flag ? flag ? Visibility.Collapsed : Visibility.Visible : throw new ArgumentException("Expected a Boolean value and Visibility target.");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => targetType == typeof(bool) && value is Visibility visibility ? visibility != Visibility.Visible : throw new ArgumentException("Expected a Visibility value and Boolean target.");
    public object Convert(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}
