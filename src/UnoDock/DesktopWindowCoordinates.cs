using System.Runtime.InteropServices;

namespace UnoDock;

/// <summary>Optional physical-screen coordinate contract for desktop hosts. Screen points
/// use the host's global coordinate space: physical pixels on Windows/X11 and
/// AppKit screen points on macOS. Visual points are device-independent local units.</summary>
public interface IScreenWindowCoordinates : ICrossWindowCoordinates
{
    Point ToScreen(FrameworkElement source, Point point);
    Point FromScreen(Point screenPoint, FrameworkElement destination);
}

/// <summary>Client-area coordinate conversion for native WinUI, Uno Skia Win32, X11 and AppKit.
/// No window-frame, caption-height or DPI offsets are guessed. X11 queries use a private,
/// lazily opened XCB connection with checked replies, without modifying Uno's Xlib error
/// handler or consuming its event queue. Use and dispose on the creating UI thread.</summary>
public sealed partial class DesktopWindowCoordinates : IScreenWindowCoordinates, IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private bool _disposed;
#if !WINDOWS
    private XcbConnection? _xcb;
#endif
    public Point Translate(FrameworkElement source, Point sourcePoint, FrameworkElement destination)
    {
        if (!double.IsFinite(sourcePoint.X) || !double.IsFinite(sourcePoint.Y)) throw new ArgumentOutOfRangeException(nameof(sourcePoint));
        return CreateTranslator(source, destination)(sourcePoint);
    }
    internal Func<Point, Point> CreateTranslator(FrameworkElement source, FrameworkElement destination)
    {
        Verify(); Validate(source, default); Validate(destination, default);
        if (ReferenceEquals(source.XamlRoot, destination.XamlRoot))
        { var transform = source.TransformToVisual(destination); return transform.TransformPoint; }
#if WINDOWS
        return point => FromScreen(ToScreen(source, point), destination);
#else
        if (OperatingSystem.IsMacOS()) return point => FromScreen(ToScreen(source, point), destination);
        var from = GetNative(source); var to = GetNative(destination);
        Point offset;
        if (OperatingSystem.IsLinux() && from is Uno.UI.NativeElementHosting.X11NativeWindow x && to is Uno.UI.NativeElementHosting.X11NativeWindow y)
            offset = TranslateX11(Id(x.WindowId), Id(y.WindowId));
        else if (OperatingSystem.IsWindows() && from is Uno.UI.NativeElementHosting.Win32NativeWindow w && to is Uno.UI.NativeElementHosting.Win32NativeWindow v)
        {
            var a = Win32Origin(w.Hwnd); var b = Win32Origin(v.Hwnd);
            offset = new(a.X - b.X, a.Y - b.Y);
        }
        else throw Unsupported();
        // TransformToVisual(root.Content) cancels the root's own RTL/scale transform.
        // Native origins refer to physical client axes, so include that last transform.
        var sourceTransform = source.TransformToVisual(null);
        var destinationTransform = (destination.TransformToVisual(null).Inverse ?? throw new InvalidOperationException("Destination transform is not invertible."));
        var sourceScale = Scale(source); var destinationScale = Scale(destination);
        // Snapshot one live native origin per bounds query, not four synchronous RPCs.
        // Never retain across frames, moves or DPI changes.
        return point =>
        {
            var local = sourceTransform.TransformPoint(point);
            var mapped = DockInteractionGeometry.TranslateClientPoint(new(local.X, local.Y), new(offset.X, offset.Y), sourceScale, destinationScale);
            return destinationTransform.TransformPoint(new(mapped.X, mapped.Y));
        };
#endif
    }
    public Point ToScreen(FrameworkElement source, Point point)
    {
        Verify(); Validate(source, point);
        var local = source.TransformToVisual(null).TransformPoint(point);
#if WINDOWS
        var screen = Microsoft.UI.Content.ContentCoordinateConverter.CreateForWindowId(source.XamlRoot!.ContentIslandEnvironment.AppWindowId).ConvertLocalToScreen(local);
        return new Point(screen.X, screen.Y);
#else
        if (OperatingSystem.IsMacOS()) return Internal.MacDesktopInterop.ToScreen(WindowFor(source) ?? throw Unsupported(), local);
        var native = GetNative(source);
        var origin = native switch
        {
            Uno.UI.NativeElementHosting.X11NativeWindow window when OperatingSystem.IsLinux() => TranslateX11(Id(window.WindowId), RootX11(Id(window.WindowId))),
            Uno.UI.NativeElementHosting.Win32NativeWindow window when OperatingSystem.IsWindows() => Win32Origin(window.Hwnd),
            _ => throw Unsupported()
        };
        return new(origin.X + local.X * Scale(source), origin.Y + local.Y * Scale(source));
#endif
    }
    public Point FromScreen(Point screenPoint, FrameworkElement destination)
    {
        Verify(); Validate(destination, screenPoint);
        Point point;
#if WINDOWS
        // WinAppSDK uses integer physical screen coordinates. Round only at that
        // API boundary; same-root and Uno Skia calculations retain fractional DIPs.
        point = Microsoft.UI.Content.ContentCoordinateConverter.CreateForWindowId(destination.XamlRoot!.ContentIslandEnvironment.AppWindowId)
            .ConvertScreenToLocal(new Windows.Graphics.PointInt32 { X = checked((int)Math.Round(screenPoint.X)), Y = checked((int)Math.Round(screenPoint.Y)) });
#else
        if (OperatingSystem.IsMacOS())
        {
            point = Internal.MacDesktopInterop.FromScreen(WindowFor(destination) ?? throw Unsupported(), screenPoint);
            return (destination.TransformToVisual(null).Inverse ?? throw new InvalidOperationException("Destination transform is not invertible.")).TransformPoint(point);
        }
        var native = GetNative(destination);
        var origin = native switch
        {
            Uno.UI.NativeElementHosting.X11NativeWindow window when OperatingSystem.IsLinux() => TranslateX11(Id(window.WindowId), RootX11(Id(window.WindowId))),
            Uno.UI.NativeElementHosting.Win32NativeWindow window when OperatingSystem.IsWindows() => Win32Origin(window.Hwnd),
            _ => throw Unsupported()
        };
        point = new((screenPoint.X - origin.X) / Scale(destination), (screenPoint.Y - origin.Y) / Scale(destination));
#endif
        return (destination.TransformToVisual(null).Inverse ?? throw new InvalidOperationException("Destination transform is not invertible.")).TransformPoint(point);
    }
    /// <summary>Returns false only when this provider has no native z-order query.
    /// A successful query with a null root means the point is occluded by another
    /// application or lies outside any registered Uno client window.</summary>
    internal bool TryGetTopmostRoot(FrameworkElement source, Point point, out XamlRoot? hitRoot)
    {
        Verify(); Validate(source, point);
        hitRoot = null;
#if WINDOWS
        return false;
#else
        if (!OperatingSystem.IsLinux() || source.XamlRoot is not { } root) return false;
        var windows = Uno.UI.ApplicationHelper.Windows;
        var window = windows.FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, root));
        if (window == null || Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native)
            return false;
        var client = source.TransformToVisual(null).TransformPoint(point);
        var x = Math.Round(client.X * root.RasterizationScale);
        var y = Math.Round(client.Y * root.RasterizationScale);
        // Core X11 coordinate requests use signed 16-bit coordinates. Reject, do
        // not wrap an out-of-range position into a different pane.
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue)
            throw new InvalidOperationException("The point exceeds the X11 coordinate range.");
        try
        {
            var connection = Connection;
            if (connection.IsInvalid || Xcb.Error(connection) != 0)
                throw new InvalidOperationException("The X11 connection is unavailable.");
            var geometry = Xcb.GeometryReply(connection, Xcb.Geometry(connection, Id(native.WindowId)), out var error);
            uint current;
            try
            {
                if (error != 0 || geometry == 0) throw new InvalidOperationException("The source window has closed.");
                current = unchecked((uint)Marshal.ReadInt32(geometry, 8)); // screen root
            }
            finally { if (geometry != 0) Xcb.Free(geometry); if (error != 0) Xcb.Free(error); }
            // Descend the server's actual stacking order at the queried point.
            // This works with window-manager reparenting and never guesses a
            // frame-to-client offset or relies on focus order for native windows.
            for (var depth = 0; depth < 32; depth++)
            {
                var reply = Xcb.TranslateReply(connection, Xcb.Translate(connection, Id(native.WindowId), current, (short)x, (short)y), out error);
                uint child;
                try
                {
                    if (error != 0 || reply == 0 || Marshal.ReadByte(reply, 1) == 0)
                        throw new InvalidOperationException("The X11 target hierarchy changed during hit testing.");
                    child = unchecked((uint)Marshal.ReadInt32(reply, 8));
                }
                finally { if (reply != 0) Xcb.Free(reply); if (error != 0) Xcb.Free(error); }
                if (child == 0 || child == current) return true;
                foreach (var candidate in windows)
                    if (candidate.Content?.XamlRoot is { } candidateRoot &&
                        Uno.UI.Xaml.WindowHelper.GetNativeWindow(candidate) is Uno.UI.NativeElementHosting.X11NativeWindow candidateNative &&
                        Id(candidateNative.WindowId) == child)
                    { hitRoot = candidateRoot; return true; }
                current = child;
            }
            // An unexpectedly deep foreign hierarchy is not a docking target.
            return true;
        }
        catch (DllNotFoundException e) { throw new InvalidOperationException("X11 hit testing requires libxcb.", e); }
        catch (EntryPointNotFoundException e) { throw new InvalidOperationException("The XCB coordinate API is unavailable.", e); }
