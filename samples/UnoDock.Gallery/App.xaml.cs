namespace UnoDock.Gallery;

public partial class App : Application
{
    private Window? _window;
    private bool _selfTestStarted;
    public App()
    {
        InitializeComponent();
        // Self-test runs record unhandled exceptions: a native WinUI crash otherwise leaves
        // only an exit code behind.
        UnhandledException += (_, e) => ReportCrash("XAML", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => ReportCrash("Task", e.Exception);
    }

    private static void ReportCrash(string source, Exception? error)
    {
        var text = $"UNHANDLED {source}: {error}";
        Console.Error.WriteLine(text);
        if (Environment.GetEnvironmentVariable("UNODOCK_TEST_RESULTS") is { Length: > 0 } directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "crash.log"), text + Environment.NewLine);
            }
            catch (IOException)
            {
            }
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if HAS_UNO
        if (TryLaunchBrowserWorkspace())
            return;
#endif
        if (ReferenceScenario.Mode is { } scenario)
        {
            // Side-by-side review scene (see ReferenceScenario).
            _window = new Window
            {
                Title = "UnoDock - " + scenario
            };
            _window.Content = ReferenceScenario.Create(_window, scenario);
            _window.Activate();
            return;
        }

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
        // Presentation options for documentation screenshots and manual review.
        if (Enum.TryParse<SampleTheme>(Environment.GetEnvironmentVariable("UNODOCK_GALLERY_THEME"), true, out var startTheme))
            gallery.Loaded += (_, _) => gallery.SetSampleTheme(startTheme);
        if (Environment.GetEnvironmentVariable("UNODOCK_GALLERY_TABVIEW") == "1")
            gallery.Loaded += (_, _) => gallery.Dock.DocumentTabStripMode = DocumentTabStripMode.TabView;
        if (Environment.GetEnvironmentVariable("UNODOCK_GALLERY_FLOAT") is { Length: > 0 } floating)
            gallery.Loaded += (_, _) => gallery.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // Comma-separated content titles to float, for screenshots of floating chrome.
                var index = 0;
                foreach (var title in floating.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (gallery.Dock.Layout.Descendents().OfType<UnoDock.Layout.LayoutContent>().FirstOrDefault(c => c.Title == title) is { } content)
                    {
                        content.FloatingLeft = 260 + index * 380;
                        content.FloatingTop = 240 + index * 60;
                        content.FloatingWidth = 360;
                        content.FloatingHeight = 260;
                        content.Float();
                        index++;
                    }
            });
        // Size the window in device-independent pixels (AppWindow sizes are
        // physical), fit it to the monitor's work area and center it.
        var desired = Environment.GetEnvironmentVariable("UNODOCK_GALLERY_SIZE") is { } size && size.Split('x') is [var w, var h] && double.TryParse(w, out var width) && double.TryParse(h, out var height) ? new Windows.Foundation.Size(width, height) : new Windows.Foundation.Size(1440, 900);
        var placed = false;
        gallery.Loaded += (_, _) =>
        {
            // Self-tests keep the historical physical host size their geometry expects.
            if (placed || gallery.XamlRoot is not { } root || Environment.GetEnvironmentVariable("UNODOCK_SELFTEST") == "1" || !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
                return;
            placed = true;
            var areas = DesktopWindowCoordinates.GetWorkAreas(gallery);
            var area = areas.Count > 0 ? areas[0] : new Windows.Foundation.Rect(0, 0, desired.Width, desired.Height);
            var bounds = UnoDock.Core.FloatingPlacement.Fit(new(area.X + (area.Width - desired.Width) / 2, area.Y + (area.Height - desired.Height) / 2, desired.Width, desired.Height), areas.Select(a => new UnoDock.Core.DockRect(a.X, a.Y, a.Width, a.Height)).ToArray());
            DesktopWindowCoordinates.SetWindowBounds(_window, new(bounds.X, bounds.Y, bounds.Width, bounds.Height), root.RasterizationScale);
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
                        ("fluent-navigator", true, () => Testing.FluentNavigatorTests.Run(output)),
                        ("fluent-state-resources", true, () => Testing.FluentStateResourceTests.Run(output)),
                        ("xaml-workspaces", true, () => Testing.XamlWorkspaceTests.Run(output)),
                        ("xaml-workbench", true, () => Testing.XamlWorkbenchTests.Run(output)),
                        ("layout-mutation-invariants", true, () => Testing.LayoutMutationInvariantTests.Run(output)),
                        ("runtime", false, () => Testing.RuntimeTests.Run(gallery.Dock, output)),
                        ("interop", false, () => Testing.InteropTests.Run(output)),
                        ("reference-behavior", true, () => Testing.ReferenceBehaviorTests.Run(output)),
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
                        ("fluent-presentation", true, () => Testing.FluentPresentationTests.Run(output)),
                        ("docking-sizing", true, () => Testing.DockingSizingTests.Run(output)),
                        ("auto-hide-quality", true, () => Testing.AutoHideQualityTests.Run(gallery.Dock, output)),
                        ("menu-quality", true, () => Testing.MenuQualityTests.Run(gallery.Dock, output)),
                        ("menu-context-lifetime", true, () => Testing.MenuContextLifetimeTests.Run(output)),
                        ("dropdown-quality", true, () => Testing.DropDownQualityTests.Run(output)),
                        ("sample-quality", true, () => Testing.SampleQualityTests.Run(gallery, output)),
                        ("presentation-quality", true, () => Testing.PresentationQualityTests.Run(output)),
                        ("inspector-quality", true, () => Testing.InspectorQualityTests.Run(output)),
                        ("native-inspector", true, () => Testing.NativeInspectorTests.Run(output)),
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
                        ("classic-themes", true, () => Testing.ClassicThemeTests.Run(output)),
                        ("localization", true, () => Testing.LocalizationTests.Run(output)),
                        ("window-placement", true, () => Testing.WindowPlacementTests.Run(output)),
                        ("tabview-strip", true, () => Testing.TabViewStripTests.Run(output)),
                        ("templates-icons", true, () => Testing.TemplateIconTests.Run(output)),
                        ("windows-floating-input", true, () => Testing.WindowsFloatingInputTests.Run(output)),
                        ("tear-off", true, () => Testing.TearOffInputTests.Run(output))
                    };
                    var selected = suites.Where(s => string.IsNullOrEmpty(requested) || requested == "all" || (requested == "windows-acceptance" ? s.Windows : requested == "desktop-acceptance" ? s.Name is "mac-native" or "desktop-floating" or "floating-drag-cleanup" or "uno-theme" or "windows-floating-input" : requested == "floating-resize-policy" ? s.Name.StartsWith("floating-resize-policy-", StringComparison.Ordinal) : requested == "floating-chrome" ? s.Name is "floating-chrome-documents" or "floating-chrome-tools" or "floating-resize-policy-documents" or "floating-resize-policy-tools" : s.Name == requested)).ToArray();
                    if (selected.Length == 0)
                        throw new ArgumentException("Unknown UNODOCK_TEST_SUITE: " + requested);
                    // Registry-owned platform selection also drives isolated CI.
                    // A platform no-op is not an executed (or passed) test suite.
                    selected = selected.Where(s => (s.Name != "mac-native" || OperatingSystem.IsMacOS()) && (s.Name != "windows-floating-input" || OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1") && (s.Name != "tear-off" || (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")).ToArray();
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
