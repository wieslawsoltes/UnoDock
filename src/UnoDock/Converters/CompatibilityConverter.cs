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
