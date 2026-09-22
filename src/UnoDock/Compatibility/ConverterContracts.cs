using System.Globalization;

namespace Xceed.Wpf.AvalonDock.Compatibility;

/// <summary>Declares the input and output types of an independently implemented converter.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ValueConversionAttribute(Type sourceType, Type targetType) : Attribute
{
    public Type SourceType { get; } = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
    public Type TargetType { get; } = targetType ?? throw new ArgumentNullException(nameof(targetType));
    public Type? ParameterType { get; set; }
}

/// <summary>Culture-aware multi-value contract. Use ConverterBinding for explicit source subscriptions;
/// this does not introduce a WPF MultiBinding parser into native WinUI XAML.</summary>
public interface IMultiValueConverter
{
    object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
    object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
}

/// <summary>Binding transfer results that cannot be represented by WinUI's UnsetValue alone.</summary>
public static class BindingValue
{
    /// <summary>Suppresses a transfer without clearing the destination or applying its fallback.
    /// Consume through the CultureInfo converter overload and ConverterBinding. Native WinUI
    /// converter overloads explicitly map this to UnsetValue because WinUI has no skip sentinel.</summary>
    public static object DoNothing { get; } = new NoTransfer();
    private sealed class NoTransfer { public override string ToString() => "Binding.DoNothing"; }
}
