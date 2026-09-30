using System.Globalization;

namespace UnoDock.Internal;
/// <summary>Reads a theme thickness written as a <c>Thickness</c>, a uniform
/// <c>x:Double</c> or a "left,top,right,bottom" string. Each side is clamped;
/// anything else reports no value so the caller keeps its fallback.</summary>
internal static class DockThicknessResource
{
    internal static Thickness? Read(object? value, double min, double max)
    {
        double C(double side) => double.IsFinite(side) ? Math.Clamp(side, min, max) : min;
        switch (value)
        {
            case Thickness t:
                return new(C(t.Left), C(t.Top), C(t.Right), C(t.Bottom));
            case double d when double.IsFinite(d):
                return new(C(d));
            case string s:
                var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var numbers = new double[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                    if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
                        return null;
                return numbers.Length switch
                {
                    1 => new(C(numbers[0])),
                    2 => new(C(numbers[0]), C(numbers[1]), C(numbers[0]), C(numbers[1])),
                    4 => new(C(numbers[0]), C(numbers[1]), C(numbers[2]), C(numbers[3])),
                    _ => null
                };
            default:
                return null;
        }
    }
}
