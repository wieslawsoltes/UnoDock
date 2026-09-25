using System.Runtime.InteropServices;
using UnoDock.Internal;

namespace UnoDock;

internal readonly record struct DesktopPointerState(Point Position, bool LeftDown, bool ControlDown, bool EscapeDown);

public sealed partial class DesktopWindowCoordinates
{
    internal static Window? WindowFor(FrameworkElement element) => Microsoft.Windows.Shell.WindowRegistry.Find(element);
    internal bool TryGetPointer(Window window, out DesktopPointerState state)
    {
        Verify(); state = default;
        if (OperatingSystem.IsWindows())
        {
            if (!W32.GetCursorPos(out var point)) return false;
            state = new(new(point.X, point.Y), W32.Down(1), W32.Down(0x11), W32.Down(0x1b)); return true;
        }
#if !WINDOWS
        if (OperatingSystem.IsMacOS()) { state = MacDesktopInterop.Pointer(); return true; }
        if (OperatingSystem.IsLinux() && Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.X11NativeWindow native)
        {
            var connection = Connection;
            var reply = Xcb.PointerReply(connection, Xcb.Pointer(connection, Id(native.WindowId)), out var error);
            try
            {
                CheckReply(reply, error);
                if (Marshal.ReadByte(reply, 1) == 0) return false;
                var mask = (ushort)Marshal.ReadInt16(reply, 24);
                state = new(new(Marshal.ReadInt16(reply, 16), Marshal.ReadInt16(reply, 18)),
                    (mask & 0x100) != 0, (mask & 4) != 0, EscapeDownX11());
                return true;
            }
            finally { Xcb.Free(reply); Xcb.Free(error); }
        }
#endif
        return false;
    }
    internal static Point NativeOrigin(Window window)
    {
#if !WINDOWS
        if (OperatingSystem.IsMacOS()) return MacDesktopInterop.Origin(window);
#endif
        return new(window.AppWindow.Position.X, window.AppWindow.Position.Y);
    }
    internal static void MoveNative(Window window, Point origin)
    {
        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y)) throw new ArgumentOutOfRangeException(nameof(origin));
#if !WINDOWS
        if (OperatingSystem.IsMacOS()) { MacDesktopInterop.Move(window, origin); return; }