#endif
    }

    internal static void HideNativeClientBeforeClose(Window window)
    {
#if !WINDOWS
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            if (Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native) return;
            using var adapter = new DesktopWindowCoordinates();
            var connection = adapter.Connection;
            // Uno's renderer teardown is asynchronous: unmap our own client now,
            // but leave all renderer-owned resource destruction to the framework.
            var error = Xcb.RequestCheck(connection, Xcb.Unmap(connection, Id(native.WindowId)));
            Xcb.Free(error); // An already-destroyed client requires no action.
        }
        catch (PlatformNotSupportedException) { }
        catch (InvalidOperationException) { }
#endif
    }
    internal static bool TryTranslateX11Origins(nint source, nint destination, out Point offset)
    {
        offset = default;
#if !WINDOWS
        if (!OperatingSystem.IsLinux()) return false;
        try
        {
            using var adapter = new DesktopWindowCoordinates();
            offset = adapter.TranslateX11(Id(source), Id(destination));
            return true;
        }
        catch (PlatformNotSupportedException) { }
        catch (InvalidOperationException) { }
#endif
        return false;
    }
    internal static double Scale(FrameworkElement element)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1;
        return double.IsFinite(scale) && scale > 0 ? scale : 1;
    }
    private static void Validate(FrameworkElement element, Point point)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        if (!element.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Coordinate conversion requires the UI thread.");
        if (element.XamlRoot?.Content == null || !element.IsLoaded) throw new InvalidOperationException("The coordinate visual must be loaded into a live XamlRoot.");
    }
    private void Verify()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Use desktop coordinates on the creating UI thread.");
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
    public void Dispose()
    {
        if (_disposed) return;
        Verify(); _disposed = true;
#if !WINDOWS
        _xcb?.Dispose(); _xcb = null;
#endif
    }
