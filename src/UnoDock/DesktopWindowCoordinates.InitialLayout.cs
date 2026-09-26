using System.Runtime.InteropServices;

namespace UnoDock;

public sealed partial class DesktopWindowCoordinates
{
    /// <summary>Notify the X11 host of its actual initial client geometry after
    /// activation. No frame, focus, ownership or input state is changed.</summary>
    internal void RefreshInitialNativeLayout(Window window)
    {
        Verify();
        ArgumentNullException.ThrowIfNull(window);
#if !WINDOWS
        if (!OperatingSystem.IsLinux() || Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native)
            return;
        var id = Id(native.WindowId);
        var connection = Connection;
        var reply = Xcb.GeometryReply(connection, Xcb.Geometry(connection, id), out var error);
        InitialConfigureEvent notification;
        try
        {
            CheckReply(reply, error);
            notification = new()
            {
                Type = 22,
                Event = id,
                Window = id,
                X = Marshal.ReadInt16(reply, 12),
                Y = Marshal.ReadInt16(reply, 14),
                Width = (ushort)Marshal.ReadInt16(reply, 16),
                Height = (ushort)Marshal.ReadInt16(reply, 18),
                BorderWidth = (ushort)Marshal.ReadInt16(reply, 20)
            };
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }
        reply = Xcb.AttributesReply(connection, Xcb.Attributes(connection, id), out error);
        try
        {
            CheckReply(reply, error);
            notification.OverrideRedirect = Marshal.ReadByte(reply, 27);
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }
        // Uno's X11 event thread polls the socket before inspecting Xlib's
        // buffered queue. Synchronous pre-show requests can already have moved
        // ConfigureNotify into that queue. Send one checked post-show notice from
        // our independent connection so the host drains and reconciles it.
        // Configure handling queries the live server geometry, not this payload.
        error = Xcb.RequestCheck(connection, Xcb.InitialConfigure(connection, 0, id, 1u << 17, ref notification));
        try
        {
            if (error != 0)
                throw new InvalidOperationException("The initial X11 client geometry notification was rejected.");
        }
        finally
        {
            Xcb.Free(error);
        }
#endif
    }

#if !WINDOWS
    // SendEvent copies the entire 32-byte core event, including the padding.
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct InitialConfigureEvent
    {
        internal byte Type, Padding;
        internal ushort Sequence;
        internal uint Event, Window, AboveSibling;
        internal short X, Y;
        internal ushort Width, Height, BorderWidth;
        internal byte OverrideRedirect, PaddingEnd;
    }

    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_send_event_checked")]
        internal static extern uint InitialConfigure(XcbConnection connection, byte propagate, uint destination, uint eventMask, ref InitialConfigureEvent notification);
    }
#endif
}
