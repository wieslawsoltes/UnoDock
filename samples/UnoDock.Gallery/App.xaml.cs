namespace UnoDock.Gallery;
public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "UnoDock — docking workbench" };
        var gallery = new GalleryPage(); _window.Content = gallery;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        _window.Closed += (_, _) => gallery.Dock.Dispose();
        if (Environment.GetEnvironmentVariable("UNODOCK_SELFTEST") == "1")
            gallery.Loaded += async (_, _) =>
            {
                try
                {
                    await Task.Delay(300);
                    var result = await Testing.RuntimeTests.Run(gallery.Dock, Environment.GetEnvironmentVariable("UNODOCK_TEST_OUTPUT") ?? "artifacts/test-results");
                    Environment.Exit(result);
                }
                catch (Exception e) { Console.Error.WriteLine(e); Environment.Exit(2); }
            };
        _window.Activate();
    }
}
