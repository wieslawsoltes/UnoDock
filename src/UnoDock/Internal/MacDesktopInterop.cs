#if !WINDOWS
using System.Reflection;
using System.Runtime.InteropServices;

namespace UnoDock.Internal;

/// <summary>AppKit conversions use the actual content view, not a guessed title-bar
/// offset. Global AppKit coordinates are logical screen points (bottom-left axes),
/// independent of the backing scale of any particular display.</summary>
internal static class MacDesktopInterop
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string Quartz = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public double X, Y; internal NativePoint(double x, double y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct NativeSize { public double Width, Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct NativeRect { public NativePoint Origin; public NativeSize Size; }

    internal static nint Handle(Window window)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var native = Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) ?? throw new InvalidOperationException("The AppKit host is unavailable.");
        var type = native.GetType();
        // Uno 6.7's desktop host exposes an opaque MacOSWindowNative from the public
        // helper. Its Handle property is the audited NSWindow boundary. Do not
        // interpret AppWindow.Id as an NSWindow pointer or search by window title.
        if (type.FullName is not ("Uno.UI.Runtime.Skia.MacOS.MacOSWindowNative" or "AppKit.NSWindow"))
            throw new PlatformNotSupportedException("This AppKit host needs an explicit cross-window coordinate adapter.");
        var value = type.GetProperty("Handle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(native);
        if (value is not nint handle || handle == 0) throw new InvalidOperationException("The AppKit window has closed or its native handle is unavailable.");
        return handle;
    }
    private static nint ContentView(nint window)
    {
        var view = Send(window, Sel("contentView"));
        return view != 0 ? view : throw new InvalidOperationException("The native content view is unavailable.");
    }
    internal static Point ToScreen(Window window, Point point)
    {
        var handle = Handle(window); var view = ContentView(handle);
        var local = SendPointView(view, Sel("convertPoint:toView:"), new(point.X, point.Y), 0);
        var screen = SendPoint(handle, Sel("convertPointToScreen:"), local);
        return new(screen.X, screen.Y);
    }
    internal static Point FromScreen(Window window, Point point)
    {
        var handle = Handle(window); var view = ContentView(handle);
        var windowPoint = SendPoint(handle, Sel("convertPointFromScreen:"), new(point.X, point.Y));
        var local = SendPointView(view, Sel("convertPoint:fromView:"), windowPoint, 0);
        return new(local.X, local.Y);
    }
    internal static Point Origin(Window window)
    {
        var frame = Rect(Handle(window), "frame"); return new(frame.Origin.X, frame.Origin.Y);
    }
    internal static void Move(Window window, Point origin) => SendVoidPoint(Handle(window), Sel("setFrameOrigin:"), new(origin.X, origin.Y));
    internal static bool IsCaption(Window window, Point point)
    {
        var handle = Handle(window); var frame = Rect(handle, "frame"); var view = ContentView(handle);
        var bounds = Rect(view, "bounds");
        var a = ToScreen(window, new(bounds.Origin.X, bounds.Origin.Y));
        var b = ToScreen(window, new(bounds.Origin.X + bounds.Size.Width, bounds.Origin.Y + bounds.Size.Height));
        return point.X >= frame.Origin.X && point.X < frame.Origin.X + frame.Size.Width &&
            point.Y >= Math.Max(a.Y, b.Y) && point.Y < frame.Origin.Y + frame.Size.Height;
    }
    internal static DesktopPointerState Pointer()
    {
        var type = Class("NSEvent"); var p = SendPointResult(type, Sel("mouseLocation"));
        return new(new(p.X, p.Y), (SendLong(type, Sel("pressedMouseButtons")) & 1) != 0,
            (SendLong(type, Sel("modifierFlags")) & (1L << 18)) != 0, KeyState(0, 53));
    }
    internal static XamlRoot? HitTest(Point point, Window? excluded)
    {
        var excludedNumber = excluded == null ? 0 : SendLong(Handle(excluded), Sel("windowNumber"));
        var type = Class("NSWindow");
        var number = WindowAtPoint(type, Sel("windowNumberAtPoint:belowWindowWithWindowNumber:"), new(point.X, point.Y), 0);
        if (excludedNumber != 0 && number == excludedNumber)
            number = WindowAtPoint(type, Sel("windowNumberAtPoint:belowWindowWithWindowNumber:"), new(point.X, point.Y), (nint)excludedNumber);
        if (number == 0) return null;
        foreach (var candidate in Uno.UI.ApplicationHelper.Windows.ToArray())
        {
            if (ReferenceEquals(candidate, excluded) || candidate.Content?.XamlRoot is not { } root) continue;
            if (SendLong(Handle(candidate), Sel("windowNumber")) != number) continue;
            var local = FromScreen(candidate, point);
            var bounds = Rect(ContentView(Handle(candidate)), "bounds");
            return new Rect(bounds.Origin.X, bounds.Origin.Y, bounds.Size.Width, bounds.Size.Height).Contains(local) ? root : null;
        }
        return null; // A foreign native window is an occluder, not a docking target.
    }
    internal static IDisposable SetOwner(Window window, Window owner, bool tool)
    {
        var child = Handle(window); var parent = Handle(owner);
        if (child == parent) throw new ArgumentException("A floating window cannot own itself.");
        Retain(parent); Retain(child);
        SendChild(parent, Sel("addChildWindow:ordered:"), child, 1);
        SendBool(child, Sel("setHidesOnDeactivate:"), tool);
        SendBool(child, Sel("setExcludedFromWindowsMenu:"), tool);
        return new OwnerLease(parent, child);
    }
    private sealed class OwnerLease(nint parent, nint child) : IDisposable
    {
        private nint _parent = parent;
        public void Dispose()
        {
            var owner = _parent; _parent = 0;
            if (owner == 0) return;
            try { if (Send(child, Sel("parentWindow")) == owner) SendVoidObject(owner, Sel("removeChildWindow:"), child); }
            finally { Release(child); Release(owner); }
        }
    }
    private static NativeRect Rect(nint receiver, string selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        { SendRectStret(out var rect, receiver, Sel(selector)); return rect; }
        return SendRect(receiver, Sel(selector));
    }
    [DllImport(ObjC, EntryPoint = "objc_retain")] private static extern nint Retain(nint value);
    [DllImport(ObjC, EntryPoint = "objc_release")] private static extern void Release(nint value);
    [DllImport(ObjC, EntryPoint = "objc_getClass")] private static extern nint Class(string name);
    [DllImport(ObjC, EntryPoint = "sel_registerName")] private static extern nint Sel(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern long SendLong(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern NativePoint SendPointResult(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern NativePoint SendPoint(nint receiver, nint selector, NativePoint point);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern NativePoint SendPointView(nint receiver, nint selector, NativePoint point, nint view);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendVoidPoint(nint receiver, nint selector, NativePoint point);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendVoidObject(nint receiver, nint selector, nint value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendBool(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern NativeRect SendRect(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")] private static extern void SendRectStret(out NativeRect result, nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint WindowAtPoint(nint receiver, nint selector, NativePoint point, nint below);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendChild(nint receiver, nint selector, nint child, nint order);
    [DllImport(Quartz, EntryPoint = "CGEventSourceKeyState")]
    [return: MarshalAs(UnmanagedType.I1)] private static extern bool KeyState(int state, ushort key);
}
#endif
