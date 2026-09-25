using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Actual AppKit acceptance, not simulated platform behavior. A command-
/// line launched CI app must explicitly become foreground before testing utility
/// windows whose documented behavior is to hide when their application is inactive.</summary>
internal static class MacNativeTests
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    internal static async Task<int> Run(string output)
    {
        if (!OperatingSystem.IsMacOS()) return 0;
        var application = Send(Class("NSApplication"), Sel("sharedApplication"));
        SendBool(application, Sel("activateIgnoringOtherApps:"), true);
        await Wait(() => SendLong(application, Sel("isActive")) != 0);
        var tests = new TestRunner();
        tests.Test("AppKit: top-left UI points have downward Y and independent screen basis", async () =>
        {
            var content = new Grid { Width = 500, Height = 300 };
            var window = new Window { Content = content, Title = "UnoDock AppKit coordinate basis" };
            window.AppWindow.Move(new() { X = 80, Y = 90 }); window.AppWindow.Resize(new() { Width = 600, Height = 450 }); window.Activate();
            using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
            using var coordinates = new DesktopWindowCoordinates();
            try
            {
                await Wait(() => content.IsLoaded && content.ActualHeight > 0);
                var top = coordinates.ToScreen(content, new(30, 30));
                var below = coordinates.ToScreen(content, new(30, 80));
                var right = coordinates.ToScreen(content, new(100, 30));
                Check.Near(-50, below.Y - top.Y, .01);
                Check.Near(70, right.X - top.X, .01);
                Check.Near(0, below.X - top.X, .01); Check.Near(0, right.Y - top.Y, .01);
                var returned = coordinates.FromScreen(new(top.X + 17.5, top.Y - 29.25), content);
                Check.Near(47.5, returned.X, .01); Check.Near(59.25, returned.Y, .01);
            }
            finally { window.Content = null; window.Close(); }
        });
        tests.Test("AppKit: drag clock fires within actual native event tracking mode", () =>
        {
            var ticks = 0; Exception? error = null;
            using var clock = Clock(() => ticks++, e => error = e);
            Track(.12);
            Check.True(ticks > 0, "The drag clock paused in the AppKit tracking loop.");
            Check.True(error == null); return Task.CompletedTask;
        });
        tests.Test("AppKit: disposed tracking clock cannot deliver a stale callback", () =>
        {
            var ticks = 0;
            var clock = Clock(() => ticks++, _ => { });
            Track(.08); Check.True(ticks > 0); clock.Dispose();
            var previous = ticks; Track(.08); Check.Equal(previous, ticks); clock.Dispose();
            return Task.CompletedTask;
        });
        tests.Test("AppKit: callback failure stops clock and never crosses native ABI", () =>
        {
            var ticks = 0; var failures = 0; var failure = new InvalidOperationException("AppKit clock acceptance failure");
            Exception? observed = null;
            using var clock = Clock(() => { ticks++; throw failure; }, e => { failures++; observed = e; });
            Track(.12);
            Check.Equal(1, ticks); Check.Equal(1, failures); Check.Same(failure, observed);
            return Task.CompletedTask;
        });
        return await tests.Run(output, "mac-native");
    }
    private static IDisposable Clock(Action tick, Action<Exception> failed)
    {
        var type = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.NativeDragClock", true)!;
        return (IDisposable)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, [tick, failed], null)!;
    }
    private static void Track(double duration)
    {
        var library = NativeLibrary.Load(AppKit);
        try
        {
            var mode = Marshal.ReadIntPtr(NativeLibrary.GetExport(library, "NSEventTrackingRunLoopMode"));
            Check.True(mode != 0); RunMode(mode, duration, false);
        }
        finally { NativeLibrary.Free(library); }
    }
    private static async Task Wait(Func<bool> predicate)
    { for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(20); Check.True(predicate(), "AppKit host was not ready."); }
    [DllImport(ObjC, EntryPoint = "objc_getClass")] private static extern nint Class(string name);
    [DllImport(ObjC, EntryPoint = "sel_registerName")] private static extern nint Sel(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern long SendLong(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendBool(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);
    [DllImport(CF, EntryPoint = "CFRunLoopRunInMode")] private static extern int RunMode(nint mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);
}
