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
                    var requested = Environment.GetEnvironmentVariable("UNODOCK_TEST_SUITE");
                    // One ordered registry keeps standalone selectors and platform
                    // acceptance on the same code path. Windows flags preserve the
                    // existing selected-host scope; false is not a passed test.
                    var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]
                    {
                        ("runtime", false, () => Testing.RuntimeTests.Run(gallery.Dock, output)),
                        ("interop", false, () => Testing.InteropTests.Run(output)),
                        ("parity", false, () => Testing.ParityTests.Run(gallery.Dock, output)),
                        ("lifecycle", false, () => Testing.LifecycleTests.Run(gallery.Dock, output)),
                        ("interaction", false, () => Testing.InteractionTests.Run(gallery.Dock, output)),
                        ("converters", false, () => Testing.ConverterTests.Run(output)),
                        ("window-coordinates", false, () => Testing.WindowCoordinateTests.Run(output, gallery.Dock)),
                        ("shell", false, () => Testing.ShellTests.Run(output, gallery.Dock)),
                        ("window-lifecycle", true, () => Testing.WindowLifecycleTests.Run(gallery.Dock, output)),
                        ("input-extensions", true, () => Testing.InputRoutingTrace.Run(gallery.Dock, output)),
                        ("visual-parity", true, () => Testing.VisualParityTests.Run(gallery.Dock, output)),
                        ("navigator-quality", true, () => Testing.NavigatorQualityTests.Run(gallery.Dock, output)),
                        ("navigator-commit", true, () => Testing.NavigatorCommitTests.Run(output)),
                        ("navigator-revocation", true, () => Testing.NavigatorRevocationTests.Run(output)),
                        ("navigator-sample", true, () => Testing.NavigatorSampleTests.Run(output)),
                        ("focus-ownership", true, () => Testing.FocusOwnershipTests.Run(output)),
                        ("docking-guides", true, () => Testing.DockGuideTests.Run(gallery.Dock, output)),
                        ("splitter-quality", true, () => Testing.SplitterQualityTests.Run(gallery.Dock, output)),
                        ("auto-hide-quality", true, () => Testing.AutoHideQualityTests.Run(gallery.Dock, output)),
                        ("menu-quality", true, () => Testing.MenuQualityTests.Run(gallery.Dock, output)),
                        ("menu-context-lifetime", true, () => Testing.MenuContextLifetimeTests.Run(output)),
                        ("dropdown-quality", true, () => Testing.DropDownQualityTests.Run(output)),
                        ("sample-quality", true, () => Testing.SampleQualityTests.Run(gallery, output)),
                        ("presentation-quality", true, () => Testing.PresentationQualityTests.Run(output)),
                        ("inspector-quality", true, () => Testing.InspectorQualityTests.Run(output)),
                        ("restore-ownership", true, () => Testing.RestoreOwnershipTests.Run(output)),
                        ("mvvm-workspace", true, () => Testing.MvvmWorkspaceTests.Run(output)),
                        ("source-ownership", true, () => Testing.SourceOwnershipTests.Run(output)),
                        ("source-identity", true, () => Testing.SourceIdentityTests.Run(output)),
                        ("mvvm-chrome", true, () => Testing.MvvmChromeTests.Run(output)),
                        ("accessibility-quality", true, () => Testing.AccessibilityQualityTests.Run(output))
                    };
                    var selected = suites.Where(s => string.IsNullOrEmpty(requested) || requested == "all" ||
                        (requested == "windows-acceptance" ? s.Windows : s.Name == requested)).ToArray();
                    if (selected.Length == 0) throw new ArgumentException("Unknown UNODOCK_TEST_SUITE: " + requested);
                    exitCode = 0;
                    foreach (var suite in selected) exitCode |= await suite.Run();
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
