using Uno.UI.Hosting;
namespace UnoDock.Gallery;
public static class Program
{
    public static async Task Main(string[] args) => await UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly().Build().RunAsync();
}
