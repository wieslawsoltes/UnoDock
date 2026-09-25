using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;

namespace UnoDock.Testing;
/// <summary>Bounded, observer-only native event evidence for acceptance failures.</summary>
public static class InputRoutingTrace
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        // Diagnostics must not influence the acceptance path merely by registering
        // extra routed handlers. The normal run has no observer on the root.
        if (Environment.GetEnvironmentVariable("UNODOCK_INPUT_TRACE") != "1")
            return await InputExtensionTests.Run(host, output);
        var root = host.XamlRoot?.Content ?? throw new InvalidOperationException("Input test host is detached.");
        var records = new Queue<Record>();
        var watch = Stopwatch.StartNew();
        PointerEventHandler pressed = (_, e) => Capture("pressed", e);
        PointerEventHandler released = (_, e) => Capture("released", e);
        PointerEventHandler lost = (_, e) => Capture("capture-lost", e);
        PointerEventHandler cancelled = (_, e) => Capture("cancelled", e);
        root.AddHandler(UIElement.PointerPressedEvent, pressed, true);
        root.AddHandler(UIElement.PointerReleasedEvent, released, true);
        root.AddHandler(UIElement.PointerCaptureLostEvent, lost, true);
        root.AddHandler(UIElement.PointerCanceledEvent, cancelled, true);
        var result = 2;
        try
        {
            result = await InputExtensionTests.Run(host, output);
            return result;
        }
        finally
        {
            root.RemoveHandler(UIElement.PointerPressedEvent, pressed);
            root.RemoveHandler(UIElement.PointerReleasedEvent, released);
            root.RemoveHandler(UIElement.PointerCaptureLostEvent, lost);
            root.RemoveHandler(UIElement.PointerCanceledEvent, cancelled);
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "input-routing-trace.json"), JsonSerializer.Serialize(new
            {
                schema = 1,
                result,
                records
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        void Capture(string phase, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(root);
            var path = new List<string>();
            for (var node = e.OriginalSource as DependencyObject; node != null && path.Count < 16; node = VisualTreeHelper.GetParent(node))
                path.Add(node is LayoutTabItemBase tab ? $"{node.GetType().Name}[{tab.Model?.Title}]" : node.GetType().Name);
            if (records.Count == 256)
                records.Dequeue();
            records.Enqueue(new(watch.Elapsed.TotalMilliseconds, phase, e.Pointer.PointerId, point.Properties.PointerUpdateKind.ToString(), point.Properties.IsLeftButtonPressed, point.Position.X, point.Position.Y, e.Handled, string.Join("/", path)));
        }
    }

    public sealed record Record(double Milliseconds, string Phase, uint Pointer, string Update, bool LeftPressed, double X, double Y, bool Handled, string Route);
}
