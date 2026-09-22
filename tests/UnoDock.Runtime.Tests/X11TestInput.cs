using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Server-side input synthesis for the opt-in Xvfb test suite only.
/// No input is generated unless UNODOCK_NATIVE_INPUT_TESTS is explicitly enabled.</summary>
internal sealed class X11TestInput : IDisposable
{
    private nint _display;
    private bool _pressed;

    internal X11TestInput()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1")
            throw new InvalidOperationException("X11 input tests require an explicit opt-in on a dedicated test display.");
        _display = OpenDisplay(0);
        if (_display == 0) throw new InvalidOperationException("Cannot open the dedicated test display.");
        if (QueryExtension(_display, out _, out _, out _, out _) == 0)
        {
            Dispose();
            throw new InvalidOperationException("The test display does not support XTEST.");
        }
    }

    internal void MoveTo(FrameworkElement element, Point point)
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        var root = element.XamlRoot ?? throw new InvalidOperationException("Target element is detached.");
        var window = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, root));
        if (Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native)
            throw new InvalidOperationException("Input test requires an X11 native window.");
        // Independently use Xlib for input positioning; the production converter uses XCB.
        if (TranslateCoordinates(_display, native.WindowId, DefaultRootWindow(_display), 0, 0, out var x, out var y, out _) == 0)
            throw new InvalidOperationException("The native window is on another screen.");
        var client = element.TransformToVisual(root.Content).TransformPoint(point);
        Check.True(FakeMotion(_display, -1, checked(x + (int)Math.Round(client.X * root.RasterizationScale)),
            checked(y + (int)Math.Round(client.Y * root.RasterizationScale)), 0) != 0);
        Flush(_display);
    }

    internal void Press()
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        Check.True(FakeButton(_display, 1, 1, 0) != 0);
        _pressed = true;
        Flush(_display);
    }

    internal void Release()
    {
        if (!_pressed || _display == 0) return;
        FakeButton(_display, 1, 0, 0);
        _pressed = false;
        Flush(_display);
    }

    internal void Escape()
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        var key = KeysymToKeycode(_display, 0xff1b);
        Check.True(key != 0);
        Check.True(FakeKey(_display, key, 1, 0) != 0);
        Check.True(FakeKey(_display, key, 0, 0) != 0);
        Flush(_display);
    }

    internal async Task Begin(FrameworkElement source, Point point)
    {
        MoveTo(source, point);
        await Task.Delay(50);
        Press();
        await Task.Delay(50);
        MoveTo(source, new(point.X + 12, point.Y));
        await Task.Delay(50);
    }

    internal async Task Drop(FrameworkElement target, Point point)
    {
        MoveTo(target, point);
        await Task.Delay(100);
        Release();
        await Task.Delay(100);
    }

    public void Dispose()
    {
        Release();
        if (_display != 0)
        {
            CloseDisplay(_display);
            _display = 0;
        }
    }

    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")] private static extern nint OpenDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")] private static extern int CloseDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")] private static extern nint DefaultRootWindow(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XFlush")] private static extern int Flush(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XTranslateCoordinates")] private static extern int TranslateCoordinates(nint display, nint source, nint destination, int sourceX, int sourceY, out int destinationX, out int destinationY, out nint child);
    [DllImport("libX11.so.6", EntryPoint = "XKeysymToKeycode")] private static extern byte KeysymToKeycode(nint display, nuint keysym);
    [DllImport("libXtst.so.6", EntryPoint = "XTestQueryExtension")] private static extern int QueryExtension(nint display, out int eventBase, out int errorBase, out int major, out int minor);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeMotionEvent")] private static extern int FakeMotion(nint display, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeButtonEvent")] private static extern int FakeButton(nint display, uint button, int pressed, nuint delay);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeKeyEvent")] private static extern int FakeKey(nint display, uint keycode, int pressed, nuint delay);
}
