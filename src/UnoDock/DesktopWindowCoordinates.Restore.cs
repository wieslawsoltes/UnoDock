using System.Runtime.InteropServices;

namespace UnoDock;

public sealed partial class DesktopWindowCoordinates
{
    /// <summary>Complete the missing X11 unmaximize request before asking Uno to
    /// reactivate a restored window. The WM remains authoritative for saved normal
    /// geometry. No unrelated EWMH state or minimized restore state is rewritten.</summary>
    internal void PrepareRestore(Window window)
    {
        Verify(); ArgumentNullException.ThrowIfNull(window);
#if !WINDOWS
        if (!OperatingSystem.IsLinux() || Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native) return;
        var id = Id(native.WindowId); var stateAtom = AtomX11("_NET_WM_STATE");
        var states = ReadAtomsX11(id, stateAtom);
        if (states.Contains(AtomX11("_NET_WM_STATE_HIDDEN"))) return;
        var horizontal = AtomX11("_NET_WM_STATE_MAXIMIZED_HORZ");
        var vertical = AtomX11("_NET_WM_STATE_MAXIMIZED_VERT");
        if (!states.Contains(horizontal) && !states.Contains(vertical)) return;
        var message = new RestoreClientMessage
        {
            ResponseType = 33, Format = 32, Window = id, Type = stateAtom,
            Action = 0, First = horizontal, Second = vertical, Source = 1
        };
        // EWMH _NET_WM_STATE_REMOVE, source=application. Use a client message,
        // not a property replacement; the WM owns the state of a mapped window.
        var connection = Connection;
        var error = Xcb.RequestCheck(connection, Xcb.SendRestoreMessage(connection, 0, RootX11(id), (1u << 19) | (1u << 20), ref message));
        try { if (error != 0) throw new InvalidOperationException("The native X11 restore request was rejected by the X server."); }
        finally { Xcb.Free(error); }
#endif
    }
#if !WINDOWS
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct RestoreClientMessage
    {
        internal byte ResponseType, Format;
        internal ushort Sequence;
        internal uint Window, Type, Action, First, Second, Source, Reserved;
    }
    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_send_event_checked")]
        internal static extern uint SendRestoreMessage(XcbConnection connection, byte propagate, uint destination, uint eventMask, ref RestoreClientMessage message);
    }
#endif
}
