#if !WINDOWS
using System.Runtime.InteropServices;

namespace UnoDock;

public sealed partial class DesktopWindowCoordinates
{
    /// <summary>Mark an unmapped X11 window's position and size as requested by
        /// the program (ICCCM PPosition/PSize with USPosition/USSize) so the window
        /// manager keeps them instead of applying its own placement when the window
        /// is first shown. Other WM_NORMAL_HINTS fields are preserved.</summary>
        internal void RequestInitialPlacementX11(Window window, DockRect pixels)
    {
        Verify();
        if (!OperatingSystem.IsLinux() || Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow native)
            return;
        var id = Id(native.WindowId);
        var property = AtomX11("WM_NORMAL_HINTS");
        var type = AtomX11("WM_SIZE_HINTS");
        var hints = new uint[18];
        var reply = Xcb.GetPropertyReply(Connection, Xcb.GetProperty(Connection, 0, id, property, type, 0, 18), out var error);
        try
        {
            CheckReply(reply, error);
            var count = Marshal.ReadInt32(reply, 16);
            if (Marshal.ReadByte(reply, 1) == 32 && count > 0)
                for (var i = 0; i < Math.Min(count, hints.Length); i++)
                    hints[i] = unchecked((uint)Marshal.ReadInt32(reply, 32 + i * 4));
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }

        hints[0] |= 1 | 2 | 4 | 8; // USPosition | USSize | PPosition | PSize
        hints[1] = unchecked((uint)(int)Math.Round(pixels.X));
        hints[2] = unchecked((uint)(int)Math.Round(pixels.Y));
        hints[3] = (uint)Math.Max(1, Math.Round(pixels.Width));
        hints[4] = (uint)Math.Max(1, Math.Round(pixels.Height));
        PropertyX11(id, property, type, hints);
    }
}
#endif
