using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Converters;

internal static class ConverterInterop
{
    internal static CultureInfo Culture(string language)
    {
        try
        {
            return string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
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
    internal static LayoutItem? Item(object value) => value is LayoutContent content && content.Root?.Manager is { } manager ? manager.GetLayoutItemFromModel(content) : null;
    // This is the observed reference contract for unsupported reverse directions, not
    // an unimplemented forward conversion. See the public-call fixtures and regression suite.
    internal static object UnsupportedReverse() => throw new NotImplementedException();
}
