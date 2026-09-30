using System.Runtime.InteropServices;

namespace UnoDock;

public sealed partial class DesktopWindowCoordinates
{
    /// <summary>Work areas of all monitors (screen minus task bars, docks and menu
        /// bars) in top-left, device-independent coordinates relative to the primary
        /// screen, converted with the rasterization scale of <paramref name = "reference"/>.
        /// Returns an empty list on hosts without a native screen query.</summary>
        public static IReadOnlyList<Rect> GetWorkAreas(FrameworkElement reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return [.. WorkAreas(Scale(reference)).Select(r => new Rect(r.X, r.Y, r.Width, r.Height))];
    }

    /// <summary>Place a desktop window: <paramref name = "bounds"/> X/Y is the
        /// window's outer top-left corner and Width/Height its client (content) size,
        /// all in DIPs, consistently on every platform (AppWindow sizes include the
        /// frame on some hosts and positions are bottom-based on others).</summary>
        public static void SetWindowBounds(Window window, Rect bounds, double scale)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height + scale) || bounds.Width <= 0 || bounds.Height <= 0 || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        var frameWidth = Math.Max(0, window.AppWindow.Size.Width - window.AppWindow.ClientSize.Width);
        var frameHeight = Math.Max(0, window.AppWindow.Size.Height - window.AppWindow.ClientSize.Height);
        window.AppWindow.Resize(new()
        {
            Width = (int)Math.Round(bounds.Width * scale) + frameWidth,
            Height = (int)Math.Round(bounds.Height * scale) + frameHeight
        });
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
        {
            Internal.MacDesktopInterop.MoveTopLeft(window, new(bounds.X, bounds.Y));
            return;
        }

#endif
        window.AppWindow.Move(new()
        {
            X = (int)Math.Round(bounds.X * scale),
            Y = (int)Math.Round(bounds.Y * scale)
        });
    }

    /// <summary>Desktop position of a visual point in top-left DIPs: the space of
        /// <see cref = "GetWorkAreas"/> and of LayoutContent floating bounds.</summary>
        public Point ToDesktopPoint(FrameworkElement source, Point point)
    {
        var screen = ToScreen(source, point);
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
            return Internal.MacDesktopInterop.ToTopLeft(screen);
#endif
        var scale = Scale(source);
        return new(screen.X / scale, screen.Y / scale);
    }

    /// <summary>Inverse of <see cref = "ToDesktopPoint"/>: maps a top-left desktop
        /// DIP point into the coordinate space of <paramref name = "destination"/>.</summary>
        internal Point FromDesktopPoint(Point point, FrameworkElement destination)
    {
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
            return FromScreen(new(point.X, Internal.MacDesktopInterop.PrimaryScreenHeight() - point.Y), destination);
#endif
        var scale = Scale(destination);
        return FromScreen(new(point.X * scale, point.Y * scale), destination);
    }

    internal static IReadOnlyList<DockRect> WorkAreas(double scale)
    {
        if (!(scale > 0) || !double.IsFinite(scale))
            scale = 1;
        try
        {
            if (OperatingSystem.IsWindows())
                return [.. ScreensWin32.WorkAreas().Select(r => new DockRect(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale))];
#if !WINDOWS
            if (OperatingSystem.IsMacOS())
                return Internal.MacDesktopInterop.VisibleFrames();
            if (OperatingSystem.IsLinux())
                return [.. ScreensX11.WorkAreas().Select(r => new DockRect(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale))];
#endif
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or PlatformNotSupportedException)
        {
        }

        return [];
    }

    private static class ScreensWin32
    {
        internal static List<DockRect> WorkAreas()
        {
            var result = new List<DockRect>();
            MonitorEnum callback = (monitor, _, _, _) =>
            {
                var info = new MonitorInfo
                {
                    Size = Marshal.SizeOf<MonitorInfo>()
                };
                if (GetMonitorInfoW(monitor, ref info))
                    result.Add(new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top));
                return true;
            };
            EnumDisplayMonitors(0, 0, callback, 0);
            GC.KeepAlive(callback);
            return result;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Bounds
        {
            internal int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            internal int Size;
            internal Bounds Monitor, Work;
            internal uint Flags;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool MonitorEnum(nint monitor, nint context, nint bounds, nint data);
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(nint context, nint clip, MonitorEnum callback, nint data);
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    }
#if !WINDOWS
    private static class ScreensX11
    {
        /// <summary>The window manager's _NET_WORKAREA for the current desktop, or
                /// the root window when no EWMH window manager publishes it.</summary>
                internal static List<DockRect> WorkAreas()
        {
            var display = XOpenDisplay(0);
            if (display == 0)
                return [];
            try
            {
                var screen = XDefaultScreen(display);
                var root = XRootWindow(display, screen);
                var atom = XInternAtom(display, "_NET_WORKAREA", true);
                if (atom != 0 && XGetWindowProperty(display, root, atom, 0, 4, false, 6, out _, out var format, out var count, out _, out var data) == 0 && data != 0)
                {
                    try
                    {
                        if (format == 32 && count >= 4)
                        {
                            var x = Marshal.ReadInt64(data, 0);
                            var y = Marshal.ReadInt64(data, 8);
                            var w = Marshal.ReadInt64(data, 16);
                            var h = Marshal.ReadInt64(data, 24);
                            if (w > 0 && h > 0)
                                return [new(x, y, w, h)];
                        }
                    }
                    finally
                    {
                        XFree(data);
                    }
                }

                return [new(0, 0, XDisplayWidth(display, screen), XDisplayHeight(display, screen))];
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        [DllImport("libX11.so.6")]
        private static extern nint XOpenDisplay(nint name);
        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")]
        private static extern int XDefaultScreen(nint display);
        [DllImport("libX11.so.6")]
        private static extern nuint XRootWindow(nint display, int screen);
        [DllImport("libX11.so.6")]
        private static extern int XDisplayWidth(nint display, int screen);
        [DllImport("libX11.so.6")]
        private static extern int XDisplayHeight(nint display, int screen);
        [DllImport("libX11.so.6")]
        private static extern nuint XInternAtom(nint display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);
        [DllImport("libX11.so.6")]
        private static extern int XGetWindowProperty(nint display, nuint window, nuint property, nint offset, nint length, [MarshalAs(UnmanagedType.Bool)] bool delete, nuint type, out nuint actualType, out int format, out nuint count, out nuint remaining, out nint data);
        [DllImport("libX11.so.6")]
        private static extern int XFree(nint data);
    }
#endif
}
