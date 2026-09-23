namespace UnoDock.Gallery;

public partial class App : Application
{
    private Window? _window;
    private bool _selfTestStarted;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "UnoDock Samples" };
        var gallery = new GalleryPage();
        _window.Content = gallery;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        var windowRegistration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
        _window.Closed += (_, _) => { windowRegistration.Dispose(); gallery.Dispose(); };
        if (Environment.GetEnvironmentVariable("UNODOCK_SELFTEST") == "1")
            gallery.Loaded += async (_, _) =>
            {
                if (_selfTestStarted) return;
                _selfTestStarted = true;
                var exitCode = 2;
                try
                {
                    await Task.Delay(300);
                    var output = Environment.GetEnvironmentVariable("UNODOCK_TEST_RESULTS") ?? "artifacts/test-results";
                    var suite = Environment.GetEnvironmentVariable("UNODOCK_TEST_SUITE");
                    if (suite == "presentation-quality")
                        exitCode = await Testing.PresentationQualityTests.Run(output);
                    else if (suite == "sample-quality")
                        exitCode = await Testing.SampleQualityTests.Run(gallery, output);
                    else if (suite == "dropdown-quality")
                        exitCode = await Testing.DropDownQualityTests.Run(output);
                    else if (suite == "menu-quality")
                        exitCode = await Testing.MenuQualityTests.Run(gallery.Dock, output);
                    else if (suite == "menu-context-lifetime")
                        exitCode = await Testing.MenuContextLifetimeTests.Run(output);
                    else if (suite == "auto-hide-quality")
                        exitCode = await Testing.AutoHideQualityTests.Run(gallery.Dock, output);
                    else if (suite == "splitter-quality")
                        exitCode = await Testing.SplitterQualityTests.Run(gallery.Dock, output);
                    else if (suite == "docking-guides")
                        exitCode = await Testing.DockGuideTests.Run(gallery.Dock, output);
                    else if (suite == "window-lifecycle")
                        exitCode = await Testing.WindowLifecycleTests.Run(gallery.Dock, output);
                    else if (suite == "navigator-quality")
                        exitCode = await Testing.NavigatorQualityTests.Run(gallery.Dock, output);
                    else if (suite == "visual-parity")
                        exitCode = await Testing.VisualParityTests.Run(gallery.Dock, output);
                    else if (suite == "input-extensions")
                        exitCode = await Testing.InputRoutingTrace.Run(gallery.Dock, output);
                    else if (suite == "windows-acceptance")
                    {
                        exitCode = await Testing.WindowLifecycleTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.InputRoutingTrace.Run(gallery.Dock, output);
                        exitCode |= await Testing.VisualParityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.NavigatorQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.DockGuideTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.SplitterQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.AutoHideQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.MenuQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.MenuContextLifetimeTests.Run(output);
                        exitCode |= await Testing.DropDownQualityTests.Run(output);
                        exitCode |= await Testing.SampleQualityTests.Run(gallery, output);
                        exitCode |= await Testing.PresentationQualityTests.Run(output);
                    }
                    else if (string.IsNullOrEmpty(suite) || suite == "all")
                    {
                        exitCode = await Testing.RuntimeTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.InteropTests.Run(output);
                        exitCode |= await Testing.ParityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.LifecycleTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.InteractionTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.ConverterTests.Run(output);
                        exitCode |= await Testing.WindowCoordinateTests.Run(output, gallery.Dock);
                        exitCode |= await Testing.ShellTests.Run(output, gallery.Dock);
                        exitCode |= await Testing.WindowLifecycleTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.InputRoutingTrace.Run(gallery.Dock, output);
                        exitCode |= await Testing.VisualParityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.NavigatorQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.DockGuideTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.SplitterQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.AutoHideQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.MenuQualityTests.Run(gallery.Dock, output);
                        exitCode |= await Testing.MenuContextLifetimeTests.Run(output);
                        exitCode |= await Testing.DropDownQualityTests.Run(output);
                        exitCode |= await Testing.SampleQualityTests.Run(gallery, output);
                        exitCode |= await Testing.PresentationQualityTests.Run(output);
                    }
                    else throw new ArgumentException("Unknown UNODOCK_TEST_SUITE: " + suite);
                }
                catch (Exception e) { exitCode = 2; Console.Error.WriteLine(e); }
                finally
                {
                    Environment.ExitCode = exitCode;
                    _window.Close();
                    Exit();
                }
            };
        _window.Activate();
    }
}
