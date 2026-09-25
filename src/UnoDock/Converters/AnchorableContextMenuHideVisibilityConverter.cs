using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;
/// <summary>Returns the first value unless a two-input hide flag suppresses visibility.
/// Input identity, non-visibility values and invalid-array exceptions follow the observed contract.</summary>
public class AnchorableContextMenuHideVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Length == 2 && values[1] is true ? Visibility.Collapsed : values[0];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException(); // Reference contract: reverse conversion is unsupported.
}
