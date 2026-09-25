using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;

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
