#if WINDOWS
namespace Microsoft.Windows.Shell;
/// <summary>Observes a WinUI host island's top-level window. The system destroys
/// owned windows before the owner's AppWindow raises Destroying, which a WinUI
/// window does not survive, so owned windows are released when the owner hides
/// (the first step of its destruction) and owned again when it is shown.</summary>
internal sealed class HostWindowSubclass : IDisposable
{
    private const uint WindowPosChanging = 0x0046;
    private const uint WindowPosChanged = 0x0047;
    private const uint NonClientDestroy = 0x0082;
    private const uint HideWindowFlag = 0x0080;
    private const uint ShowWindowFlag = 0x0040;
    private static nuint _nextId;
    private readonly nint _handle;
    private readonly nuint _id;
    private readonly Action _hiding;
    private readonly Action _shown;
    // Rooted for the lifetime of the subclass: the system calls it directly.
    private readonly SubclassProc _proc;
    private bool _attached;
    internal HostWindowSubclass(nint handle, Action hiding, Action shown)
    {
        _handle = handle;
        _hiding = hiding;
        _shown = shown;
        _proc = WindowProc;
        _id = ++_nextId;
        _attached = SetWindowSubclass(handle, _proc, _id, 0);
    }

    public void Dispose()
    {
        if (!_attached)
            return;
        _attached = false;
        RemoveWindowSubclass(_handle, _proc, _id);
    }

    private nint WindowProc(nint window, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        try
        {
            if (message == WindowPosChanging && lParam != 0 && (Marshal.PtrToStructure<WindowPos>(lParam).Flags & HideWindowFlag) != 0 && IsWindowVisible(window))
                _hiding();
            else if (message == WindowPosChanged && lParam != 0 && (Marshal.PtrToStructure<WindowPos>(lParam).Flags & ShowWindowFlag) != 0)
                _shown();
            else if (message == NonClientDestroy)
                Dispose();
        }
        catch (Exception error) when (error is InvalidOperationException or COMException)
        {
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        internal nint Window;
        internal nint InsertAfter;
        internal int X, Y, Width, Height;
        internal uint Flags;
    }

    private delegate nint SubclassProc(nint window, uint message, nint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
}
#endif
