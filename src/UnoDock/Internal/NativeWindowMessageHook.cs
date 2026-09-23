using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace UnoDock.Internal;

/// <summary>
/// An owning-thread comctl32 subclass subscription. The native callback stays
/// rooted until removal succeeds or WM_NCDESTROY terminates the HWND lifetime.
/// </summary>
internal sealed class NativeWindowMessageHook : IDisposable
{
    internal delegate nint Filter(nint hwnd, int message, nint wParam, nint lParam, ref bool handled);
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    private static readonly ConcurrentDictionary<nuint, NativeWindowMessageHook> Roots = new();
    private static long _sequence;
    private readonly nint _hwnd;
    private readonly nuint _id;
    private readonly uint _thread;
    private readonly SubclassProc _callback;
    private Filter? _filter;
    private Action<Exception>? _failure;
    private bool _attached, _disposed;
    private NativeWindowMessageHook(nint hwnd, Filter filter, Action<Exception> failure)
    {
        _hwnd = hwnd; _filter = filter; _failure = failure;
        _id = unchecked((nuint)Interlocked.Increment(ref _sequence));
        _thread = GetCurrentThreadId(); _callback = Dispatch;
        if (hwnd == 0 || !IsWindow(hwnd)) throw new ArgumentException("A live HWND is required.", nameof(hwnd));
        if (GetWindowThreadProcessId(hwnd, out _) != _thread)
            throw new InvalidOperationException("Window subclassing must run on the owning thread.");
        if (!Roots.TryAdd(_id, this)) throw new InvalidOperationException("Window subclass identity exhausted.");
        try
        {
            if (!SetWindowSubclass(hwnd, _callback, _id, 0))
                throw new InvalidOperationException("SetWindowSubclass rejected the live window.");
            _attached = true;
        }
        catch
        {
            Roots.TryRemove(_id, out _); _filter = null; _failure = null;
            throw;
        }
    }
    internal static NativeWindowMessageHook? Attach(Window window, Filter filter, Action<Exception> failure)
    {
        ArgumentNullException.ThrowIfNull(window); ArgumentNullException.ThrowIfNull(filter); ArgumentNullException.ThrowIfNull(failure);
        if (!OperatingSystem.IsWindows()) return null;
        var hwnd = Microsoft.Windows.Shell.NativeChrome.Handle(window);
        return hwnd == 0 ? null : new(hwnd, filter, failure);
    }
    private nint Dispatch(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        // No managed exception may unwind into comctl32. Destruction must always
        // reach the original window procedure even when a user filter handles it.
        const uint NcDestroy = 0x0082;
        try
        {
            if (!_disposed && _filter is { } filter)
            {
                var handled = false;
                var result = filter(hwnd, unchecked((int)message), unchecked((nint)wParam), lParam, ref handled);
                if (handled && message != NcDestroy) return result;
            }
        }
        catch (Exception error)
        {
            try { _failure?.Invoke(error); } catch (Exception) { /* Contain observer failures too. */ }
        }
        finally
        {
            if (message == NcDestroy)
            {
                // During WM_NCDESTROY the helper removes its per-HWND state.
                _disposed = true; _attached = false; _filter = null; _failure = null;
                Roots.TryRemove(_id, out _);
            }
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }
    public void Dispose()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (GetCurrentThreadId() != _thread)
            throw new InvalidOperationException("Remove the subclass on the window's owning thread.");
        _disposed = true; _filter = null; _failure = null;
        if (!_attached) return;
        if (RemoveWindowSubclass(_hwnd, _callback, _id) || !IsWindow(_hwnd))
        {
            _attached = false; Roots.TryRemove(_id, out _);
        }
        // A failed removal retains only a forwarding callback until NCDESTROY.
        // Unrooting its delegate here would leave a dangling native function pointer.
        GC.KeepAlive(this);
    }
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern uint GetCurrentThreadId();
}
