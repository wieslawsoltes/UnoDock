namespace UnoDock.Gallery;

public partial class App : Application
{
    private Window? _window;
    private bool _selfTestStarted;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "UnoDock — docking workbench" };
        var gallery = new GalleryPage();
        _window.Content = gallery;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        _window.Closed += (_, _) => gallery.Dock.Dispose();
        if (Environment.GetEnvironmentVariable("UNODOCK_SELFTEST") == "1")
            gallery.Loaded += async (_, _) =>
            {
                // Loaded may be delivered again when a host is reattached.
                if (_selfTestStarted) return;
                _selfTestStarted = true;
                var exitCode = 2;
                try
                {
                    await Task.Delay(300);
                    var output = Environment.GetEnvironmentVariable("UNODOCK_TEST_RESULTS") ?? "artifacts/test-results";
                    exitCode = await Testing.RuntimeTests.Run(gallery.Dock, output);
                    exitCode |= await Testing.InteropTests.Run(output);
                    exitCode |= await Testing.ParityTests.Run(gallery.Dock, output);
                    exitCode |= await Testing.LifecycleTests.Run(gallery.Dock, output);
                }
                catch (Exception e) { Console.Error.WriteLine(e); }
                finally
                {
                    // Let native render loops unwind; Environment.Exit can tear down
                    // Skia/X11 while a render callback still owns native resources.
                    Environment.ExitCode = exitCode;
                    _window.Close();
                    Exit();
                }
            };
        _window.Activate();
    }
}
