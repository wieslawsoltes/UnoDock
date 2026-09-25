using System.Globalization;

namespace UnoDock.Compatibility;
/// <summary>Declares the input and output types of an independently implemented converter.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ValueConversionAttribute(Type sourceType, Type targetType) : Attribute
{
    public Type SourceType { get; } = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
    public Type TargetType { get; } = targetType ?? throw new ArgumentNullException(nameof(targetType));
    public Type? ParameterType { get; set; }
}
