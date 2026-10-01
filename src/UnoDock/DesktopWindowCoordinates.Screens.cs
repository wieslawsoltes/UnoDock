using System.Runtime.InteropServices;
using UnoDock.Core;

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
        // On Windows the target monitor's own scale sizes and places the window.
        var physical = DipToPhysical(new DockRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), scale);
        window.AppWindow.Resize(new()
        {
            Width = (int)Math.Round(physical.Width) + frameWidth,
            Height = (int)Math.Round(physical.Height) + frameHeight
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
            X = (int)Math.Round(physical.X),
            Y = (int)Math.Round(physical.Y)
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
        return PhysicalToDip(screen, Scale(source));
    }

    /// <summary>Inverse of <see cref = "ToDesktopPoint"/>: maps a top-left desktop
        /// DIP point into the coordinate space of <paramref name = "destination"/>.</summary>
        internal Point FromDesktopPoint(Point point, FrameworkElement destination)
    {
#if !WINDOWS
        if (OperatingSystem.IsMacOS())
            return FromScreen(new(point.X, Internal.MacDesktopInterop.PrimaryScreenHeight() - point.Y), destination);
#endif
        return FromScreen(DipToPhysical(point, Scale(destination)), destination);
    }

    /// <summary>Win32 monitors in physical pixels with their own scales (mixed
        /// DPI); empty on other hosts, whose desktop space has one scale.</summary>
        internal static IReadOnlyList<DesktopMonitor> Monitors()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ScreensWin32.Monitors();
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
        }

        return [];
    }

    /// <summary>Physical desktop point to top-left desktop DIPs: through its
        /// monitor's own scale on Windows, by <paramref name = "scale"/> elsewhere.</summary>
        internal static Point PhysicalToDip(Point physical, double scale)
    {
        var dip = DesktopDipSpace.ToDip(new DockPoint(physical.X, physical.Y), Monitors(), scale);
        return new(dip.X, dip.Y);
    }

    internal static Point DipToPhysical(Point dip, double scale)
    {
        var physical = DesktopDipSpace.ToPhysical(new DockPoint(dip.X, dip.Y), Monitors(), scale);
        return new(physical.X, physical.Y);
    }

    internal static DockRect PhysicalToDip(DockRect physical, double scale) => DesktopDipSpace.ToDip(physical, Monitors(), scale);
    internal static DockRect DipToPhysical(DockRect dip, double scale) => DesktopDipSpace.ToPhysical(dip, Monitors(), scale);
    internal static IReadOnlyList<DockRect> WorkAreas(double scale)
    {
        if (!(scale > 0) || !double.IsFinite(scale))
            scale = 1;
        try
        {
            // Each Windows monitor converts with its own scale (mixed DPI).
            if (OperatingSystem.IsWindows())
                return [.. ScreensWin32.Monitors().Select(DesktopDipSpace.WorkArea)];
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
        internal static List<DesktopMonitor> Monitors()
        {
            var result = new List<DesktopMonitor>();
            MonitorEnum callback = (monitor, _, _, _) =>
            {
                var info = new MonitorInfo
                {
                    Size = Marshal.SizeOf<MonitorInfo>()
                };
                if (GetMonitorInfoW(monitor, ref info))
                {
                    var scale = 1d;
                    try
                    {
                        // Effective DPI: the per-monitor scale the shell applies.
                        if (GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
                            scale = dpiX / 96d;
                    }
                    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
                    {
                    }

                    result.Add(new(Rect(info.Monitor), Rect(info.Work), scale));
                }

                return true;
            };
            EnumDisplayMonitors(0, 0, callback, 0);
            GC.KeepAlive(callback);
            return result;
        }

        private static DockRect Rect(Bounds bounds) => new(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        [DllImport("shcore.dll", ExactSpelling = true)]
        private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
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
        /// <summary>One work area per RandR monitor, clipped by the window manager's
                /// _NET_WORKAREA for the current desktop (panels and docks). Without RandR
                /// monitors the work area (or the root window) is a single area.</summary>
                internal static List<DockRect> WorkAreas()
        {
            var display = XOpenDisplay(0);
            if (display == 0)
                return [];
            try
            {
                var screen = XDefaultScreen(display);
                var root = XRootWindow(display, screen);
                var work = WorkArea(display, root) ?? new DockRect(0, 0, XDisplayWidth(display, screen), XDisplayHeight(display, screen));
                var areas = new List<DockRect>();
                foreach (var monitor in Monitors(display, root))
                {
                    var left = Math.Max(monitor.X, work.X);
                    var top = Math.Max(monitor.Y, work.Y);
                    var right = Math.Min(monitor.X + monitor.Width, work.X + work.Width);
                    var bottom = Math.Min(monitor.Y + monitor.Height, work.Y + work.Height);
                    areas.Add(right > left && bottom > top ? new(left, top, right - left, bottom - top) : monitor);
                }

                return areas.Count > 0 ? areas : [work];
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        private static DockRect? WorkArea(nint display, nuint root)
        {
            var desktop = 0L;
            var current = XInternAtom(display, "_NET_CURRENT_DESKTOP", true);
            if (current != 0 && XGetWindowProperty(display, root, current, 0, 1, false, 6, out _, out var desktopFormat, out var desktopCount, out _, out var desktopData) == 0 && desktopData != 0)
            {
                if (desktopFormat == 32 && desktopCount >= 1)
                    desktop = Math.Max(0, Marshal.ReadInt64(desktopData, 0));
                XFree(desktopData);
            }

            var atom = XInternAtom(display, "_NET_WORKAREA", true);
            if (atom == 0 || XGetWindowProperty(display, root, atom, (nint)(desktop * 4), 4, false, 6, out _, out var format, out var count, out _, out var data) != 0 || data == 0)
                return null;
            try
            {
                if (format != 32 || count < 4)
                    return null;
                var x = Marshal.ReadInt64(data, 0);
                var y = Marshal.ReadInt64(data, 8);
                var w = Marshal.ReadInt64(data, 16);
                var h = Marshal.ReadInt64(data, 24);
                return w > 0 && h > 0 ? new DockRect(x, y, w, h) : null;
            }
            finally
            {
                XFree(data);
            }
        }

        private static List<DockRect> Monitors(nint display, nuint root)
        {
            var monitors = new List<DockRect>();
            try
            {
                var list = XRRGetMonitors(display, root, 1, out var count);
                if (list == 0)
                    return monitors;
                try
                {
                    // XRRMonitorInfo: Atom name; Bool primary, automatic; int noutput,
                    // x, y, width, height, mwidth, mheight; RROutput* outputs.
                    var size = IntPtr.Size == 8 ? 56 : 44;
                    var offset = IntPtr.Size;
                    for (var i = 0; i < count; i++)
                    {
                        var item = list + i * size + offset;
                        var x = Marshal.ReadInt32(item, 12);
                        var y = Marshal.ReadInt32(item, 16);
                        var width = Marshal.ReadInt32(item, 20);
                        var height = Marshal.ReadInt32(item, 24);
                        if (width > 0 && height > 0)
                            monitors.Add(new(x, y, width, height));
                    }
                }
                finally
                {
                    XRRFreeMonitors(list);
                }
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
            {
            }

            return monitors;
        }

        [DllImport("libXrandr.so.2")]
        private static extern nint XRRGetMonitors(nint display, nuint window, int active, out int count);
        [DllImport("libXrandr.so.2")]
        private static extern void XRRFreeMonitors(nint monitors);
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
