using Uno.UI.Hosting;

namespace UnoDock.Gallery;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Uno.UI.FeatureConfiguration.AutomationPeer.AutoEnableAccessibility = true;
        await UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly().Build().RunAsync();
    }
}
