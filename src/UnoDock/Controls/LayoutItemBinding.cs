using Microsoft.UI.Xaml.Data;

namespace UnoDock.Controls;
/// <summary>Declarative per-item Binding settings. Each application creates a fresh native Binding.</summary>
public sealed partial class LayoutItemBinding : DependencyObject
{
    public string Property
    {
        get;
        set;
    } = string.Empty;
    public string Path
    {
        get;
        set;
    } = string.Empty;
    public BindingMode Mode
    {
        get;
        set;
    } = BindingMode.OneWay;
    public UpdateSourceTrigger UpdateSourceTrigger
    {
        get;
        set;
    } = UpdateSourceTrigger.Default;
    public object? Source
    {
        get;
        set;
    }
    public string? ElementName
    {
        get;
        set;
    }
    public RelativeSource? RelativeSource
    {
        get;
        set;
    }
    public IValueConverter? Converter
    {
        get;
        set;
    }
    public object? ConverterParameter
    {
        get;
        set;
    }
    public string ConverterLanguage
    {
        get;
        set;
    } = string.Empty;
    public object? FallbackValue
    {
        get;
        set;
    } = DependencyProperty.UnsetValue;
    public object? TargetNullValue
    {
        get;
        set;
    } = DependencyProperty.UnsetValue;

    internal Binding CreateBinding()
    {
        if (string.IsNullOrWhiteSpace(Property))
            throw new ArgumentException("A binding target property is required.");
        if (!Enum.IsDefined(Mode))
            throw new ArgumentOutOfRangeException(nameof(Mode));
        if (!Enum.IsDefined(UpdateSourceTrigger))
            throw new ArgumentOutOfRangeException(nameof(UpdateSourceTrigger));
        var selectors = (Source != null ? 1 : 0) + (!string.IsNullOrEmpty(ElementName) ? 1 : 0) + (RelativeSource != null ? 1 : 0);
        if (selectors > 1)
            throw new ArgumentException("Specify only one of Source, ElementName or RelativeSource.");
        var result = new Binding
        {
            Path = new PropertyPath(Path),
            Mode = Mode,
            UpdateSourceTrigger = UpdateSourceTrigger,
            Converter = Converter,
            ConverterParameter = ConverterParameter,
            ConverterLanguage = ConverterLanguage
        };
        if (Source != null)
            result.Source = Source;
        if (ElementName != null)
            result.ElementName = ElementName;
        if (RelativeSource != null)
            result.RelativeSource = RelativeSource;
        if (FallbackValue != DependencyProperty.UnsetValue)
            result.FallbackValue = FallbackValue;
        if (TargetNullValue != DependencyProperty.UnsetValue)
            result.TargetNullValue = TargetNullValue;
        return result;
    }
}