#if !WINDOWS
    private static object GetNative(FrameworkElement element)
    {
        var window = Uno.UI.ApplicationHelper.Windows.FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, element.XamlRoot));
        if (window == null) throw new InvalidOperationException("The visual no longer belongs to a live desktop window.");
        return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) ?? throw Unsupported();
    }
    private static PlatformNotSupportedException Unsupported() => new("This native host has no built-in client coordinate adapter. Supply ICrossWindowCoordinates for embedded islands or other custom hosts.");
    private static uint Id(IntPtr handle)
    {
        var value = unchecked((ulong)handle.ToInt64());
        if (value == 0 || value > uint.MaxValue) throw new InvalidOperationException("Invalid X11 window identifier.");
        return (uint)value;
    }
    private XcbConnection Connection
    {
        get
        {
            if (_xcb is { IsClosed: false, IsInvalid: false } && Xcb.Error(_xcb) == 0) return _xcb;
            _xcb?.Dispose(); _xcb = null;
            try
            {
                var connection = new XcbConnection(Xcb.Connect(IntPtr.Zero, out _));
                if (connection.IsInvalid || Xcb.Error(connection) != 0)
                { connection.Dispose(); throw new InvalidOperationException("Unable to open a coordinate-query connection to the X11 display."); }
                return _xcb = connection;
            }
            catch (DllNotFoundException e) { throw new PlatformNotSupportedException("X11 coordinate conversion requires libxcb.so.1.", e); }
            catch (EntryPointNotFoundException e) { throw new PlatformNotSupportedException("X11 coordinate query functions are unavailable.", e); }
        }
    }
    private uint RootX11(uint window)
    {
        var connection = Connection;
        var reply = Xcb.GeometryReply(connection, Xcb.Geometry(connection, window), out var error);
        try
        {
            CheckReply(reply, error);
            // xcb_get_geometry_reply_t: response/depth/sequence/length, then root.
            return unchecked((uint)Marshal.ReadInt32(reply, 8));
        }
        finally { Xcb.Free(reply); Xcb.Free(error); }
    }
    private Point TranslateX11(uint source, uint target)
    {
        var connection = Connection;
        var reply = Xcb.TranslateReply(connection, Xcb.Translate(connection, source, target, 0, 0), out var error);
        try
        {
            CheckReply(reply, error);
            if (Marshal.ReadByte(reply, 1) == 0) throw new InvalidOperationException("Windows are on different X11 screens.");
            // xcb_translate_coordinates_reply_t: response/same_screen/sequence,
            // length, child, then signed 16-bit dst_x and dst_y. Only the origin
            // enters the protocol; arbitrary fractional local points stay doubles.
            return new(Marshal.ReadInt16(reply, 12), Marshal.ReadInt16(reply, 14));
        }
        finally { Xcb.Free(reply); Xcb.Free(error); }
    }
    private static void CheckReply(IntPtr reply, IntPtr error)
    {
        if (error != IntPtr.Zero) throw new InvalidOperationException("X11 coordinate query rejected (error " + Marshal.ReadByte(error, 1) + "). The window may have closed.");
        if (reply == IntPtr.Zero) throw new InvalidOperationException("The X11 display disconnected during a coordinate query.");
    }
    private static Point Win32Origin(IntPtr hwnd)
    {
        var origin = new NativePoint();
        if (hwnd == IntPtr.Zero || !ClientToScreen(hwnd, ref origin))
            throw new InvalidOperationException("The native client area is unavailable.", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        return new(origin.X, origin.Y);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    private sealed class XcbConnection : SafeHandle
    {
        internal XcbConnection(IntPtr value) : base(IntPtr.Zero, true) => SetHandle(value);
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle() { Xcb.Disconnect(handle); return true; }
    }
    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_connect", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Connect(IntPtr display, out int screen);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_disconnect", CallingConvention = CallingConvention.Cdecl)] internal static extern void Disconnect(IntPtr connection);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_connection_has_error", CallingConvention = CallingConvention.Cdecl)] internal static extern int Error(XcbConnection connection);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_translate_coordinates", CallingConvention = CallingConvention.Cdecl)] internal static extern uint Translate(XcbConnection connection, uint source, uint target, short x, short y);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_translate_coordinates_reply", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr TranslateReply(XcbConnection connection, uint cookie, out IntPtr error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_geometry", CallingConvention = CallingConvention.Cdecl)] internal static extern uint Geometry(XcbConnection connection, uint drawable);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_geometry_reply", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr GeometryReply(XcbConnection connection, uint cookie, out IntPtr error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_unmap_window_checked", CallingConvention = CallingConvention.Cdecl)] internal static extern uint Unmap(XcbConnection connection, uint window);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_request_check", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr RequestCheck(XcbConnection connection, uint cookie);
        [DllImport("libc", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)] internal static extern void Free(IntPtr memory);
    }
#endif
}
