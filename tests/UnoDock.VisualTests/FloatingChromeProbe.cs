using System.Reflection;
using System.Runtime.InteropServices;
using UnoDock.Core;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Independent native geometry and decoration oracle. No production
/// chrome/coordinate adapter is used. Xlib property values use native-long slots,
/// unlike the packed uint32 XCB protocol used by the implementation.</summary>
internal static class FloatingChromeProbe
{
    internal static DockRect Bounds(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            Check.True(GetWindowRect(WinHandle(window), out var r));
            return new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }

        if (OperatingSystem.IsMacOS())
        {
            var r = MacRect(MacHandle(window), "frame");
            return new(r.X, -r.Y - r.Height, r.Width, r.Height);
        }

        using var x = new X11();
        var id = X11Handle(window);
        Check.True(Translate(x.Display, id, x.Root, 0, 0, out var sx, out var sy, out _) != 0);
        Check.True(GetGeometry(x.Display, id, out _, out _, out _, out var width, out var height, out _, out _) != 0);
        var e = x.Property(id, "_NET_FRAME_EXTENTS");
        var left = e.Length >= 4 ? e[0] : 0;
        var right = e.Length >= 4 ? e[1] : 0;
        var top = e.Length >= 4 ? e[2] : 0;
        var bottom = e.Length >= 4 ? e[3] : 0;
        return new(sx - (double)left, sy - (double)top, width + (double)left + right, height + (double)top + bottom);
    }

    internal static void AssertCustom(Window window, Window owner)
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = WinHandle(window);
            Check.True(GetWindowRect(handle, out var frame));
            var origin = new POINT();
            Check.True(ClientToScreen(handle, ref origin));
            Check.True(GetClientRect(handle, out var client));
            Check.Equal(frame.Top, origin.Y);
            Check.Equal(frame.Left, origin.X);
            Check.Equal(frame.Right - frame.Left, client.Right);
            Check.Equal(frame.Bottom - frame.Top, client.Bottom);
            Check.True((GetWindowLong(handle, -16) & 0x00c00000) == 0, "WS_CAPTION is still present.");
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var handle = MacHandle(window);
            var style = Integer(handle, Sel("styleMask"));
            Check.True((style & (1L << 15)) != 0, "Full-size AppKit content is absent.");
            Check.Equal(1L, Integer(handle, Sel("titleVisibility")));
            Check.True(Integer(handle, Sel("titlebarAppearsTransparent")) != 0);
            Check.True(Integer(handle, Sel("canBecomeKeyWindow")) != 0, "Custom chrome must not discard keyboard capability.");
            for (var i = 0; i < 3; i++)
            {
                var button = ObjectInteger(handle, Sel("standardWindowButton:"), i);
                if (button != 0)
                    Check.True(Integer(button, Sel("isHidden")) != 0, "A native traffic-light button remains visible.");
            }

            var content = Object(handle, Sel("contentView"));
            var native = MacRect(handle, "frame");
            var client = MacRect(content, "frame");
            Check.True(Math.Abs(native.Height - client.Height) <= 1, $"AppKit still reserves {native.Height - client.Height} points for OS chrome.");
            return;
        }

        using var x = new X11();
        var id = X11Handle(window);
        var motif = x.Property(id, "_MOTIF_WM_HINTS");
        Check.True(motif.Length >= 5 && (motif[0] & 2) != 0 && motif[2] == 0, "The native X11 decoration request was not applied.");
        if (Environment.GetEnvironmentVariable("UNODOCK_REQUIRE_WM") == "1")
        {
            Check.True(x.Property(x.Root, "_NET_SUPPORTING_WM_CHECK").Length > 0, "This acceptance run requires a real window manager.");
            var ownerFrame = x.Property(X11Handle(owner), "_NET_FRAME_EXTENTS");
            Check.True(ownerFrame.Length == 4 && ownerFrame[2] > 0, "The oracle must observe an OS title bar on the unmodified owner.");
            var extents = x.Property(id, "_NET_FRAME_EXTENTS");
            Check.True(extents.Length == 4 && extents.All(v => v == 0), "The window manager retained floating OS decorations.");
        }
    }

    internal static void AssertSystem(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            Check.True((GetWindowLong(WinHandle(window), -16) & 0x00c00000) != 0);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var handle = MacHandle(window);
            Check.True((Integer(handle, Sel("styleMask")) & (1L << 15)) == 0);
            Check.Equal(0L, Integer(handle, Sel("titleVisibility")));
            return;
        }

        using var x = new X11();
        var motif = x.Property(X11Handle(window), "_MOTIF_WM_HINTS");
        Check.True(motif.Length == 0 || (motif[0] & 2) == 0 || motif[2] != 0);
        if (Environment.GetEnvironmentVariable("UNODOCK_REQUIRE_WM") == "1")
        {
            var extents = x.Property(X11Handle(window), "_NET_FRAME_EXTENTS");
            Check.True(extents.Length == 4 && extents[2] > 0);
        }
    }

    private static nint WinHandle(Window window) => Uno.UI.Xaml.WindowHelper.GetNativeWindow(window)is Uno.UI.NativeElementHosting.Win32NativeWindow native ? native.Hwnd : throw new InvalidOperationException("Expected a Win32 host.");
    private static nint X11Handle(Window window) => Uno.UI.Xaml.WindowHelper.GetNativeWindow(window)is Uno.UI.NativeElementHosting.X11NativeWindow native ? native.WindowId : throw new InvalidOperationException("Expected an X11 host.");
    private static nint MacHandle(Window window)
    {
        var native = Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) ?? throw new InvalidOperationException("Expected AppKit host.");
        return (nint)(native.GetType().GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(native) ?? throw new InvalidOperationException("Expected native NSWindow handle."));
    }

    private sealed class X11 : IDisposable
    {
        internal nint Display { get; } = OpenDisplay(0);
        internal nint Root => DefaultRootWindow(Display);

        internal X11()
        {
            Check.True(Display != 0, "Independent Xlib oracle could not connect.");
        }

        internal ulong[] Property(nint window, string name)
        {
            var atom = InternAtom(Display, name, false);
            var status = GetProperty(Display, window, atom, 0, 64, false, 0, out _, out var format, out var count, out var after, out var data);
            try
            {
                Check.Equal(0, status);
                Check.True(after == 0 && count <= 64);
                if (format == 0)
                    return[];
                Check.Equal(32, format);
                var result = new ulong[(int)count];
                for (var i = 0; i < result.Length; i++)
                    result[i] = unchecked((uint)Marshal.ReadIntPtr(data, i * IntPtr.Size).ToInt64());
                return result;
            }
            finally
            {
                if (data != 0)
                    Free(data);
            }
        }

        public void Dispose()
        {
            if (Display != 0)
                CloseDisplay(Display);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        internal int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        internal int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out RECT rect);
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out RECT rect);
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref POINT point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
    private static extern int GetWindowLong(nint window, int index);
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal double X, Y, Width, Height;
    }

    private static NativeRect MacRect(nint receiver, string selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            RectStret(out var rect, receiver, Sel(selector));
            return rect;
        }

        return Rect(receiver, Sel(selector));
    }

    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    private static extern nint Sel(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Object(nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint ObjectInteger(nint value, nint selector, long argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern long Integer(nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NativeRect Rect(nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")]
    private static extern void RectStret(out NativeRect result, nint value, nint selector);
    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
    private static extern nint OpenDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
    private static extern int CloseDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
    private static extern nint DefaultRootWindow(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XInternAtom")]
    private static extern nint InternAtom(nint display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyExisting);
    [DllImport("libX11.so.6", EntryPoint = "XGetWindowProperty")]
    private static extern int GetProperty(nint display, nint window, nint property, nint offset, nint length, [MarshalAs(UnmanagedType.Bool)] bool delete, nint requestedType, out nint type, out int format, out nuint count, out nuint after, out nint data);
    [DllImport("libX11.so.6", EntryPoint = "XFree")]
    private static extern int Free(nint data);
    [DllImport("libX11.so.6", EntryPoint = "XTranslateCoordinates")]
    private static extern int Translate(nint display, nint source, nint destination, int x, int y, out int sx, out int sy, out nint child);
    [DllImport("libX11.so.6", EntryPoint = "XGetGeometry")]
    private static extern int GetGeometry(nint display, nint window, out nint root, out int x, out int y, out uint width, out uint height, out uint border, out uint depth);
}
