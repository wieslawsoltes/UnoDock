using System.Globalization;

namespace UnoDock.Compatibility;

/// <summary>Culture-aware multi-value contract. Use ConverterBinding for explicit source subscriptions;
/// this does not introduce a WPF MultiBinding parser into native WinUI XAML.</summary>
public interface IMultiValueConverter
{
    object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
    object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
}
