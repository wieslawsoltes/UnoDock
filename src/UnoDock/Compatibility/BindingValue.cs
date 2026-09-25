using System.Globalization;

namespace UnoDock.Compatibility;
/// <summary>Binding transfer results that cannot be represented by WinUI's UnsetValue alone.</summary>
public static class BindingValue
{
    /// <summary>Suppresses a transfer without clearing the destination or applying its fallback.
    /// Consume through the CultureInfo converter overload and ConverterBinding. Native WinUI
    /// converter overloads explicitly map this to UnsetValue because WinUI has no skip sentinel.</summary>
    public static object DoNothing { get; } = new NoTransfer();

    private sealed class NoTransfer
    {
        public override string ToString() => "Binding.DoNothing";
    }
}
