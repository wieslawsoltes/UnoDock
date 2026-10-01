using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.NativeWinUI;
/// <summary>A native WinUI 3 host that never registers its Window with the
/// library. With UNODOCK_SMOKE=&lt;result file&gt; it verifies owner, drop targeting
/// and shutdown behavior, then closes its main window; the process must exit,
/// which requires the floating windows to close with their owner.</summary>
public partial class App : Application
{
    private Window? _window;
    private static string? ResultPath => Environment.GetEnvironmentVariable("UNODOCK_SMOKE") is { Length: > 0 } path ? path : null;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Note("FAIL unhandled exception (" + e.Message + " | " + e.Exception + ")");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Note("FAIL unhandled exception (" + e.ExceptionObject + ")");
        TaskScheduler.UnobservedTaskException += (_, e) => Note("FAIL unobserved task exception (" + e.Exception + ")");
    }

    private static void Note(string line)
    {
        if (ResultPath is { } path)
            File.AppendAllLines(path + ".log", [line]);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Note("INFO launched");
        try
        {
            Launch();
        }
        catch (Exception error)
        {
            Note("FAIL launch (" + error + ")");
            throw;
        }
    }

    private void Launch()
    {
        var output = new LayoutAnchorable
        {
            Title = "Output",
            ContentId = "output",
            Content = new TextBox
            {
                Text = "Build succeeded.",
                AcceptsReturn = true
            }
        };
        var tools = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Explorer", ContentId = "explorer", Content = new TextBlock { Text = "Program.cs" } })
        {
            DockWidth = new(220)
        };
        tools.Children.Add(output);
        var documents = new LayoutDocumentPane(new LayoutDocument { Title = "Program.cs", ContentId = "program", Content = new TextBox { Text = "class Program {}", AcceptsReturn = true } });
        var panel = new LayoutPanel(tools);
        panel.Children.Add(documents);
        var manager = new DockingManager
        {
            Theme = new GenericTheme(),
            Layout = new()
            {
                RootPanel = panel
            }
        };
        Note("INFO manager created");
        _window = new Window
        {
            Title = "UnoDock native WinUI",
            Content = manager
        };
        _window.AppWindow.Resize(new()
        {
            Width = 1200,
            Height = 800
        });
        if (Environment.GetEnvironmentVariable("UNODOCK_SMOKE") is { Length: > 0 } result)
        {
            var started = false;
            manager.Loaded += async (_, _) =>
            {
                if (started)
                    return;
                started = true;
                Note("INFO loaded");
                try
                {
                    await SelfTest(_window, manager, output, documents, result);
                }
                catch (Exception error)
                {
                    Note("FAIL self-test (" + error + ")");
                }
            };
        }

        _window.Activate();
        Note("INFO window activated");
    }

    private static async Task SelfTest(Window window, DockingManager manager, LayoutAnchorable output, LayoutDocumentPane documents, string path)
    {
        var lines = new List<string>();
        void Record(string name, bool ok, string detail = "") => lines.Add($"{(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " (" + detail + ")" : "")}");
        try
        {
            await Task.Delay(800);
            var main = WinRT.Interop.WindowNative.GetWindowHandle(window);
            output.Float();
            var floating = await Until(() => manager.FloatingWindows.FirstOrDefault(w => w.NativeWindow != null && w.IsLoaded));
            Record("native floating window opened", floating != null);
            if (floating?.NativeWindow is { } native)
            {
                await Task.Delay(300);
                var child = WinRT.Interop.WindowNative.GetWindowHandle(native);
                var owner = GetWindow(child, 4);
                Record("floating window is owned by the unregistered main window", owner == main, $"owner={owner} main={main}");
                var view = manager.FindVisualChildren<LayoutDocumentPaneControl>().First(p => ReferenceEquals(p.Model, documents));
                var center = view.TransformToVisual(manager).TransformPoint(new(view.ActualWidth / 2, view.ActualHeight / 2));
                var plan = manager.GetDropPlan(output, center);
                Record("drop targets resolve over the main window", plan != null, plan?.Type.ToString() ?? "no plan");
                if (plan != null)
                {
                    plan.Execute();
                    await Task.Delay(300);
                    Record("dropping docks the floating tool into the main window", !output.IsFloating && output.Parent is LayoutDocumentPane);
                }
            }

            output.Float();
            await Until(() => manager.FloatingWindows.FirstOrDefault(w => w.NativeWindow != null && w.IsLoaded));
            // Diagnostic variant: close the main window with nothing floating.
            if (Environment.GetEnvironmentVariable("UNODOCK_SMOKE_DOCK_FIRST") == "1")
            {
                output.Dock();
                await Task.Delay(800);
                Note("INFO docked before closing; floating=" + manager.FloatingWindows.Count());
            }
        }
        catch (Exception error)
        {
            Record("self-test", false, error.GetType().Name + ": " + error.Message);
        }

        File.WriteAllLines(path, lines);
        Note("INFO closing main window");
        // Floating windows must close with the main window, or the process stays alive.
        window.Close();
    }

    private static async Task<T?> Until<T>(Func<T?> probe)
        where T : class
    {
        for (var i = 0; i < 200; i++)
        {
            if (probe() is { } value)
                return value;
            await Task.Delay(25);
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
}
