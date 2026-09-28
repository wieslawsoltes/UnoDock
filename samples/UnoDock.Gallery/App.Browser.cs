namespace UnoDock.Gallery;

public partial class App
{
    private bool TryLaunchBrowserWorkspace()
    {
#if __WASM__
        if (Uno.Foundation.WebAssemblyRuntime.InvokeJS("String(!!window.parent.UnoDockBrowser)") != "true")
            return false;
        _window = new Window { Content = new BrowserWorkspacePage() };
        _window.Activate();
        return true;
#else
        return false;
#endif
    }
}
