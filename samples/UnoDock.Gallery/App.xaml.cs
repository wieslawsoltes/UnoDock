namespace UnoDock.Gallery;

public partial class App : Application
{
    private Window? _window;
    private bool _selfTestStarted;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window
        {
            Title = "UnoDock Samples"
        };
        var gallery = new GalleryPage();
        _window.Content = gallery;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        var windowRegistration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
        _window.Closed += (_, _) =>
        {
            windowRegistration.Dispose();
            gallery.Dispose();
        };
        if (Environment.GetEnvironmentVariable("UNODOCK_SELFTEST") == "1")
            gallery.Loaded += async (_, _) =>
            {
                if (_selfTestStarted)
                    return;
                _selfTestStarted = true;
                var exitCode = 2;
                try
                {
                    await Task.Delay(300);
                    var output = Environment.GetEnvironmentVariable("UNODOCK_TEST_RESULTS") ?? "artifacts/test-results";
                    var requested = Environment.GetEnvironmentVariable("UNODOCK_TEST_SUITE");
                    var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]
                    {
                        ("xaml-workspaces", true, () => Testing.XamlWorkspaceTests.Run(output)),
                        ("xaml-workbench", true, () => Testing.XamlWorkbenchTests.Run(output)),
                        ("layout-mutation-invariants", true, () => Testing.LayoutMutationInvariantTests.Run(output)),
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
                        ("accessibility-quality", true, () => Testing.AccessibilityQualityTests.Run(output)),
                        ("mac-native", false, () => Testing.MacNativeTests.Run(output)),
                        ("desktop-floating", true, () => Testing.DesktopFloatingTests.Run(output)),
                        ("floating-drag-cleanup", true, () => Testing.FloatingDragCleanupTests.Run(output)),
                        ("floating-chrome-documents", true, () => Testing.FloatingChromeTests.Run(output, false)),
                        ("floating-chrome-tools", true, () => Testing.FloatingChromeTests.Run(output, true)),
                        ("floating-resize-policy-documents", true, () => Testing.FloatingChromeTests.RunResizePolicy(output, false)),
                        ("floating-resize-policy-tools", true, () => Testing.FloatingChromeTests.RunResizePolicy(output, true)),
                        ("uno-theme", true, () => Testing.UnoThemeTests.Run(output)),
                        ("windows-floating-input", true, () => Testing.WindowsFloatingInputTests.Run(output))
                    };
                    var selected = suites.Where(s => string.IsNullOrEmpty(requested) || requested == "all" || (requested == "windows-acceptance" ? s.Windows : requested == "desktop-acceptance" ? s.Name is "mac-native" or "desktop-floating" or "floating-drag-cleanup" or "uno-theme" or "windows-floating-input" : requested == "floating-resize-policy" ? s.Name.StartsWith("floating-resize-policy-", StringComparison.Ordinal) : requested == "floating-chrome" ? s.Name is "floating-chrome-documents" or "floating-chrome-tools" or "floating-resize-policy-documents" or "floating-resize-policy-tools" : s.Name == requested)).ToArray();
                    if (selected.Length == 0)
                        throw new ArgumentException("Unknown UNODOCK_TEST_SUITE: " + requested);
                    // Registry-owned platform selection also drives isolated CI.
                    // A platform no-op is not an executed (or passed) test suite.
                    selected = selected.Where(s => (s.Name != "mac-native" || OperatingSystem.IsMacOS()) && (s.Name != "windows-floating-input" || OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")).ToArray();
                    exitCode = 0;
                    if (Environment.GetEnvironmentVariable("UNODOCK_LIST_TESTS") == "1")
                    {
                        Directory.CreateDirectory(output);
                        await File.WriteAllTextAsync(Path.Combine(output, "selected-suites.json"), System.Text.Json.JsonSerializer.Serialize(selected.Select(s => s.Name).ToArray()));
                    }
                    else
                        foreach (var suite in selected)
                            exitCode |= await suite.Run();
                }
                catch (Exception e)
                {
                    exitCode = 2;
                    Console.Error.WriteLine(e);
                }
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
