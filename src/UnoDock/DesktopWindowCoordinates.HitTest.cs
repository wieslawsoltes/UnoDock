using System.Runtime.InteropServices;
using UnoDock.Internal;

namespace UnoDock;

public sealed partial class DesktopWindowCoordinates
{
    /// <summary>Native stacking query with one explicitly excluded drag source.
        /// Foreign windows remain occluders; focus order is never a z-order substitute.</summary>
        internal bool TryGetTopmostRootExcludingWindow(FrameworkElement source, Point point, out XamlRoot? hitRoot, Window? excluded)
    {
        Verify();
        Validate(source, point);
        hitRoot = null;
        if (OperatingSystem.IsWindows())
        {
            var screen = ToScreen(source, point);
            var windows = Microsoft.Windows.Shell.WindowRegistry.Snapshot();
            var ignored = excluded == null ? 0 : W32.GetAncestor(WindowsHandle(excluded), 2);
            var current = W32.GetTopWindow(0);
            for (var visited = 0; current != 0 && visited < 4096; visited++, current = W32.GetWindow(current, 2))
            {
                if (current == ignored || !W32.IsWindowVisible(current) || W32.IsIconic(current) || !W32.GetWindowRect(current, out var frame) || screen.X < frame.Left || screen.X >= frame.Right || screen.Y < frame.Top || screen.Y >= frame.Bottom)
                    continue;
                foreach (var window in windows)
                {
                    if (ReferenceEquals(window, excluded) || window.Content?.XamlRoot is not { } root)
                        continue;
                    var handle = WindowsHandle(window);
                    if (W32.GetAncestor(handle, 2) != current)
                        continue;
                    if (!W32.IsWindowEnabled(handle) || !W32.GetClientRect(handle, out var client))
                        return true;
                    var origin = W32.ClientOrigin(handle);
                    if (new Rect(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top).Contains(screen))
                        hitRoot = root;
                    return true;
                }

                return true;
            }

            return true;
        }

#if !WINDOWS
        if (OperatingSystem.IsMacOS())
        {
            hitRoot = MacDesktopInterop.HitTest(ToScreen(source, point), excluded);
            return true;
        }

        if (OperatingSystem.IsLinux() && GetNative(source) is Uno.UI.NativeElementHosting.X11NativeWindow native)
        {
            var id = Id(native.WindowId);
            var root = RootX11(id);
            var screen = ToScreen(source, point);
            var ignored = excluded != null && Uno.UI.Xaml.WindowHelper.GetNativeWindow(excluded) is Uno.UI.NativeElementHosting.X11NativeWindow ignoredNative ? TopLevelX11(Id(ignoredNative.WindowId), root) : 0;
            var windows = Uno.UI.ApplicationHelper.Windows.Where(w => w.Content?.XamlRoot != null && !ReferenceEquals(w, excluded)).Select(w => (Window: w, Native: Uno.UI.Xaml.WindowHelper.GetNativeWindow(w) as Uno.UI.NativeElementHosting.X11NativeWindow)).Where(w => w.Native != null).ToDictionary(w => Id(w.Native!.WindowId), w => w.Window.Content!.XamlRoot!);
            hitRoot = Descend(root, 0);
            return true;
            XamlRoot? Descend(uint parent, int depth)
            {
                if (depth >= 32)
                    return null;
                var children = TreeX11(parent).Children;
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    var child = children[i];
                    if (child == ignored || !ViewableX11(child) || !BoundsX11(child, root).Contains(screen))
                        continue;
                    if (windows.TryGetValue(child, out var childRoot))
                        return childRoot;
                    return Descend(child, depth + 1); // A foreign frame still blocks windows below it.
                }

                return null;
            }
        }

#endif
        return false;
    }
#if !WINDOWS
    private (uint Parent, uint[] Children) TreeX11(uint window)
    {
        var reply = Xcb.TreeReply(Connection, Xcb.Tree(Connection, window), out var error);
        try
        {
            CheckReply(reply, error);
            var count = (ushort)Marshal.ReadInt16(reply, 16);
            var children = new uint[count];
            for (var i = 0; i < count; i++)
                children[i] = unchecked((uint)Marshal.ReadInt32(reply, 32 + i * 4));
            return (unchecked((uint)Marshal.ReadInt32(reply, 12)), children);
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }
    }

    private uint TopLevelX11(uint window, uint root)
    {
        for (var depth = 0; depth < 32; depth++)
        {
            var parent = TreeX11(window).Parent;
            if (parent == root || parent == 0)
                return window;
            if (parent == window)
                break;
            window = parent;
        }

        throw new InvalidOperationException("Invalid native X11 parent hierarchy.");
    }

    private bool ViewableX11(uint window)
    {
        var reply = Xcb.AttributesReply(Connection, Xcb.Attributes(Connection, window), out var error);
        try
        {
            CheckReply(reply, error);
            return Marshal.ReadByte(reply, 26) == 2;
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }
    }

    private Rect BoundsX11(uint window, uint root)
    {
        var reply = Xcb.GeometryReply(Connection, Xcb.Geometry(Connection, window), out var error);
        try
        {
            CheckReply(reply, error);
            if (unchecked((uint)Marshal.ReadInt32(reply, 8)) != root)
                return default;
            var origin = TranslateX11(window, root);
            var border = (ushort)Marshal.ReadInt16(reply, 20);
            return new(origin.X - border, origin.Y - border, (ushort)Marshal.ReadInt16(reply, 16) + 2 * border, (ushort)Marshal.ReadInt16(reply, 18) + 2 * border);
        }
        finally
        {
            Xcb.Free(reply);
            Xcb.Free(error);
        }
    }

    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_tree")]
        internal static extern uint Tree(XcbConnection connection, uint window);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_tree_reply")]
        internal static extern nint TreeReply(XcbConnection connection, uint cookie, out nint error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_window_attributes")]
        internal static extern uint Attributes(XcbConnection connection, uint window);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_window_attributes_reply")]
        internal static extern nint AttributesReply(XcbConnection connection, uint cookie, out nint error);
    }
#endif
}
