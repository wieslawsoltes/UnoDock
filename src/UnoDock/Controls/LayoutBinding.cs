using Microsoft.UI.Xaml.Data;

namespace UnoDock.Controls;
/// <summary>A shareable XAML description of a per-item native binding. Use it in
/// LayoutItemBindings attached-property setters, not in a scalar Setter.Value.</summary>
public sealed partial class LayoutBinding : DependencyObject
{
    private object? _source;
    private bool _hasSource;
    public string Path
    {
        get;
        set;
    } = "";
    public BindingMode Mode
    {
        get;
        set;
    } = BindingMode.OneWay;
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
    } = "";
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
    public UpdateSourceTrigger UpdateSourceTrigger
    {
        get;
        set;
    } = UpdateSourceTrigger.Default;

    public object? Source
    {
        get => _source;
        set
        {
            _source = value;
            _hasSource = true;
        }
    }

    internal Binding CreateBinding()
    {
        if (!Enum.IsDefined(Mode))
            throw new ArgumentOutOfRangeException(nameof(Mode));
        if (!Enum.IsDefined(UpdateSourceTrigger))
            throw new ArgumentOutOfRangeException(nameof(UpdateSourceTrigger));
        var binding = new Binding
        {
            Path = new PropertyPath(Path ?? ""),
            Mode = Mode,
            Converter = Converter,
            ConverterParameter = ConverterParameter,
            UpdateSourceTrigger = UpdateSourceTrigger
        };
        if (_hasSource)
            binding.Source = _source;
        if (!string.IsNullOrEmpty(ConverterLanguage))
            binding.ConverterLanguage = ConverterLanguage;
        if (!ReferenceEquals(FallbackValue, DependencyProperty.UnsetValue))
            binding.FallbackValue = FallbackValue;
        if (!ReferenceEquals(TargetNullValue, DependencyProperty.UnsetValue))
            binding.TargetNullValue = TargetNullValue;
        return binding;
    }
}
