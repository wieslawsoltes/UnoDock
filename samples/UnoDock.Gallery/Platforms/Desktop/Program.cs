using Uno.UI.Hosting;

namespace UnoDock.Gallery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Virtual machines without a working GPU driver can opt into Skia's
        // software rasterizer; physical desktops keep the accelerated default.
        if (Environment.GetEnvironmentVariable("UNODOCK_SOFTWARE_RENDERING") == "1")
        {
            Uno.UI.FeatureConfiguration.Rendering.UseOpenGLOnWin32 = false;
            Uno.UI.FeatureConfiguration.Rendering.UseVulkanOnWin32 = false;
            Uno.UI.FeatureConfiguration.Rendering.UseOpenGLOnX11 = false;
            Uno.UI.FeatureConfiguration.Rendering.UseVulkanOnX11 = false;
        }

        Run();
    }

    private static void Run() => UnoPlatformHostBuilder.Create().App(() => new App()).UseX11().UseLinuxFrameBuffer().UseMacOS().UseWin32().Build().Run();
}
