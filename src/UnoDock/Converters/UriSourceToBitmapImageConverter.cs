using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;

public class UriSourceToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return BindingValue.DoNothing;
        var uri = (Uri)value;
        // The original public converter returns an Image CONTROL, despite its name.
        // Image-source identity passthrough and string-to-URI coercion are not its contract.
        return new Image
        {
            Source = new BitmapImage(uri)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ConverterInterop.UnsupportedReverse();
    public object Convert(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(Convert(value, targetType, parameter, ConverterInterop.Culture(language)));
    public object ConvertBack(object value, Type targetType, object parameter, string language) => ConverterInterop.Native(ConvertBack(value, targetType, parameter, ConverterInterop.Culture(language)));
}
