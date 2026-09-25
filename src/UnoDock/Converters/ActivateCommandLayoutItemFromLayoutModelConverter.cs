using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;
public class ActivateCommandLayoutItemFromLayoutModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.Item(value)?.ActivateCommand!;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}