#endif
        window.AppWindow.Move(new() { X = checked((int)Math.Round(origin.X)), Y = checked((int)Math.Round(origin.Y)) });
    }
    internal bool IsNativeCaption(Window window, Point screen)
    {
        Verify();
        if (OperatingSystem.IsWindows())
        {
            var handle = WindowsHandle(window);
            if (!W32.GetWindowRect(handle, out var frame) || !W32.ClientToScreen(handle, ref W32.Zero)) return false;
            var client = W32.ClientOrigin(handle);
            return screen.X >= frame.Left && screen.X < frame.Right && screen.Y >= frame.Top && screen.Y < client.Y;
        }
#if !WINDOWS
        if (OperatingSystem.IsMacOS()) return MacDesktopInterop.IsCaption(window, screen);
        if (OperatingSystem.IsLinux() && Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.X11NativeWindow native)
        {
            var id = Id(native.WindowId); var root = RootX11(id); var top = TopLevelX11(id, root);
            if (top == id) return false; // No window-manager decoration (for example Xvfb).
            var client = TranslateX11(id, root);
            return BoundsX11(top, root).Contains(screen) && screen.Y < client.Y;
        }
#endif
        return false;
    }
    internal IDisposable? ConfigureOwner(Window window, Window? owner, bool tool)
    {
        Verify();
        if (owner == null || ReferenceEquals(window, owner)) return null;
        if (OperatingSystem.IsWindows()) return W32.Own(WindowsHandle(window), WindowsHandle(owner), tool);
#if !WINDOWS
        if (OperatingSystem.IsMacOS()) return MacDesktopInterop.SetOwner(window, owner, tool);
        if (OperatingSystem.IsLinux() && Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.X11NativeWindow child &&
            Uno.UI.Xaml.WindowHelper.GetNativeWindow(owner) is Uno.UI.NativeElementHosting.X11NativeWindow parent)
        {
            var id = Id(child.WindowId);
            PropertyX11(id, AtomX11("WM_TRANSIENT_FOR"), 33, [Id(parent.WindowId)]);
            if (tool)
            {
                PropertyX11(id, AtomX11("_NET_WM_WINDOW_TYPE"), 4, [AtomX11("_NET_WM_WINDOW_TYPE_UTILITY")]);
                var stateAtom = AtomX11("_NET_WM_STATE");
                var state = ReadAtomsX11(id, stateAtom);
                PropertyX11(id, stateAtom, 4, state.Append(AtomX11("_NET_WM_STATE_SKIP_TASKBAR")).Distinct().ToArray());
            }
        }
#endif
        return null;
    }
    private static nint WindowsHandle(Window window)
    {
#if WINDOWS
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
#else
        return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.Win32NativeWindow native
            ? native.Hwnd : throw new PlatformNotSupportedException("A Win32 native host is required.");
#endif
    }
    private static class W32
    {
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct NativeRect { internal int Left, Top, Right, Bottom; }
        internal static NativePoint Zero;
        internal static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
        internal static Point ClientOrigin(nint handle)
        {
            var point = new NativePoint();
            if (!ClientToScreen(handle, ref point)) throw new InvalidOperationException("The native client has closed.");
            return new(point.X, point.Y);
        }
        internal static IDisposable Own(nint child, nint owner, bool tool)
        {
            var oldOwner = GetLong(child, -8); var oldStyle = GetLong(child, -20);
            var lease = new OwnerLease(child, oldOwner, oldStyle);
            try
            {
                SetLong(child, -8, owner);
                if (tool) SetLong(child, -20, (nint)((oldStyle.ToInt64() | 0x80L) & ~0x40000L));
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        private sealed class OwnerLease(nint child, nint owner, nint style) : IDisposable
        {
            private nint _child = child;
            public void Dispose()
            {
                var window = _child; _child = 0;
                if (window == 0 || !IsWindow(window)) return;
                SetLong(window, -8, owner); SetLong(window, -20, style);
            }
        }
        private static nint GetLong(nint window, int index) => IntPtr.Size == 8 ? GetWindowLongPtr(window, index) : GetWindowLong(window, index);
        private static void SetLong(nint window, int index, nint value)
        {
            Marshal.SetLastPInvokeError(0);
            var previous = IntPtr.Size == 8 ? SetWindowLongPtr(window, index, value) : (nint)SetWindowLong(window, index, value.ToInt32());
            if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ClientToScreen(nint window, ref NativePoint point);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetClientRect(nint window, out NativeRect rect);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint window, out NativeRect rect);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint window);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint window);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint window);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowEnabled(nint window);
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetTopWindow(nint parent);
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetWindow(nint window, uint command);
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetAncestor(nint window, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr(nint window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(nint window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong(nint window, int index, int value);
    }
#if !WINDOWS
    private byte? _escapeKeycode;
    private bool EscapeDownX11()
    {
        var connection = Connection;
        if (_escapeKeycode == null)
        {
            var setup = Xcb.Setup(connection);
            if (setup == 0) throw new InvalidOperationException("The X11 display setup is unavailable.");
            // xcb_setup_t: min_keycode/max_keycode at bytes 34/35.
            var first = Marshal.ReadByte(setup, 34); var last = Marshal.ReadByte(setup, 35);
            var count = last - first + 1;
            if (count is <= 0 or > 255) throw new InvalidOperationException("Invalid X11 keyboard range.");
            var map = Xcb.KeyboardMapReply(connection, Xcb.KeyboardMap(connection, first, (byte)count), out var error);
            try
            {
                CheckReply(map, error);
                var per = Marshal.ReadByte(map, 1);
                if ((long)count * per > (uint)Marshal.ReadInt32(map, 4)) throw new InvalidOperationException("Incomplete X11 key mapping reply.");
                _escapeKeycode = 0;
                for (var row = 0; row < count && _escapeKeycode == 0; row++)
                    for (var col = 0; col < per; col++)
                        if (Marshal.ReadInt32(map, 32 + (row * per + col) * 4) == 0xff1b) { _escapeKeycode = (byte)(first + row); break; }
            }
            finally { Xcb.Free(map); Xcb.Free(error); }
        }
        if (_escapeKeycode == 0) return false;
        var keys = Xcb.KeymapReply(connection, Xcb.Keymap(connection), out var keyError);
        try { CheckReply(keys, keyError); var code = _escapeKeycode!.Value; return (Marshal.ReadByte(keys, 8 + code / 8) & (1 << (code % 8))) != 0; }
        finally { Xcb.Free(keys); Xcb.Free(keyError); }
    }
    private uint AtomX11(string name)
    {
        var reply = Xcb.AtomReply(Connection, Xcb.Atom(Connection, 0, checked((ushort)name.Length), name), out var error);
        try { CheckReply(reply, error); return unchecked((uint)Marshal.ReadInt32(reply, 8)); }
        finally { Xcb.Free(reply); Xcb.Free(error); }
    }
    private void PropertyX11(uint window, uint property, uint type, uint[] values)
    {
        var error = Xcb.RequestCheck(Connection, Xcb.Property(Connection, 0, window, property, type, 32, (uint)values.Length, values));
        try { if (error != 0) throw new InvalidOperationException("Unable to configure native X11 floating-window ownership."); }
        finally { Xcb.Free(error); }
    }
    private uint[] ReadAtomsX11(uint window, uint property)
    {
        var reply = Xcb.GetPropertyReply(Connection, Xcb.GetProperty(Connection, 0, window, property, 4, 0, 1024), out var error);
        try
        {
            CheckReply(reply, error);
            if (Marshal.ReadByte(reply, 1) == 0) return [];
            var count = Marshal.ReadInt32(reply, 16);
            if (Marshal.ReadByte(reply, 1) != 32 || count < 0 || count > 1024 || Marshal.ReadInt32(reply, 12) != 0)
                throw new InvalidOperationException("The existing X11 window state is not a bounded atom list.");
            var result = new uint[count];
            for (var i = 0; i < result.Length; i++) result[i] = unchecked((uint)Marshal.ReadInt32(reply, 32 + i * 4));
            return result;
        }
        finally { Xcb.Free(reply); Xcb.Free(error); }
    }
    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_pointer")] internal static extern uint Pointer(XcbConnection connection, uint window);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_pointer_reply")] internal static extern nint PointerReply(XcbConnection connection, uint cookie, out nint error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_setup")] internal static extern nint Setup(XcbConnection connection);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_keyboard_mapping")] internal static extern uint KeyboardMap(XcbConnection connection, byte first, byte count);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_keyboard_mapping_reply")] internal static extern nint KeyboardMapReply(XcbConnection connection, uint cookie, out nint error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_keymap")] internal static extern uint Keymap(XcbConnection connection);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_query_keymap_reply")] internal static extern nint KeymapReply(XcbConnection connection, uint cookie, out nint error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_intern_atom")] internal static extern uint Atom(XcbConnection connection, byte onlyExisting, ushort length, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_intern_atom_reply")] internal static extern nint AtomReply(XcbConnection connection, uint cookie, out nint error);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_change_property_checked")] internal static extern uint Property(XcbConnection connection, byte mode, uint window, uint property, uint type, byte format, uint length, [In] uint[] data);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_property")] internal static extern uint GetProperty(XcbConnection connection, byte delete, uint window, uint property, uint type, uint offset, uint length);
        [DllImport("libxcb.so.1", EntryPoint = "xcb_get_property_reply")] internal static extern nint GetPropertyReply(XcbConnection connection, uint cookie, out nint error);
    }
#endif
}
