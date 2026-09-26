namespace UnoDock.Testing;

internal static partial class FloatingChromeTests
{
    private static void RegisterInitialLayout(TestRunner tests, bool tools)
    {
        var kind = tools ? "tools" : "document";
        tests.Test($"chrome/{kind}: initial client geometry settles without pointer input", async () =>
        {
            // Repeat cold native creation without moving a mouse to wake the host.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var fixture = new Fixture(tools);
                await fixture.Show();
                var native = fixture.Native;
                var frame = FloatingChromeProbe.Bounds(native);
                var client = (FrameworkElement)native.Content!;
                await Wait(() =>
                {
                    var scale = OperatingSystem.IsMacOS() ? 1 : client.XamlRoot!.RasterizationScale;
                    return Math.Abs(client.ActualWidth * scale - frame.Width) <= 1 && Math.Abs(client.ActualHeight * scale - frame.Height) <= 1;
                });
                Near(frame, FloatingChromeProbe.Bounds(native));
                Check.Same(native, fixture.Native);
                fixture.AssertEditors();
            }
        });
    }
}
