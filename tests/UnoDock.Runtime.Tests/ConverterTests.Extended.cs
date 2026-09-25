using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock;
using UnoDock.Compatibility;
using UnoDock.Converters;
using UnoDock.Layout;

namespace UnoDock.Testing;
public static partial class ConverterTests
{
    private static void ExtendedTests(TestRunner tests, DockingManager manager, LayoutDocument document, LayoutAnchorable tool)
    {
        using var stream = typeof(ConverterTests).Assembly.GetManifestResourceStream("ConverterFixtures.converter-details.xml") ?? throw new InvalidOperationException("Missing URI and multi-value reference observations.");
        var cases = XDocument.Load(stream).Root!.Elements().ToArray();
        tests.Test("complete extended converter fixture matrix", () => Check.Equal(108, cases.Length));
        var converter = new UriSourceToBitmapImageConverter();
        var multi = new AnchorableContextMenuHideVisibilityConverter();
        foreach (var record in cases)
        {
            var key = (string)record.Attribute("Input")!;
            var target = (string? )record.Attribute("Target") ?? "visibility";
            var reverse = (bool)record.Attribute("Reverse")!;
            tests.Test($"extended reference {record.Name}: {key} -> {target}, reverse={reverse}", () =>
            {
                // Conversion constructs an image; decoding is deferred until its
                // control is loaded. No network access or image assets are required.
                object? input = record.Name == "MultiCase" ? ExtendedValues(key) : key switch
                {
                    "null" => null,
                    "image" => new BitmapImage(),
                    "relative-uri" => new Uri("unodock-probe.png", UriKind.Relative),
                    "absolute-uri" => new Uri(Path.Combine(Path.GetTempPath(), "unodock-probe.png")),
                    "string" => "not-an-image",
                    "empty-string" => "",
                    "integer" => 17,
                    "boolean" => true,
                    _ => throw new InvalidOperationException("Unknown input " + key)};
                var type = target switch
                {
                    "image-source" => typeof(ImageSource),
                    "bitmap-image" => typeof(BitmapImage),
                    "uri" => typeof(Uri),
                    "string" => typeof(string),
                    _ => Target(target)};
                Verify(record.Elements().Single(), record.Name == "MultiCase" ? () => reverse ? multi.ConvertBack(Visibility.Visible, [typeof(bool), typeof(bool)], null!, Invariant) : multi.Convert((object[])input!, type, null!, Invariant) : () => reverse ? converter.ConvertBack(input!, type, null!, Invariant) : converter.Convert(input!, type, null!, Invariant), input, manager, document, tool, nameof(UriSourceToBitmapImageConverter));
            });
        }
    }

    private static object[]? ExtendedValues(string key) => key switch
    {
        "null-array" => null,
        "empty" => [],
        _ => key.Split(',').Select(value => value switch
        {
            "visible" => (object)Visibility.Visible,
            "collapsed" => Visibility.Collapsed,
            "true" => true,
            "false" => false,
            "null" => null!,
            "one" => 1,
            "string" => "true",
            "unset" => DependencyProperty.UnsetValue,
            "nothing" => BindingValue.DoNothing,
            _ => throw new InvalidOperationException("Unknown multi-value input " + value)}).ToArray()};
}
