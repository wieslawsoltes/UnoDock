using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Server-side input synthesis for the opt-in Xvfb test suite only.
/// No input is generated unless UNODOCK_NATIVE_INPUT_TESTS is explicitly enabled.</summary>
internal sealed class X11TestInput : IDisposable
{
    private nint _display;
    private readonly HashSet<uint> _buttons = [];
    private readonly HashSet<byte> _keys = [];
    internal X11TestInput()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1")
            throw new InvalidOperationException("X11 input tests require an explicit opt-in on a dedicated test display.");
        _display = OpenDisplay(0);
        if (_display == 0)
            throw new InvalidOperationException("Cannot open the dedicated test display.");
        if (QueryExtension(_display, out _, out _, out _, out _) == 0)
        {
            Dispose();
            throw new InvalidOperationException("The test display does not support XTEST.");
        }
    }

    internal Point ScreenPoint(FrameworkElement element, Point point)
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        var root = element.XamlRoot ?? throw new InvalidOperationException("Target element is detached.");
        var window = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, root));
        if (Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native)
            throw new InvalidOperationException("Input test requires an X11 native window.");
        // The independent oracle uses Xlib; production geometry uses checked XCB.
        if (TranslateCoordinates(_display, native.WindowId, DefaultRootWindow(_display), 0, 0, out var x, out var y, out _) == 0)
            throw new InvalidOperationException("The native window is on another screen.");
        var client = element.TransformToVisual(null).TransformPoint(point);
        return new(x + client.X * root.RasterizationScale, y + client.Y * root.RasterizationScale);
    }

    internal void MoveTo(FrameworkElement element, Point point)
    {
        var screen = ScreenPoint(element, point);
        Check.True(FakeMotion(_display, -1, checked((int)Math.Round(screen.X)), checked((int)Math.Round(screen.Y)), 0) != 0);
        // Fence server-side motion before the next press is injected. Application
        // delivery remains asynchronous; callers still wait for actual UI state.
        Sync(_display, false);
    }

    internal async Task Click(FrameworkElement target)
    {
        // A newly opened popup can have a nonzero ActualHeight before its
        // native input position settles. Wait for stable geometry, then inject
        // exactly one click; do not retry a failed command or invoke it directly.
        Point? previous = null;
        var stable = 0;
        for (var i = 0; i < 100; i++)
        {
            if (!target.IsLoaded || target.ActualWidth <= 0 || target.ActualHeight <= 0)
            {
                stable = 0;
                previous = null;
                await Task.Delay(20);
                continue;
            }

            var local = new Point(target.ActualWidth / 2, target.ActualHeight / 2);
            var screen = ScreenPoint(target, local);
            MoveTo(target, local);
            if (previous == screen)
                stable++;
            else
                stable = 0;
            previous = screen;
            await Task.Delay(20);
            if (stable < 3 || !target.IsLoaded || ScreenPoint(target, local) != screen)
                continue;
            Press();
            await Task.Delay(40);
            Release();
            return;
        }

        throw new InvalidOperationException("The native click target did not acquire stable input geometry.");
    }

    internal void Press(uint button = 1)
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        Check.True(FakeButton(_display, button, 1, 0) != 0);
        _buttons.Add(button);
        Flush(_display);
    }

    internal void Release(uint button = 1)
    {
        if (!_buttons.Remove(button) || _display == 0)
            return;
        FakeButton(_display, button, 0, 0);
        Flush(_display);
    }

    internal void KeyDown(nuint keysym)
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        var key = KeysymToKeycode(_display, keysym);
        Check.True(key != 0);
        Check.True(FakeKey(_display, key, 1, 0) != 0);
        _keys.Add(key);
        Flush(_display);
    }

    internal void KeyUp(nuint keysym)
    {
        ObjectDisposedException.ThrowIf(_display == 0, this);
        var key = KeysymToKeycode(_display, keysym);
        Check.True(key != 0);
        Check.True(FakeKey(_display, key, 0, 0) != 0);
        _keys.Remove(key);
        Flush(_display);
    }

    internal void KeyPress(nuint keysym)
    {
        KeyDown(keysym);
        KeyUp(keysym);
    }

    internal void Escape() => KeyPress(0xff1b);
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
        foreach (var button in _buttons.ToArray())
            Release(button);
        if (_display != 0)
        {
            foreach (var key in _keys)
                FakeKey(_display, key, 0, 0);
            _keys.Clear();
            Flush(_display);
            CloseDisplay(_display);
            _display = 0;
        }
    }

    [DllImport("libX11.so.6", EntryPoint = "XSync")]
    private static extern int Sync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);
    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
    private static extern nint OpenDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
    private static extern int CloseDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
    private static extern nint DefaultRootWindow(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XFlush")]
    private static extern int Flush(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XTranslateCoordinates")]
    private static extern int TranslateCoordinates(nint display, nint source, nint destination, int sourceX, int sourceY, out int destinationX, out int destinationY, out nint child);
    [DllImport("libX11.so.6", EntryPoint = "XKeysymToKeycode")]
    private static extern byte KeysymToKeycode(nint display, nuint keysym);
    [DllImport("libXtst.so.6", EntryPoint = "XTestQueryExtension")]
    private static extern int QueryExtension(nint display, out int eventBase, out int errorBase, out int major, out int minor);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeMotionEvent")]
    private static extern int FakeMotion(nint display, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeButtonEvent")]
    private static extern int FakeButton(nint display, uint button, int pressed, nuint delay);
    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeKeyEvent")]
    private static extern int FakeKey(nint display, uint keycode, int pressed, nuint delay);
}
