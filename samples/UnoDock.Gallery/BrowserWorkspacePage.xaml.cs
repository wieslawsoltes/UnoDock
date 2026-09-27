using UnoDock.Browser;
using UnoDock.Layout;

namespace UnoDock.Gallery;

public sealed partial class BrowserWorkspacePage : UserControl
{
    private BrowserDockingSession? _session;
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };
    public BrowserWorkspacePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _timer.Tick += (_, _) =>
        {
            _session?.Poll();
            Status.Text = _session?.LastError ?? "Native Uno controls · Drag tabs to split locally; drag browser chips between windows.";
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_session != null)
            return;
        try
        {
            _session = new BrowserDockingSession(Dock, Invoke, new BrowserTextViewFactory());
            _timer.Start();
        }
        catch (Exception error)
        {
            Status.Text = "Browser workspace startup failed: " + error.Message;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _timer.Stop();
        _session?.Dispose();
        _session = null;
    }

    private void FloatSelected(object sender, RoutedEventArgs args)
    {
        if (Dock.Layout.ActiveContent?.ContentId is { } id)
            _session?.Float(id);
    }

    private void DockSelected(object sender, RoutedEventArgs args) => Dock.Layout.ActiveContent?.Dock();
    private void AutoHideSelected(object sender, RoutedEventArgs args)
    {
        if (Dock.Layout.ActiveContent is LayoutAnchorable tool)
            tool.ToggleAutoHide();
    }

    private static string Invoke(string script)
    {
#if __WASM__
        return Uno.Foundation.WebAssemblyRuntime.InvokeJS(script);
#else
        throw new PlatformNotSupportedException("The browser workspace requires WebAssembly.");
#endif
    }
}
