using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace UnoDock.Testing;
public static class Check
{
    public static void True(bool condition, string? message = null) { if (!condition) throw new InvalidOperationException(message ?? "Assertion failed."); }
    public static void False(bool condition) => True(!condition);
    public static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; actual {actual}."); }
    public static void Same(object? expected, object? actual) => True(ReferenceEquals(expected, actual), "Object identity changed.");
    public static void Near(double expected, double actual, double tolerance = 1e-7) => True(double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance, $"Expected {expected:R}; actual {actual:R}.");
    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T error) { return error; }
        throw new InvalidOperationException("Expected exception " + typeof(T).Name);
    }
}
