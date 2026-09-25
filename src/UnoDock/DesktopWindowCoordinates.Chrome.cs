#if !WINDOWS
using System.Runtime.InteropServices;
using UnoDock.Internal;

namespace UnoDock;
public sealed partial class DesktopWindowCoordinates
{
    internal static NativeFloatingChrome CreateX11FloatingChrome(Window window) => new X11FloatingChrome(window);
    private sealed class X11FloatingChrome(Window window) : NativeFloatingChrome(window)
    {
        private readonly DesktopWindowCoordinates _coordinates = new();
        private uint _window, _atom;
        private uint[]? _before, _applied;
        private bool _changed;
        protected override void EnableCore()
        {
            if (Uno.UI.Xaml.WindowHelper.GetNativeWindow(Window)is not Uno.UI.NativeElementHosting.X11NativeWindow native)
                throw new PlatformNotSupportedException("Custom Linux decorations require an X11 native host; pure Wayland is not inferred from the OS name.");
            _window = Id(native.WindowId);
            _atom = _coordinates.AtomX11("_MOTIF_WM_HINTS");
            _before = ReadHints();
            _applied = _before == null ? new uint[5] : (uint[])_before.Clone();
            _applied[0] |= 2; // MWM_HINTS_DECORATIONS. Preserve function/input/status fields.
            _applied[2] = 0;
            _changed = true;
            _coordinates.PropertyX11(_window, _atom, _atom, _applied);
        }

        private uint[]? ReadHints()
        {
            var c = _coordinates.Connection;
            var reply = Xcb.GetPropertyReply(c, Xcb.GetProperty(c, 0, _window, _atom, 0, 0, 64), out var error);
            try
            {
                CheckReply(reply, error);
                if (Marshal.ReadByte(reply, 1) == 0)
                    return null;
                var count = Marshal.ReadInt32(reply, 16);
                if (Marshal.ReadByte(reply, 1) != 32 || unchecked((uint)Marshal.ReadInt32(reply, 8)) != _atom || count < 5 || count > 64 || Marshal.ReadInt32(reply, 12) != 0 || (uint)Marshal.ReadInt32(reply, 4) < count)
                    throw new InvalidOperationException("The existing Motif decoration property is malformed or unbounded.");
                var values = new uint[count];
                for (var i = 0; i < count; i++)
                    values[i] = unchecked((uint)Marshal.ReadInt32(reply, 32 + i * 4));
                return values;
            }
            finally
            {
                Xcb.Free(reply);
                Xcb.Free(error);
            }
        }

        internal override DockRect ReadBounds()
        {
            Verify();
            var root = _coordinates.RootX11(_window);
            var frame = _coordinates.BoundsX11(_coordinates.TopLevelX11(_window, root), root);
            return new(frame.X, frame.Y, frame.Width, frame.Height);
        }

        internal override void WriteBounds(DockRect bounds)
        {
            Verify();
            Window.AppWindow.Resize(new() { Width = checked((int)Math.Round(bounds.Width)), Height = checked((int)Math.Round(bounds.Height)) });
            if (!IsDisposed)
                MoveNative(Window, new(bounds.X, bounds.Y));
        }

        protected override void RestoreCore()
        {
            if (!_changed || _applied == null || ReadHints()is not { } current || !current.SequenceEqual(_applied))
                return;
            if (_before != null)
            {
                _coordinates.PropertyX11(_window, _atom, _atom, _before);
                return;
            }

            var error = Xcb.RequestCheck(_coordinates.Connection, Xcb.DeleteProperty(_coordinates.Connection, _window, _atom));
            try
            {
                if (error != 0)
                    throw new InvalidOperationException("Unable to restore the original X11 decorations.");
            }
            finally
            {
                Xcb.Free(error);
            }
        }

        protected override void ReleaseCore() => _coordinates.Dispose();
    }

    private static partial class Xcb
    {
        [DllImport("libxcb.so.1", EntryPoint = "xcb_delete_property_checked")]
        internal static extern uint DeleteProperty(XcbConnection connection, uint window, uint property);
    }
}
#endif
