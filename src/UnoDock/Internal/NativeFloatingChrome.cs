using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using UnoDock.Controls;

namespace UnoDock.Internal;

/// <summary>Per-window decoration lease. Coordinates are screen units with downward Y:
/// physical pixels on Windows/X11 and negated AppKit screen points on macOS.
/// No platform-wide window class, input hook or native event queue is modified.</summary>
internal abstract class NativeFloatingChrome(Window window) : IDisposable
{
    protected Window Window { get; } = window;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    protected bool IsDisposed { get; private set; }
    internal abstract DockRect ReadBounds();
    internal abstract void WriteBounds(DockRect bounds);
    protected abstract void EnableCore();
    protected abstract void RestoreCore();
    protected virtual void ReleaseCore() { }
    internal static Point NormalizeScreen(Point point) => OperatingSystem.IsMacOS() ? new(point.X, -point.Y) : point;
    internal static double ScreenScale(FrameworkElement view) => OperatingSystem.IsMacOS() ? 1 : DesktopWindowCoordinates.Scale(view);

    internal static NativeFloatingChrome Attach(Window window, LayoutFloatingWindowControl control, Action<Exception> failure)
    {
        if (!window.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Floating chrome requires the owning UI thread.");
        NativeFloatingChrome lease;
        if (OperatingSystem.IsWindows()) lease = new WindowsChrome(window, control, failure);
#if !WINDOWS
        else if (OperatingSystem.IsMacOS()) lease = new MacFloatingChrome(window);
        else if (OperatingSystem.IsLinux()) lease = DesktopWindowCoordinates.CreateX11FloatingChrome(window);
#endif
        else throw new PlatformNotSupportedException("Custom floating chrome requires a Windows, X11 or AppKit window.");
        try { lease.EnableCore(); return lease; }
        catch (Exception error)
        {
            var cleanup = new DockCleanup(); cleanup.Attempt(() => throw error); cleanup.Attempt(lease.Dispose);
            cleanup.ThrowIfFailed(); throw;
        }
    }
    protected void Verify()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Floating chrome belongs to its creating UI thread.");
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
    internal void Release(bool restore)
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Release floating chrome on its creating UI thread.");
        if (IsDisposed) return;
        IsDisposed = true;
        var cleanup = new DockCleanup();
        if (restore) cleanup.Attempt(RestoreCore);
        cleanup.Attempt(ReleaseCore); cleanup.ThrowIfFailed();
    }
    public void Dispose() => Release(true);

    private sealed class WindowsChrome(Window window, LayoutFloatingWindowControl control, Action<Exception> failure) : NativeFloatingChrome(window)
    {
        private OverlappedPresenter? _presenter;
        private NativeWindowMessageHook? _hook;
        private bool _border, _title, _extended, _changed;
        private nint Handle => Microsoft.Windows.Shell.NativeChrome.Handle(Window);
        protected override void EnableCore()
        {
            _presenter = Window.AppWindow.Presenter as OverlappedPresenter ?? throw new NotSupportedException("Floating chrome requires an overlapped presenter.");
            _border = _presenter.HasBorder; _title = _presenter.HasTitleBar;
            _extended = Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar;
            _hook = NativeWindowMessageHook.Attach(Window, Filter, failure) ?? throw new NotSupportedException("The native HWND is unavailable.");
            _changed = true;
            // Extending into a WinUI title bar leaves OS caption buttons and drag
            // hit regions. Completely remove it; XAML owns the entire client frame.
            Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
            _presenter.SetBorderAndTitleBar(false, false);
            Check(SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020));
        }
        internal override DockRect ReadBounds()
        {
            Verify(); Check(GetWindowRect(Handle, out var r));
            return new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }
        internal override void WriteBounds(DockRect bounds)
        {
            Verify();
            var x = checked((int)Math.Round(bounds.X)); var y = checked((int)Math.Round(bounds.Y));
            var right = checked((int)Math.Round(bounds.Right)); var bottom = checked((int)Math.Round(bounds.Bottom));
            Check(SetWindowPos(Handle, 0, x, y, Math.Max(1, right - x), Math.Max(1, bottom - y), 0x0004 | 0x0010));
        }
        private nint Filter(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            if (IsDisposed) return 0;
            if (message == 0x0083) { handled = true; return 0; } // WM_NCCALCSIZE: no residual OS frame strip.
            if (message != 0x0024 || lParam == 0) return 0; // WM_GETMINMAXINFO
            var monitor = MonitorFromWindow(hwnd, 2); var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return 0;
            var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            // A borderless maximize must respect the monitor work area, including
            // taskbars on non-primary monitors and negative desktop coordinates.
            limits.MaxPosition = new(info.Work.Left - info.Monitor.Left, info.Work.Top - info.Monitor.Top);
            limits.MaxSize = new(info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            var scale = Math.Max(1, GetDpiForWindow(hwnd)) / 96d;
            limits.MinTrack = new(checked((int)Math.Ceiling(Math.Max(160, control.MinWidth) * scale)),
                checked((int)Math.Ceiling(Math.Max(100, control.MinHeight) * scale)));
            if (double.IsFinite(control.MaxWidth)) limits.MaxTrack.X = Math.Max(limits.MinTrack.X, checked((int)Math.Floor(control.MaxWidth * scale)));
            if (double.IsFinite(control.MaxHeight)) limits.MaxTrack.Y = Math.Max(limits.MinTrack.Y, checked((int)Math.Floor(control.MaxHeight * scale)));
            Marshal.StructureToPtr(limits, lParam, false); handled = true; return 0;
        }
        protected override void RestoreCore()
        {
            _hook?.Dispose(); _hook = null;
            if (!_changed || _presenter == null || !IsWindow(Handle)) return;
            if (!_presenter.HasBorder && !_presenter.HasTitleBar) _presenter.SetBorderAndTitleBar(_border, _title);
            if (!Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar) Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = _extended;
        }
        protected override void ReleaseCore() { _hook?.Dispose(); _hook = null; }
        private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError()); }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint(int x, int y) { internal int X = x, Y = y; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { internal NativePoint Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { internal int Size; internal NativeRect Monitor, Work; internal uint Flags; }
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rect);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
        [DllImport("user32.dll", ExactSpelling = true)] private static extern nint MonitorFromWindow(nint window, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
        [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetDpiForWindow(nint window);
    }
}
