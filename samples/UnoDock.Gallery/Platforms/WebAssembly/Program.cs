using Uno.UI.Hosting;

namespace UnoDock.Gallery;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Uno.UI.FeatureConfiguration.AutomationPeer.AutoEnableAccessibility = true;
        // Each browser window uses the real Skia software renderer. The pinned
        // WebGL path crashes on the reproduced browser configuration; this is a
        // shipped hosting policy, not a headless-only test flag or a mock canvas.
        await UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly(builder => builder.ForceSoftwareRendering()).Build().RunAsync();
    }
}
